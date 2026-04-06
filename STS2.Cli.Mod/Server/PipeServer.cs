using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using STS2.Cli.Mod.Actions;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.State;
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
            var cmd = request.Cmd.ToLowerInvariant();

            return cmd switch
            {
                // ping does not access game state — handle directly on the pipe thread
                "ping" => new { ok = true, data = new { connected = true } },

                // --- Pre-run commands ---
                "select_character" => MainThreadExecutor.RunOnMainThread(() =>
                    SelectCharacterHandler.Execute(request)),

                "set_ascension" => MainThreadExecutor.RunOnMainThread(() => SetAscensionHandler.Execute(request)),

                "embark" => MainThreadExecutor.RunOnMainThread(() => EmbarkHandler.Execute()),

                "tri_select_card" => MainThreadExecutor.RunOnMainThread(() =>
                    TriSelectCardHandler.Execute(request)),

                "tri_select_skip" => MainThreadExecutor.RunOnMainThread(() =>
                    TriSelectCardHandler.ExecuteSkip(request)),

                "choose_game_mode" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ChooseGameModeHandler.ExecuteAsync(request)),

                // --- Combat and its sub-states ---
                "hand_select_card" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    HandSelectCardHandler.ExecuteAsync(request)),

                "hand_confirm_selection" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    HandSelectCardHandler.ExecuteConfirmAsync()),

                "grid_select_card" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    GridSelectCardHandler.ExecuteAsync(request)),

                "grid_select_skip" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    GridSelectCardHandler.ExecuteSkipAsync()),

                "bundle_select" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    BundleSelectHandler.HandleSelectAsync(request)),

                "bundle_confirm" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    BundleSelectHandler.HandleConfirmAsync()),

                "bundle_cancel" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    BundleSelectHandler.HandleCancelAsync()),

                "crystal_set_tool" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    CrystalSphereHandler.HandleSetToolAsync(request)),

                "crystal_click_cell" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    CrystalSphereHandler.HandleClickCellAsync(request)),

                "crystal_proceed" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    CrystalSphereHandler.HandleProceedAsync()),

                "play_card" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    PlayCardHandler.ExecuteAsync(request)),

                "end_turn" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    EndTurnHandler.ExecuteAsync(request)),

                "use_potion" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    UsePotionHandler.ExecuteAsync(request)),

                // --- Map ---
                "choose_map_node" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ChooseMapNodeHandler.ExecuteAsync(request)),

                // --- Overlay stack ---
                "reward_claim" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    RewardClaimHandler.ExecuteAsync(request)),

                "reward_choose_card" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    RewardCardHandler.ExecuteAsync(request)),

                "reward_skip_card" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    RewardCardHandler.ExecuteSkipAsync(request)),

                "proceed" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ProceedHandler.ExecuteAsync()),

                "relic_select" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    RelicSelectHandler.ExecuteAsync(request)),

                "relic_skip" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    RelicSelectHandler.ExecuteSkipAsync()),

                "return_to_menu" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ReturnToMenuHandler.ExecuteAsync()),

                "continue_run" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ContinueRunHandler.ExecuteAsync()),

                "new_run" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    NewRunHandler.ExecuteAsync()),

                "abandon_run" => MainThreadExecutor.RunOnMainThread(() =>
                    AbandonRunHandler.Execute()),

                // --- Room-based ---
                "choose_event" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ChooseEventHandler.ExecuteAsync(request)),

                "advance_dialogue" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    AdvanceDialogueHandler.ExecuteAsync(request)),

                "choose_rest_option" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ChooseRestOptionHandler.ExecuteAsync(request)),

                "open_chest" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    OpenChestHandler.ExecuteAsync()),

                "pick_relic" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    PickRelicHandler.ExecuteAsync(request)),

                "shop_buy_card" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ShopBuyCardHandler.ExecuteAsync(request)),

                "shop_buy_relic" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ShopBuyRelicHandler.ExecuteAsync(request)),

                "shop_buy_potion" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ShopBuyPotionHandler.ExecuteAsync(request)),

                "shop_remove_card" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
                    ShopRemoveCardHandler.ExecuteAsync()),

                // --- State query ---
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