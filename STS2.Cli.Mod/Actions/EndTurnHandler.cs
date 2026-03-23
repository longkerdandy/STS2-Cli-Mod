using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Rooms;
using STS2.Cli.Mod.Models.Message;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the end turn action using the game's native PlayerCmd.
///     After ending the turn, waits for the enemy turn to complete and collects
///     execution results (damage, block, powers) from <c>CombatHistory</c>.
/// </summary>
public static class EndTurnHandler
{
    private static readonly ModLogger Logger = new("EndTurnAction");

    /// <summary>
    ///     Maximum time to wait for the enemy turn to complete and the next player turn to start.
    ///     Covers all enemy animations, triggered effects, and transitions.
    /// </summary>
    private const int TurnTimeoutMs = 30000;

    /// <summary>
    ///     Handles the end_turn request.
    /// </summary>
    public static async Task<object> HandleRequestAsync(Request request)
    {
        Logger.Info("Requested to end turn");
        return await ExecuteAsync();
    }

    /// <summary>
    ///     Ends the player's turn, waits for the enemy turn to complete, and returns execution results.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    public static async Task<object> ExecuteAsync()
    {
        try
        {
            // --- Validation (synchronous, single frame) ---

            var combatError = ActionUtils.ValidateCombatReady();
            if (combatError != null) return combatError;

            // Get player
            var player = ActionUtils.GetLocalPlayer();
            if (player == null)
                return new { ok = false, error = "NO_PLAYER", message = "Player not found" };

            // --- Snapshot history and end turn ---

            var historyBefore = CombatManager.Instance.History.Entries.Count();

            // Bridge TurnStarted and CombatEnded events to a TaskCompletionSource
            var tcs = new TaskCompletionSource<string>();

            void OnTurnStarted(CombatState _)
            {
                // Only resolve when it's the player's play phase again
                if (CombatManager.Instance.IsPlayPhase)
                    tcs.TrySetResult("turn_started");
            }

            void OnCombatEnded(CombatRoom _)
            {
                tcs.TrySetResult("combat_ended");
            }

            CombatManager.Instance.TurnStarted += OnTurnStarted;
            CombatManager.Instance.CombatEnded += OnCombatEnded;

            try
            {
                // Use PlayerCmd.EndTurn (same as game UI)
                // canBackOut: false means the AI cannot undo the end-turn decision
                PlayerCmd.EndTurn(player, canBackOut: false);
                Logger.Info("EndTurn action executed via PlayerCmd, waiting for enemy turn...");

                // Wait for the next player turn or combat end (with timeout)
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TurnTimeoutMs));
                if (completedTask != tcs.Task)
                {
                    Logger.Warning("EndTurn timed out waiting for enemy turn to complete");
                    return new { ok = false, error = "TIMEOUT", message = "Enemy turn did not complete in time" };
                }

                var reason = tcs.Task.Result;
                Logger.Info($"EndTurn resolved: {reason}");

                // --- Collect results from CombatHistory ---

                var results = CombatHistoryBuilder.BuildFromHistory(historyBefore);
                Logger.Info($"EndTurn completed with {results.Count} result entries");

                return new
                {
                    ok = true,
                    data = new
                    {
                        action = "END_TURN",
                        reason,
                        results
                    }
                };
            }
            finally
            {
                // Always unsubscribe to avoid leaking event handlers
                CombatManager.Instance.TurnStarted -= OnTurnStarted;
                CombatManager.Instance.CombatEnded -= OnCombatEnded;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to end turn: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }
}
