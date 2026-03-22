using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
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
    private static readonly ModLogger Logger = new("UsePotionHandler");

    /// <summary>
    ///     Maximum time to wait for a <see cref="UsePotionAction" /> to finish executing.
    ///     Covers potion throw animation and triggered effects.
    /// </summary>
    private const int ActionTimeoutMs = 10000;

    /// <summary>
    ///     Uses a potion from the player's potion belt by ID and returns the execution results.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    /// <param name="potionId">Potion ID to use (e.g., "FIRE_POTION").</param>
    /// <param name="nth">N-th occurrence when multiple copies exist (0-based).</param>
    /// <param name="targetCombatId">Optional target combat ID for targeted potions.</param>
    public static async Task<object> ExecuteAsync(string potionId, int nth = 0, int? targetCombatId = null)
    {
        try
        {
            // --- Validation (synchronous, single frame) ---

            var combatError = ActionUtils.ValidateCombatReady();
            if (combatError != null) return combatError;

            var player = ActionUtils.GetLocalPlayer();
            if (player?.PlayerCombatState == null)
                return new { ok = false, error = "NO_PLAYER", message = "Player not found or not in combat" };

            if (!player.Creature.IsAlive)
                return new { ok = false, error = "PLAYER_DEAD", message = "Player is dead - cannot use potions" };

            // Find potion by ID
            var (potion, slot, findError) = FindPotionById(player, potionId, nth);
            if (findError != null)
                return findError;

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

            var targetName = target?.Monster?.Title.GetFormattedText();
            var targetMsg = targetName != null ? $" targeting {targetName}" : "";
            Logger.Info($"UsePotionAction enqueued: '{potion.Title}' (slot {slot}){targetMsg}");

            var finalState = await ActionUtils.EnqueueAndAwaitAsync(action, ActionTimeoutMs);
            if (finalState == null)
            {
                Logger.Warning("UsePotionAction timed out waiting for completion");
                return new { ok = false, error = "TIMEOUT", message = "Potion action did not complete in time" };
            }

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

    /// <summary>
    ///     Finds a potion in the player's potion belt by ID and nth occurrence.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="potionId">Potion ID to find.</param>
    /// <param name="nth">N-th occurrence (0-based).</param>
    /// <returns>Tuple of (potion, slot, error). If error is not null, potion and slot are invalid.</returns>
    private static (PotionModel Potion, int Slot, object? Error) FindPotionById(Player player, string potionId, int nth)
    {
        // Collect all non-null potions with their slots
        var matchingPotions = new List<(PotionModel Potion, int Slot)>();
        for (var slot = 0; slot < player.MaxPotionCount; slot++)
        {
            var potion = player.GetPotionAtSlotIndex(slot);
            if (potion != null && potion.Id.Entry.Equals(potionId, StringComparison.OrdinalIgnoreCase))
            {
                matchingPotions.Add((potion, slot));
            }
        }

        if (matchingPotions.Count == 0)
        {
            // Build list of available potion IDs for error message
            var availablePotions = new List<string>();
            for (var slot = 0; slot < player.MaxPotionCount; slot++)
            {
                var potion = player.GetPotionAtSlotIndex(slot);
                if (potion != null)
                    availablePotions.Add(potion.Id.Entry);
            }
            var availableStr = availablePotions.Count > 0 ? string.Join(", ", availablePotions) : "(none)";
            Logger.Warning($"Potion '{potionId}' not found. Available: {availableStr}");

            return (null!, 0, new
            {
                ok = false,
                error = "POTION_NOT_FOUND",
                message = $"Potion '{potionId}' not found. Available potions: {availableStr}"
            });
        }

        if (nth < 0 || nth >= matchingPotions.Count)
        {
            Logger.Warning($"Potion '{potionId}' has {matchingPotions.Count} copies, but nth={nth} was requested");
            return (null!, 0, new
            {
                ok = false,
                error = "INVALID_POTION_SLOT",
                message = $"Potion '{potionId}' has {matchingPotions.Count} copies. Use nth from 0 to {matchingPotions.Count - 1}."
            });
        }

        var selected = matchingPotions[nth];
        Logger.Info($"Found potion '{potionId}' at slot {selected.Slot} (nth={nth}, total matches={matchingPotions.Count})");

        return (selected.Potion, selected.Slot, null);
    }
}
