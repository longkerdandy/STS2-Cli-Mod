using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using STS2.Cli.Mod.Actions;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Server;

/// <summary>
///     Named Pipe server for communication with the CLI tool.
///     Runs a single-connection loop: create pipe → wait for the client → handle one request → disconnect → repeat.
/// </summary>
public static class PipeServer
{
    private static readonly ModLogger Logger = new("PipeServer");
    private const string PipeName = "sts2-cli-mod";
    private const int PipeBufferSize = 4096;
    private static CancellationTokenSource? _cts;

    /// <summary>
    ///     Starts the pipe server loop in the background.
    ///     Safe to call multiple times; subsequent calls are no-ops.
    /// </summary>
    public static void Start()
    {
        if (_cts != null)
        {
            Logger.Warning("Named Pipe Server already started");
            return;
        }

        _cts = new CancellationTokenSource();
        _ = Task.Run(() => RunServerLoopAsync(_cts.Token));
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

                pipe = CreatePipeServer();

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
        try
        {
            // leaveOpen: true — pipe lifecycle is managed by the caller (RunServerLoopAsync)
            var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            var writer = new StreamWriter(pipe, Encoding.UTF8, leaveOpen: true);
            writer.AutoFlush = true;

            // Short connection mode: handle single request per connection
            var requestJson = await reader.ReadLineAsync(ct);
            if (requestJson == null)
                return;

            Logger.Info($"Received: {requestJson}");

            // Parse request
            Request? request = null;
            try
            {
                request = JsonSerializer.Deserialize<Request>(requestJson, JsonOptions.Default);
            }
            catch (Exception)
            {
                // Deserialization failed — fall through to null check below
            }

            if (request == null)
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(
                    new { ok = false, error = "INVALID_REQUEST", message = "Failed to parse request" },
                    JsonOptions.Default));
                return;
            }

            // Process the request and write the response
            var response = await ProcessRequestAsync(request);
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
    ///     Synchronous commands (state, end_turn) use <see cref="MainThreadExecutor.RunOnMainThread{T}" />.
    ///     Asynchronous commands (play_card) use <see cref="MainThreadExecutor.RunOnMainThreadAsync{T}" />
    ///     to allow awaiting multi-frame game actions.
    /// </summary>
    /// <param name="request">The parsed request object.</param>
    /// <returns>Response object to be serialized as JSON.</returns>
    private static async Task<object> ProcessRequestAsync(Request request)
    {
        try
        {
            var cmd = request.Cmd.ToLower();

            return cmd switch
            {
                // ping does not access game state — handle directly on the pipe thread
                "ping" => new { ok = true, data = new { connected = true } },

                // play_card is async — spans multiple frames waiting for action completion
                "play_card" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    PlayCardHandler.HandleRequestAsync(request)),

                // end_turn is async — waits for the enemy turn to complete before returning results
                "end_turn" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    EndTurnHandler.HandleRequestAsync(request)),

                // use_potion is async — spans multiple frames waiting for action completion
                "use_potion" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    UsePotionHandler.HandleRequestAsync(request)),

                // reward_claim is async — uses type + id + nth for stable identification
                "reward_claim" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ClaimRewardHandler.HandleRequestAsync(request)),

                // reward_choose_card is async — uses reward type + card_id + nth
                "reward_choose_card" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ChooseCardHandler.HandleRequestAsync(request)),

                // reward_skip_card is async — uses reward type + nth
                "reward_skip_card" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ChooseCardHandler.HandleSkipRequestAsync(request)),

                // choose_event is async — ForceClick option button + polling for state change
                "choose_event" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ChooseEventHandler.HandleRequestAsync(request)),

                // advance_dialogue is async — ForceClick dialogue hitbox + polling for Ancient events
                "advance_dialogue" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    AdvanceDialogueHandler.HandleRequestAsync(request)),

                // proceed is async — FakeMerchant path needs to wait for map to open
                "proceed" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ProceedHandler.HandleRequestAsync(request)),

                // potion_select_card is synchronous — runs on main thread and returns immediately
                "potion_select_card" => MainThreadExecutor.RunOnMainThread(() =>
                    PotionSelectCardHandler.HandleRequest(request)),

                // potion_select_skip is synchronous — runs on main thread and returns immediately
                "potion_select_skip" => MainThreadExecutor.RunOnMainThread(() =>
                    PotionSelectCardHandler.HandleSkipRequest(request)),

                // deck_select_card is async — multi-step: select cards, preview, confirm, poll for removal
                "deck_select_card" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    DeckSelectCardHandler.HandleRequestAsync(request)),

                // deck_select_skip is async — click close button, poll for removal
                "deck_select_skip" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    DeckSelectCardHandler.HandleSkipRequestAsync(request)),

                // select_character is synchronous — runs on main thread and returns immediately
                "select_character" => MainThreadExecutor.RunOnMainThread(() =>
                    SelectCharacterHandler.HandleRequest(request)),

                // set_ascension is synchronous — runs on main thread and returns immediately
                "set_ascension" => MainThreadExecutor.RunOnMainThread(() => SetAscensionHandler.HandleRequest(request)),

                // embark is synchronous — runs on main thread and returns immediately
                "embark" => MainThreadExecutor.RunOnMainThread(() => EmbarkHandler.HandleRequest(request)),

                // state is synchronous — single-frame game state extraction on the main thread
                "state" => MainThreadExecutor.RunOnMainThread(() => StateHandler.HandleRequest(request)),

                _ => new { ok = false, error = "UNKNOWN_COMMAND", message = $"Unknown command: {request.Cmd}" }
            };
        }
        catch (Exception ex)
        {
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Creates a <see cref="NamedPipeServerStream" /> with platform-appropriate settings.
    ///     On Windows, sets a permissive ACL so the CLI process (running under the same or different user)
    ///     can connect. On Linux/macOS, uses the standard constructor (Unix Domain Sockets, no ACL needed).
    /// </summary>
    private static NamedPipeServerStream CreatePipeServer()
    {
#pragma warning disable CA1416 // Validate platform compatibility
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow));

            return NamedPipeServerStreamAcl.Create(
                PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                PipeBufferSize,
                PipeBufferSize,
                pipeSecurity);
        }
#pragma warning restore CA1416

        // Linux/macOS: Standard constructor uses Unix Domain Sockets
        // No ACL needed — CLI and game run under the same user
        return new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }
}