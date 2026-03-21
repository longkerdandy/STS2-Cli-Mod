using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles play card action using the game's native ActionQueue.
///     After enqueuing the action, waits for completion and collects execution results
///     (damage dealt, block gained, powers applied) from <c>CombatHistory</c>.
/// </summary>
public static class PlayCardHandler
{
    private static readonly ModLogger Logger = new("PlayCardAction");

    /// <summary>
    ///     Maximum time to wait for a <see cref="PlayCardAction" /> to finish executing.
    ///     Covers animation time for multi-hit attacks and triggered effects.
    /// </summary>
    private const int ActionTimeoutMs = 10000;

    /// <summary>
    ///     Plays a card from the player's hand and returns the execution results.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    public static async Task<object> ExecuteAsync(int cardIndex, int? targetCombatId = null)
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
                    ok = false, error = "NOT_PLAYER_TURN", message = "Not in play phase - cannot act during enemy turn"
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
                return new { ok = false, error = "PLAYER_DEAD", message = "Player is dead - cannot play cards" };

            var hand = player.PlayerCombatState.Hand;
            if (cardIndex < 0 || cardIndex >= hand.Cards.Count)
                return new
                {
                    ok = false, error = "INVALID_CARD_INDEX",
                    message = $"Card index {cardIndex} out of range (hand has {hand.Cards.Count} cards)"
                };

            var card = hand.Cards[cardIndex];

            if (!card.CanPlay(out var reason, out _))
                return new
                {
                    ok = false, error = "CANNOT_PLAY_CARD", message = $"Card '{card.Title}' cannot be played: {reason}"
                };

            // Resolve target
            Creature? target = null;
            if (card.TargetType == TargetType.AnyEnemy)
            {
                if (targetCombatId == null)
                    return new
                    {
                        ok = false, error = "TARGET_REQUIRED",
                        message = "Card requires a target. Provide 'target' with an enemy combat_id."
                    };

                target = ResolveTarget((uint)targetCombatId.Value);
                if (target == null)
                    return new
                    {
                        ok = false, error = "TARGET_NOT_FOUND",
                        message = $"No hittable enemy found with combat_id {targetCombatId}"
                    };
            }
            else if (targetCombatId != null)
            {
                return new
                {
                    ok = false, error = "TARGET_NOT_ALLOWED",
                    message = $"Card '{card.Title}' has target type '{card.TargetType}' and does not accept a target"
                };
            }

            // --- Enqueue action and wait for completion ---

            // Snapshot history count before the action executes
            var historyBefore = CombatManager.Instance.History.Entries.Count();

            var action = new PlayCardAction(card, target);

            // Bridge action lifecycle events to a TaskCompletionSource
            var tcs = new TaskCompletionSource<GameActionState>();
            action.AfterFinished += _ => tcs.TrySetResult(GameActionState.Finished);
            action.BeforeCancelled += _ => tcs.TrySetResult(GameActionState.Canceled);

            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);

            var targetName = target?.Monster?.Title.GetFormattedText();
            var targetMsg = targetName != null ? $" targeting {targetName}" : "";
            Logger.Info($"PlayCardAction enqueued: '{card.Title}'{targetMsg}");

            // Wait for the action to finish or be cancelled (with timeout)
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(ActionTimeoutMs));
            if (completedTask != tcs.Task)
            {
                Logger.Warning("PlayCardAction timed out waiting for completion");
                return new { ok = false, error = "TIMEOUT", message = "Card action did not complete in time" };
            }

            var finalState = tcs.Task.Result;
            if (finalState == GameActionState.Canceled)
            {
                Logger.Info("PlayCardAction was cancelled by the game");
                return new
                {
                    ok = false, error = "ACTION_CANCELLED",
                    message = $"Card '{card.Title}' action was cancelled by the game"
                };
            }

            // --- Collect results from CombatHistory ---

            var results = CombatHistoryBuilder.BuildFromHistory(historyBefore);
            Logger.Info($"PlayCardAction completed with {results.Count} result entries");

            return new
            {
                ok = true,
                data = new
                {
                    card_index = cardIndex,
                    card_id = card.Id.Entry,
                    target = targetCombatId,
                    results
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to play card: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Resolves a target creature by combat ID using the game's native lookup.
    ///     Returns null if the creature is not found, not an enemy, or not hittable
    ///     (dead or blocked by <c>Hook.ShouldAllowHitting</c>).
    /// </summary>
    private static Creature? ResolveTarget(uint combatId)
    {
        try
        {
            var combatState = CombatManager.Instance.DebugOnlyGetState();
            if (combatState == null)
                return null;

            var creature = combatState.GetCreature(combatId);
            if (creature == null)
            {
                Logger.Warning($"No creature found with combat_id {combatId}");
                return null;
            }

            if (creature.Side != CombatSide.Enemy)
            {
                Logger.Warning($"Creature with combat_id {combatId} is not an enemy (side={creature.Side})");
                return null;
            }

            if (!creature.IsHittable)
            {
                Logger.Warning($"Creature with combat_id {combatId} is not hittable");
                return null;
            }

            return creature;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to resolve target with combat_id {combatId}: {ex.Message}");
            return null;
        }
    }
}
