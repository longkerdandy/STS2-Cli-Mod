using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the use potion action by constructing a <see cref="UsePotionAction" />
///     and enqueuing it through the game's native ActionQueue.
///     After enqueuing, waits for completion and collects execution results
///     (damage dealt, block gained, powers applied) from <c>CombatHistory</c>.
/// </summary>
public static class UsePotionHandler
{
    private static readonly ModLogger Logger = new("UsePotionAction");

    /// <summary>
    ///     Maximum time to wait for a <see cref="UsePotionAction" /> to finish executing.
    ///     Covers potion throw animation and triggered effects.
    /// </summary>
    private const int ActionTimeoutMs = 10000;

    /// <summary>
    ///     Uses a potion from the player's potion belt and returns the execution results.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    /// <param name="slot">The 0-based potion slot index.</param>
    /// <param name="targetCombatId">Optional target combat ID for targeted potions.</param>
    public static async Task<object> ExecuteAsync(int slot, int? targetCombatId = null)
    {
        try
        {
            // --- Validation (synchronous, single frame) ---

            if (!CombatManager.Instance.IsInProgress)
                return new { ok = false, error = "NOT_IN_COMBAT", message = "Not currently in combat" };

            if (CombatManager.Instance.IsOverOrEnding)
                return new { ok = false, error = "COMBAT_ENDING", message = "Combat is over or ending" };

            if (!CombatManager.Instance.IsPlayPhase)
                return new
                {
                    ok = false, error = "NOT_PLAYER_TURN",
                    message = "Not in play phase - cannot use potions during enemy turn"
                };

            if (CombatManager.Instance.PlayerActionsDisabled)
                return new
                {
                    ok = false, error = "ACTIONS_DISABLED", message = "Player actions are currently disabled"
                };

            var player = ActionUtils.GetLocalPlayer();
            if (player?.PlayerCombatState == null)
                return new { ok = false, error = "NO_PLAYER", message = "Player not found or not in combat" };

            if (!player.Creature.IsAlive)
                return new { ok = false, error = "PLAYER_DEAD", message = "Player is dead - cannot use potions" };

            // Validate slot index
            if (slot < 0 || slot >= player.MaxPotionCount)
                return new
                {
                    ok = false, error = "INVALID_POTION_SLOT",
                    message = $"Potion slot {slot} out of range (max {player.MaxPotionCount} slots)"
                };

            var potion = player.GetPotionAtSlotIndex(slot);
            if (potion == null)
                return new
                {
                    ok = false, error = "EMPTY_POTION_SLOT",
                    message = $"Potion slot {slot} is empty"
                };

            if (potion.IsQueued)
                return new
                {
                    ok = false, error = "POTION_ALREADY_QUEUED",
                    message = $"Potion '{potion.Title}' is already queued for use"
                };

            if (!potion.PassesCustomUsabilityCheck)
                return new
                {
                    ok = false, error = "POTION_NOT_USABLE",
                    message = $"Potion '{potion.Title}' cannot be used right now"
                };

            // Resolve target based on potion's TargetType
            Creature? target = null;
            if (potion.TargetType == TargetType.AnyEnemy)
            {
                if (targetCombatId == null)
                    return new
                    {
                        ok = false, error = "TARGET_REQUIRED",
                        message = "Potion requires a target. Provide 'target' with an enemy combat_id."
                    };

                target = ActionUtils.ResolveEnemyTarget((uint)targetCombatId.Value);
                if (target == null)
                    return new
                    {
                        ok = false, error = "TARGET_NOT_FOUND",
                        message = $"No hittable enemy found with combat_id {targetCombatId}"
                    };
            }
            else if (potion.TargetType is TargetType.Self or TargetType.AnyPlayer)
            {
                // Self-targeting or any player: target is the player's creature
                target = player.Creature;

                if (targetCombatId != null)
                    return new
                    {
                        ok = false, error = "TARGET_NOT_ALLOWED",
                        message =
                            $"Potion '{potion.Title}' has target type '{potion.TargetType}' and does not accept a target"
                    };
            }
            else
            {
                // AllEnemies, AllAllies, None, etc. — no target needed
                if (targetCombatId != null)
                    return new
                    {
                        ok = false, error = "TARGET_NOT_ALLOWED",
                        message =
                            $"Potion '{potion.Title}' has target type '{potion.TargetType}' and does not accept a target"
                    };
            }

            // --- Enqueue action and wait for completion ---

            // Snapshot history count before the action executes
            var historyBefore = CombatManager.Instance.History.Entries.Count();

            // Manually construct the UsePotionAction instead of calling PotionModel.EnqueueManualUse(),
            // so we get the action reference for subscribing to AfterFinished/BeforeCancelled.
            // Note: We skip setting PotionModel.IsQueued (private setter) — it's only used
            // by the UI popup to prevent double-clicking; our CLI validation already guards this.
            var action = new UsePotionAction(potion, target, CombatManager.Instance.IsInProgress);

            // Bridge action lifecycle events to a TaskCompletionSource
            var tcs = new TaskCompletionSource<GameActionState>();
            action.AfterFinished += _ => tcs.TrySetResult(GameActionState.Finished);
            action.BeforeCancelled += _ => tcs.TrySetResult(GameActionState.Canceled);

            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);

            var targetName = target?.Monster?.Title.GetFormattedText();
            var targetMsg = targetName != null ? $" targeting {targetName}" : "";
            Logger.Info($"UsePotionAction enqueued: '{potion.Title}' (slot {slot}){targetMsg}");

            // Wait for the action to finish or be cancelled (with timeout)
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(ActionTimeoutMs));
            if (completedTask != tcs.Task)
            {
                Logger.Warning("UsePotionAction timed out waiting for completion");
                return new { ok = false, error = "TIMEOUT", message = "Potion action did not complete in time" };
            }

            var finalState = tcs.Task.Result;
            if (finalState == GameActionState.Canceled)
            {
                Logger.Info("UsePotionAction was cancelled by the game");
                return new
                {
                    ok = false, error = "ACTION_CANCELLED",
                    message = $"Potion '{potion.Title}' action was cancelled by the game"
                };
            }

            // --- Collect results from CombatHistory ---

            var results = CombatHistoryBuilder.BuildFromHistory(historyBefore);
            Logger.Info($"UsePotionAction completed with {results.Count} result entries");

            return new
            {
                ok = true,
                data = new
                {
                    slot,
                    potion_id = potion.Id.Entry,
                    target = targetCombatId,
                    results
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to use potion: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }
}
