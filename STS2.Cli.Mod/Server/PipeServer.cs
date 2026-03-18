using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using PlayCardHandler = STS2.Cli.Mod.Actions.PlayCardHandler;
using EndTurnActionClass = STS2.Cli.Mod.Actions.EndTurnAction;
using STS2.Cli.Mod.Models.Message;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Server;

/// <summary>
///     Named Pipe server for communication with the CLI tool.
///     Uses System.IO.Pipes for cross-platform support.
/// </summary>
public class PipeServer : IDisposable
{
    private static readonly ModLogger Logger = new("PipeServer");
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private NamedPipeServerStream? _pipeServer;

    /// <summary>
    ///     Releases all resources used by the pipe server.
    /// </summary>
    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _cts?.Dispose();
    }

    /// <summary>
    ///     Starts the pipe server in the background.
    /// </summary>
    public Task StartAsync()
    {
        if (_listenerTask != null)
        {
            Logger.Warning("Named Pipe Server already started");
            return Task.CompletedTask;
        }

        _cts = new CancellationTokenSource();
        _listenerTask = RunServerLoopAsync(_cts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Stops the pipe server.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts != null) await _cts.CancelAsync();
        if (_pipeServer != null) await _pipeServer.DisposeAsync();
        if (_listenerTask != null) await Task.WhenAny(_listenerTask, Task.Delay(TimeSpan.FromSeconds(2)));
    }

    private async Task RunServerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
            try
            {
                Logger.Info("Creating Named Pipe instance...");

                // Set up security to allow all users access
                var pipeSecurity = new PipeSecurity();
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    PipeAccessRights.ReadWrite,
                    AccessControlType.Allow));

                _pipeServer = NamedPipeServerStreamAcl.Create(
                    "sts2-cli-mod",
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    4096,
                    4096,
                    pipeSecurity);

                Logger.Info("Waiting for CLI connection...");
                await _pipeServer.WaitForConnectionAsync(ct);
                Logger.Info("CLI connected!");
                await HandleClientAsync(_pipeServer, ct);
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
                if (_pipeServer != null) await _pipeServer.DisposeAsync();
                _pipeServer = null;
            }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        // leaveOpen: true - pipe lifecycle is managed by the caller (RunServerLoopAsync)
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        // ReSharper disable once UseAwaitUsing
        using var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true);
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
                await writer.WriteLineAsync(JsonSerializer.Serialize(new
                    { ok = false, error = "INVALID_REQUEST", message = "Failed to parse request" }, JsonOptions.Default));
                return;
            }

            if (request == null)
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(new
                    { ok = false, error = "INVALID_REQUEST", message = "Failed to parse request" }, JsonOptions.Default));
                return;
            }

            // Process the request
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

    private object ProcessRequest(Request request)
    {
        try
        {
            return request.Cmd.ToLower() switch
            {
                "ping" => new { ok = true, data = new { connected = true } },
                "state" => HandleStateRequest(),
                "play_card" => HandlePlayRequest(request.Args),
                "end_turn" => HandleEndRequest(),
                _ => new { ok = false, error = "UNKNOWN_COMMAND", message = $"Unknown command: {request.Cmd}" }
            };
        }
        catch (Exception ex)
        {
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    private object HandleStateRequest()
    {
        var state = GameStateExtractor.GetState();
        
        if (state.Error != null)
        {
            return new { ok = false, error = "STATE_EXTRACTION_ERROR", message = state.Error };
        }

        // Return DTO directly - simpler and includes all fields
        return new { ok = true, data = state };
    }

    private object HandlePlayRequest(int[]? args)
    {
        if (args == null || args.Length == 0)
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Card index required" };

        var cardIndex = args[0];
        Logger.Info($"Requested to play card at index {cardIndex}");

        // Execute play card action via game's ActionQueue
        return PlayCardHandler.Execute(cardIndex);
    }

    private object HandleEndRequest()
    {
        Logger.Info("Requested to end turn");

        // Execute end turn action via game's ActionQueue
        return EndTurnActionClass.Execute();
    }
}
