using System.IO.Pipes;
using System.Runtime.InteropServices;
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
///     Runs a single-connection loop: create pipe → wait for the client → handle one request → disconnect → repeat.
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
                "play_card" => await HandlePlayCardRequestAsync(request.Id, request.Nth, request.Target),

                // end_turn is async — waits for the enemy turn to complete before returning results
                "end_turn" => await HandleEndTurnRequestAsync(),

                // use_potion is async — spans multiple frames waiting for action completion
                "use_potion" => await HandleUsePotionRequestAsync(request.Id, request.Nth, request.Target),

                // reward_claim is async — uses type + id + nth for stable identification
                "reward_claim" => await HandleClaimRewardRequestAsync(request.RewardType, request.Id, request.Nth),

                // reward_choose_card is async — uses reward type + card_id + nth
                "reward_choose_card" => await HandleChooseCardRequestAsync(request.RewardType, request.CardId, request.Nth),

                // reward_skip_card is async — uses reward type + nth
                "reward_skip_card" => await HandleSkipCardRequestAsync(request.RewardType, request.Nth),

                // choose_event is async — ForceClick option button + polling for state change
                "choose_event" => await HandleChooseEventRequestAsync(request.Args),

                // advance_dialogue is async — ForceClick dialogue hitbox + polling for Ancient events
                "advance_dialogue" => await HandleAdvanceDialogueRequestAsync(request.Args),

                // reward_proceed runs synchronously on the main thread — ForceClick is fire-and-forget
                "reward_proceed" => HandleProceedRequest(),

                // potion_select_card is synchronous — runs on main thread and returns immediately
                "potion_select_card" => HandlePotionSelectCardRequest(request.CardIds, request.NthValues),

                // potion_select_skip is synchronous — runs on main thread and returns immediately
                "potion_select_skip" => HandlePotionSelectSkipRequest(),

                // Synchronous commands — single-frame game state access on the main thread
                _ => MainThreadExecutor.RunOnMainThread(() => cmd switch
                {
                    "state" => HandleStateRequest(),
                    _ => new { ok = false, error = "UNKNOWN_COMMAND", message = $"Unknown command: {request.Cmd}" }
                })
            };
        }
        catch (Exception ex)
        {
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Handles the 'state' command by extracting the current game state.
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
    ///     Handles the 'play_card' command asynchronously.
    ///     Validates arguments on the pipe thread, then delegates to <see cref="PlayCardHandler.ExecuteAsync" />
    ///     via <see cref="MainThreadExecutor.RunOnMainThreadAsync{T}" /> to await action completion.
    /// </summary>
    /// <param name="cardId">Card ID to play (e.g., "STRIKE_IRONCLAD").</param>
    /// <param name="nth">Optional N-th occurrence when multiple copies exist (0-based).</param>
    /// <param name="target">Optional target combat ID for targeted cards.</param>
    /// <returns>Response indicating success or failure, with execution results when success.</returns>
    private static async Task<object> HandlePlayCardRequestAsync(string? cardId, int? nth, int? target)
    {
        if (string.IsNullOrEmpty(cardId))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Card ID required (e.g., STRIKE_IRONCLAD)" };

        var nthValue = nth ?? 0;
        Logger.Info($"Requested to play card {cardId}, nth={nthValue}, target={target?.ToString() ?? "none"}");

        return await MainThreadExecutor.RunOnMainThreadAsync(() => PlayCardHandler.ExecuteAsync(cardId, nthValue, target));
    }

    /// <summary>
    ///     Handles the 'use_potion' command asynchronously.
    ///     Validates arguments on the pipe thread, then delegates to <see cref="UsePotionHandler.ExecuteAsync" />
    ///     via <see cref="MainThreadExecutor.RunOnMainThreadAsync{T}" /> to await action completion.
    /// </summary>
    /// <param name="potionId">Potion ID to use (e.g., "FIRE_POTION").</param>
    /// <param name="nth">Optional N-th occurrence when multiple copies exist (0-based).</param>
    /// <param name="target">Optional target combat ID for targeted potions.</param>
    /// <returns>Response indicating success or failure, with execution results when success.</returns>
    private static async Task<object> HandleUsePotionRequestAsync(string? potionId, int? nth, int? target)
    {
        if (string.IsNullOrEmpty(potionId))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Potion ID required (e.g., FIRE_POTION)" };

        var nthValue = nth ?? 0;
        Logger.Info($"Requested to use potion {potionId}, nth={nthValue}, target={target?.ToString() ?? "none"}");

        return await MainThreadExecutor.RunOnMainThreadAsync(() => UsePotionHandler.ExecuteAsync(potionId, nthValue, target));
    }

    /// <summary>
    ///     Handles the 'end_turn' command asynchronously.
    ///     Delegates to <see cref="EndTurnHandler.ExecuteAsync" /> via
    ///     <see cref="MainThreadExecutor.RunOnMainThreadAsync{T}" /> to await
    ///     enemy turn completion and collect execution results.
    /// </summary>
    /// <returns>Response indicating success or failure, with execution results when success.</returns>
    private static async Task<object> HandleEndTurnRequestAsync()
    {
        Logger.Info("Requested to end turn");

        return await MainThreadExecutor.RunOnMainThreadAsync(EndTurnHandler.ExecuteAsync);
    }

    /// <summary>
    ///     Handles the 'claim_reward' command asynchronously.
    ///     Uses reward type + item ID + nth for stable identification.
    /// </summary>
    /// <param name="rewardType">Reward type (gold, potion, relic, special_card).</param>
    /// <param name="itemId">Item ID for potion/relic/special_card (optional for gold).</param>
    /// <param name="nth">N-th occurrence when multiple rewards of same type exist.</param>
    /// <returns>Response indicating success or failure.</returns>
    private static async Task<object> HandleClaimRewardRequestAsync(string? rewardType, string? itemId, int? nth)
    {
        if (string.IsNullOrEmpty(rewardType))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Reward type required (--type)" };

        var nthValue = nth ?? 0;
        Logger.Info($"Requested to claim reward: type={rewardType}, id={itemId ?? "null"}, nth={nthValue}");

        return await MainThreadExecutor.RunOnMainThreadAsync(() =>
            ClaimRewardHandler.ExecuteAsync(rewardType, itemId, nthValue));
    }

    /// <summary>
    ///     Handles the 'choose_card' command asynchronously.
    ///     Uses reward type + card ID + nth for stable identification.
    /// </summary>
    /// <param name="rewardType">Reward type (only 'card' is supported).</param>
    /// <param name="cardId">Card ID to select.</param>
    /// <param name="nth">N-th card reward when multiple exist.</param>
    /// <returns>Response indicating success or failure.</returns>
    private static async Task<object> HandleChooseCardRequestAsync(string? rewardType, string? cardId, int? nth)
    {
        if (string.IsNullOrEmpty(rewardType))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Reward type required (--type)" };

        if (string.IsNullOrEmpty(cardId))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Card ID required (--card_id)" };

        if (rewardType != "card")
            return new { ok = false, error = "INVALID_REWARD_TYPE", message = "choose_card only supports --type card" };

        var nthValue = nth ?? 0;
        Logger.Info($"Requested to choose card: type={rewardType}, card_id={cardId}, nth={nthValue}");

        return await MainThreadExecutor.RunOnMainThreadAsync(
            () => ChooseCardHandler.ExecuteAsync(rewardType, cardId, nthValue));
    }

    /// <summary>
    ///     Handles the 'skip_card' command asynchronously.
    ///     Validates arguments on the pipe thread, then delegates to <see cref="ChooseCardHandler.ExecuteSkipAsync" />
    ///     via <see cref="MainThreadExecutor.RunOnMainThreadAsync{T}" /> to open the card screen and click skip.
    /// </summary>
    /// <param name="rewardType">Reward type (only 'card' is supported).</param>
    /// <param name="nth">N-th card reward when multiple exist.</param>
    /// <returns>Response indicating success or failure.</returns>
    private static async Task<object> HandleSkipCardRequestAsync(string? rewardType, int? nth)
    {
        if (string.IsNullOrEmpty(rewardType))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Reward type required (--type)" };

        if (rewardType != "card")
            return new { ok = false, error = "INVALID_REWARD_TYPE", message = "skip_card only supports --type card" };

        var nthValue = nth ?? 0;
        Logger.Info($"Requested to skip card reward: type={rewardType}, nth={nthValue}");

        return await MainThreadExecutor.RunOnMainThreadAsync(
            () => ChooseCardHandler.ExecuteSkipAsync(rewardType, nthValue));
    }

    /// <summary>
    ///     Handles the 'proceed' command synchronously.
    ///     Delegates to <see cref="ProceedHandler.Execute" /> via
    ///     <see cref="MainThreadExecutor.RunOnMainThread{T}" /> to click the "proceed" button.
    /// </summary>
    /// <returns>Response indicating success or failure.</returns>
    private static object HandleProceedRequest()
    {
        Logger.Info("Requested to proceed from reward screen");

        return MainThreadExecutor.RunOnMainThread(ProceedHandler.Execute);
    }

    /// <summary>
    ///     Handles the 'choose_event' command asynchronously.
    ///     Validates arguments on the pipe thread, then delegates to <see cref="ChooseEventHandler.ExecuteAsync" />
    ///     via <see cref="MainThreadExecutor.RunOnMainThreadAsync{T}" /> to choose an event option.
    /// </summary>
    /// <param name="args">Command arguments, expects option index as the first element.</param>
    /// <returns>Response indicating success or failure with updated event state.</returns>
    private static async Task<object> HandleChooseEventRequestAsync(int[]? args)
    {
        if (args == null || args.Length == 0)
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Option index required" };

        var optionIndex = args[0];
        Logger.Info($"Requested to choose event option at index {optionIndex}");

        return await MainThreadExecutor.RunOnMainThreadAsync(
            () => ChooseEventHandler.ExecuteAsync(optionIndex));
    }

    /// <summary>
    ///     Handles the 'advance_dialogue' command asynchronously.
    ///     Validates arguments on the pipe thread, then delegates to <see cref="AdvanceDialogueHandler.ExecuteAsync" />
    ///     via <see cref="MainThreadExecutor.RunOnMainThreadAsync{T}" /> to advance Ancient event dialogue.
    /// </summary>
    /// <param name="args">Command arguments, expects [1] for auto mode as first element (optional).</param>
    /// <returns>Response indicating success or failure with updated event state.</returns>
    private static async Task<object> HandleAdvanceDialogueRequestAsync(int[]? args)
    {
        // args[0] = 1 for auto mode, 0 or not present for single advance
        var auto = args is { Length: > 0 } && args[0] == 1;
        Logger.Info($"Requested to advance dialogue (auto={auto})");

        return await MainThreadExecutor.RunOnMainThreadAsync(
            () => AdvanceDialogueHandler.ExecuteAsync(auto));
    }

    /// <summary>
    ///     Handles the 'potion_select_card' command synchronously.
    ///     Selects cards from a potion-opened selection screen by card ID.
    /// </summary>
    /// <param name="cardIds">Array of card IDs to select.</param>
    /// <param name="nthValues">Optional nth values for each card ID.</param>
    /// <returns>Response indicating success or failure.</returns>
    private static object HandlePotionSelectCardRequest(string[]? cardIds, int[]? nthValues)
    {
        if (cardIds == null || cardIds.Length == 0)
        {
            Logger.Warning("potion_select_card requested with no card IDs");
            return new { ok = false, error = "MISSING_ARGUMENT", message = "At least one card ID is required" };
        }

        Logger.Info($"Requested to select {cardIds.Length} card(s) from potion selection screen");
        return MainThreadExecutor.RunOnMainThread(() => PotionSelectCardHandler.Execute(cardIds, nthValues));
    }

    /// <summary>
    ///     Handles the 'potion_select_skip' command synchronously.
    ///     Skips the current potion card selection if allowed.
    /// </summary>
    /// <returns>Response indicating success or failure.</returns>
    private static object HandlePotionSelectSkipRequest()
    {
        Logger.Info("Requested to skip potion card selection");
        return MainThreadExecutor.RunOnMainThread(() => PotionSelectCardHandler.ExecuteSkip());
    }

    /// <summary>
    ///     Creates a <see cref="NamedPipeServerStream" /> with platform-appropriate settings.
    ///     On Windows, sets a permissive ACL so the CLI process (running under the same or different user)
    ///     can connect. On Linux/macOS, uses the standard constructor (Unix Domain Sockets, no ACL needed).
    /// </summary>
    private static NamedPipeServerStream CreatePipeServer()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
#pragma warning disable CA1416 // Validate platform compatibility
            var pipeSecurity = new PipeSecurity();
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                PipeAccessRights.ReadWrite,
                AccessControlType.Allow));

            return NamedPipeServerStreamAcl.Create(
                "sts2-cli-mod",
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                4096,
                4096,
                pipeSecurity);
#pragma warning restore CA1416
        }

        // Linux/macOS: Standard constructor uses Unix Domain Sockets
        // No ACL needed — CLI and game run under the same user
        return new NamedPipeServerStream(
            "sts2-cli-mod",
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }
}