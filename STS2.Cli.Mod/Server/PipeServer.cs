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

                // claim_reward is async — OnSelectWrapper() is async
                "claim_reward" => await HandleClaimRewardRequestAsync(request.Args),

                // choose_card is async — CardPileCmd.Add() is async
                "choose_card" => await HandleChooseCardRequestAsync(request.Args),

                // skip_card is async — opens card screen then clicks skip button
                "skip_card" => await HandleSkipCardRequestAsync(request.Args),

                // proceed runs synchronously on main thread — ForceClick is fire-and-forget
                "proceed" => HandleProceedRequest(),

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
    /// <returns>Response indicating success or failure, with execution results on success.</returns>
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
    /// <returns>Response indicating success or failure, with execution results on success.</returns>
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
    /// <returns>Response indicating success or failure, with execution results on success.</returns>
    private static async Task<object> HandleEndTurnRequestAsync()
    {
        Logger.Info("Requested to end turn");

        return await MainThreadExecutor.RunOnMainThreadAsync(EndTurnHandler.ExecuteAsync);
    }

    /// <summary>
    ///     Handles the 'claim_reward' command asynchronously.
    ///     Validates arguments on the pipe thread, then delegates to <see cref="ClaimRewardHandler.ExecuteAsync" />
    ///     via <see cref="MainThreadExecutor.RunOnMainThreadAsync{T}" /> to claim the reward.
    /// </summary>
    /// <param name="args">Command arguments, expects reward index as the first element.</param>
    /// <returns>Response indicating success or failure.</returns>
    private static async Task<object> HandleClaimRewardRequestAsync(int[]? args)
    {
        if (args == null || args.Length == 0)
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Reward index required" };

        var rewardIndex = args[0];
        Logger.Info($"Requested to claim reward at index {rewardIndex}");

        return await MainThreadExecutor.RunOnMainThreadAsync(() => ClaimRewardHandler.ExecuteAsync(rewardIndex));
    }

    /// <summary>
    ///     Handles the 'choose_card' command asynchronously.
    ///     Validates arguments on the pipe thread, then delegates to <see cref="ChooseCardHandler.ExecuteAsync" />
    ///     via <see cref="MainThreadExecutor.RunOnMainThreadAsync{T}" /> to pick a card from a card reward.
    /// </summary>
    /// <param name="args">Command arguments, expects [reward_index, card_index].</param>
    /// <returns>Response indicating success or failure.</returns>
    private static async Task<object> HandleChooseCardRequestAsync(int[]? args)
    {
        if (args == null || args.Length < 2)
            return new
            {
                ok = false, error = "MISSING_ARGUMENT",
                message = "Reward index and card index required (e.g., choose_card <reward_index> <card_index>)"
            };

        var rewardIndex = args[0];
        var cardIndex = args[1];
        Logger.Info($"Requested to choose card at reward index {rewardIndex}, card index {cardIndex}");

        return await MainThreadExecutor.RunOnMainThreadAsync(
            () => ChooseCardHandler.ExecuteAsync(rewardIndex, cardIndex));
    }

    /// <summary>
    ///     Handles the 'skip_card' command asynchronously.
    ///     Validates arguments on the pipe thread, then delegates to <see cref="ChooseCardHandler.ExecuteSkipAsync" />
    ///     via <see cref="MainThreadExecutor.RunOnMainThreadAsync{T}" /> to open the card screen and click skip.
    /// </summary>
    /// <param name="args">Command arguments, expects reward index as the first element.</param>
    /// <returns>Response indicating success or failure.</returns>
    private static async Task<object> HandleSkipCardRequestAsync(int[]? args)
    {
        if (args == null || args.Length == 0)
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Reward index required" };

        var rewardIndex = args[0];
        Logger.Info($"Requested to skip card reward at index {rewardIndex}");

        return await MainThreadExecutor.RunOnMainThreadAsync(
            () => ChooseCardHandler.ExecuteSkipAsync(rewardIndex));
    }

    /// <summary>
    ///     Handles the 'proceed' command synchronously.
    ///     Delegates to <see cref="ProceedHandler.Execute" /> via
    ///     <see cref="MainThreadExecutor.RunOnMainThread{T}" /> to click the proceed button.
    /// </summary>
    /// <returns>Response indicating success or failure.</returns>
    private static object HandleProceedRequest()
    {
        Logger.Info("Requested to proceed from reward screen");

        return MainThreadExecutor.RunOnMainThread(ProceedHandler.Execute);
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