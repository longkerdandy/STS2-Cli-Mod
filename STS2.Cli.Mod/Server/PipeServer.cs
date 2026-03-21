using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using STS2.Cli.Mod.Actions;
using STS2.Cli.Mod.Models.Message;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Server;

/// <summary>
///     Named Pipe server for communication with the CLI tool.
///     Runs a single-connection loop: create pipe → wait for client → handle one request → disconnect → repeat.
/// </summary>
public static class PipeServer
{
    private static readonly ModLogger Logger = new("PipeServer");
    private static CancellationTokenSource? _cts;
    private static Task? _serverTask;

    /// <summary>
    ///     Starts the pipe server loop in the background.
    ///     Safe to call multiple times; subsequent calls are no-ops.
    /// </summary>
    public static void Start()
    {
        if (_serverTask != null)
        {
            Logger.Warning("Named Pipe Server already started");
            return;
        }

        _cts = new CancellationTokenSource();
        _serverTask = Task.Run(() => RunServerLoopAsync(_cts.Token));
    }

    /// <summary>
    ///     Stops the pipe server and waits up to 2 seconds for graceful shutdown.
    /// </summary>
    public static async Task StopAsync()
    {
        if (_cts != null) await _cts.CancelAsync();
        if (_serverTask != null) await Task.WhenAny(_serverTask, Task.Delay(TimeSpan.FromSeconds(2)));

        _cts?.Dispose();
        _cts = null;
        _serverTask = null;
    }

    /// <summary>
    ///     Main server loop that creates pipe instances and waits for client connections.
    ///     Automatically recreates the pipe after each client disconnects.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the server loop.</param>
    private static async Task RunServerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                Logger.Info("Creating Named Pipe instance...");

                // Set up security to allow all users access
                var pipeSecurity = new PipeSecurity();
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    PipeAccessRights.ReadWrite,
                    AccessControlType.Allow));

                pipe = NamedPipeServerStreamAcl.Create(
                    "sts2-cli-mod",
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    4096,
                    4096,
                    pipeSecurity);

                Logger.Info("Waiting for CLI connection...");
                await pipe.WaitForConnectionAsync(ct);
                Logger.Info("CLI connected!");
                await HandleClientAsync(pipe, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Error($"Named Pipe error: {ex.Message}");
                await Task.Delay(1000, ct); // Wait before retry
            }
            finally
            {
                if (pipe != null) await pipe.DisposeAsync();
            }
        }
    }

    /// <summary>
    ///     Handles a single client connection.
    ///     Reads one request, processes it, writes the response, then returns (caller disposes pipe).
    /// </summary>
    /// <param name="pipe">The connected named pipe stream.</param>
    /// <param name="ct">Cancellation token.</param>
    private static async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        // leaveOpen: true — pipe lifecycle is managed by the caller (RunServerLoopAsync)
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true);
        writer.AutoFlush = true;

        try
        {
            // Short connection mode: handle single request per connection
            var requestJson = await reader.ReadLineAsync(ct);
            if (requestJson == null)
                return;

            Logger.Info($"Received: {requestJson}");

            // Parse request
            Request? request;
            try
            {
                request = JsonSerializer.Deserialize<Request>(requestJson, JsonOptions.Default);
            }
            catch (Exception)
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(
                    new { ok = false, error = "INVALID_REQUEST", message = "Failed to parse request" },
                    JsonOptions.Default));
                return;
            }

            if (request == null)
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(
                    new { ok = false, error = "INVALID_REQUEST", message = "Failed to parse request" },
                    JsonOptions.Default));
                return;
            }

            // Process the request and write response
            var response = ProcessRequest(request);
            var responseJson = JsonSerializer.Serialize(response, JsonOptions.Default);
            await writer.WriteLineAsync(responseJson);
        }
        catch (IOException)
        {
            Logger.Info("Pipe Client disconnected");
        }
        catch (Exception ex)
        {
            Logger.Error($"Pipe Client handler error: {ex.Message}");
        }
    }

    /// <summary>
    ///     Processes a parsed request and routes it to the appropriate handler.
    ///     Commands that access game state are executed on the main thread via <see cref="MainThreadExecutor" />.
    /// </summary>
    /// <param name="request">The parsed request object.</param>
    /// <returns>Response object to be serialized as JSON.</returns>
    private static object ProcessRequest(Request request)
    {
        try
        {
            return request.Cmd.ToLower() switch
            {
                // ping does not access game state — handle directly on pipe thread
                "ping" => new { ok = true, data = new { connected = true } },

                // All other commands require game state access on the main thread
                _ => MainThreadExecutor.RunOnMainThread(() => request.Cmd.ToLower() switch
                {
                    "state" => HandleStateRequest(),
                    "play_card" => HandlePlayCardRequest(request.Args, request.Target),
                    "end_turn" => HandleEndTurnRequest(),
                    _ => (object)new
                    {
                        ok = false, error = "UNKNOWN_COMMAND", message = $"Unknown command: {request.Cmd}"
                    }
                })
            };
        }
        catch (Exception ex)
        {
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Handles the 'state' command by extracting current game state.
    /// </summary>
    /// <returns>Response containing the game state DTO.</returns>
    private static object HandleStateRequest()
    {
        var state = GameStateExtractor.GetState();

        if (state.Error != null)
            return new { ok = false, error = "STATE_EXTRACTION_ERROR", message = state.Error };

        return new { ok = true, data = state };
    }

    /// <summary>
    ///     Handles the 'play_card' command by validating arguments and invoking the play card handler.
    /// </summary>
    /// <param name="args">Command arguments, expects card index as first element.</param>
    /// <param name="target">Optional target combat ID for targeted cards.</param>
    /// <returns>Response indicating success or failure of the play card action.</returns>
    private static object HandlePlayCardRequest(int[]? args, int? target)
    {
        if (args == null || args.Length == 0)
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Card index required" };

        var cardIndex = args[0];
        Logger.Info($"Requested to play card at index {cardIndex}, target={target?.ToString() ?? "none"}");

        return PlayCardHandler.Execute(cardIndex, target);
    }

    /// <summary>
    ///     Handles the 'end_turn' command by invoking the end turn handler.
    /// </summary>
    /// <returns>Response indicating success or failure of the end turn action.</returns>
    private static object HandleEndTurnRequest()
    {
        Logger.Info("Requested to end turn");

        return EndTurnHandler.Execute();
    }
}
