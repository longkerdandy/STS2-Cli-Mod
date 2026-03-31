using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions.Utils;

/// <summary>
///     Common utilities for action handlers.
/// </summary>
public static class ActionUtils
{
    // ── Shared timing constants ───────────────────────────────────────

    /// <summary>Default polling interval for UI state changes (ms).</summary>
    public const int DefaultPollIntervalMs = 100;

    /// <summary>Delay between consecutive UI clicks in multi-select scenarios (ms).</summary>
    public const int ClickDelayMs = 100;

    /// <summary>Delay after a ForceClick before polling for state changes (ms).</summary>
    public const int PostClickDelayMs = 200;

    /// <summary>Delay for a preview or animation to appear after interaction (ms).</summary>
    public const int PreviewAppearDelayMs = 300;

    /// <summary>Delay for card reward buttons to become enabled after the screen opens (ms).</summary>
    public const int CardEnableDelayMs = 500;

    /// <summary>Short timeout for quick UI transitions like dialogue advance (ms).</summary>
    public const int ShortTimeoutMs = 3000;

    /// <summary>Standard timeout for UI completion and state changes (ms).</summary>
    public const int UiTimeoutMs = 5000;

    /// <summary>Timeout for game action execution like card play or potion use (ms).</summary>
    public const int ActionTimeoutMs = 10000;

    /// <summary>Timeout for a full enemy turn to complete (ms).</summary>
    public const int TurnTimeoutMs = 30000;

    // ── Fields ────────────────────────────────────────────────────────

    private static readonly ModLogger Logger = new("ActionUtils");

    /// <summary>
    ///     Gets the local player from the current combat state.
    ///     In single player mode, returns the first player.
    ///     Requires an active combat (caller must validate <see cref="CombatManager.IsInProgress" /> first).
    /// </summary>
    public static Player? GetLocalPlayer()
    {
        try
        {
            var combatState = CombatManager.Instance.DebugOnlyGetState();
            var players = combatState?.Players;
            return players?.Count > 0 ? players[0] : null;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get local player: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Validates that combat is active and the player can act.
    ///     Checks: combat in progress, not ending, play phase, actions not disabled.
    ///     Returns an error response object if any check fails, or <c>null</c> if all pass.
    /// </summary>
    /// <returns>An anonymous error object, or <c>null</c> if validation passed.</returns>
    public static object? ValidateCombatReady()
    {
        if (!CombatManager.Instance.IsInProgress)
            return new { ok = false, error = "NOT_IN_COMBAT", message = "Not currently in combat" };

        if (CombatManager.Instance.IsOverOrEnding)
            return new { ok = false, error = "COMBAT_ENDING", message = "Combat is over or ending" };

        if (!CombatManager.Instance.IsPlayPhase)
            return new { ok = false, error = "NOT_PLAYER_TURN", message = "Not in play phase" };

        if (CombatManager.Instance.PlayerActionsDisabled)
            return new { ok = false, error = "ACTIONS_DISABLED", message = "Player actions are currently disabled" };

        return null;
    }

    /// <summary>
    ///     Resolves a target creature by combat ID using the game's native lookup.
    ///     Returns null if the creature is not found, not an enemy, or not hittable
    ///     (dead or blocked by <c>Hook.ShouldAllowHitting</c>).
    /// </summary>
    /// <param name="combatId">The combat ID of the target enemy.</param>
    /// <returns>The resolved <see cref="Creature" />, or null if invalid.</returns>
    public static Creature? ResolveEnemyTarget(uint combatId)
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

    /// <summary>
    ///     Resolves an ally target (player or pet) by combat ID.
    ///     Returns null if the creature is not found or not a valid ally (not player or pet).
    /// </summary>
    /// <param name="player">The local player.</param>
    /// <param name="combatId">The combat ID of the target ally.</param>
    /// <returns>The resolved <see cref="Creature" />, or null if invalid.</returns>
    public static Creature? ResolveAllyTarget(Player player, uint combatId)
    {
        try
        {
            // Check if it's the player
            if (player.Creature.CombatId == combatId)
                return player.Creature;

            // Check if it's a pet
            var combatState = CombatManager.Instance.DebugOnlyGetState();
            if (combatState == null)
                return null;

            var creature = combatState.GetCreature(combatId);
            if (creature == null)
            {
                Logger.Warning($"No creature found with combat_id {combatId}");
                return null;
            }

            // Verify it's a pet of the player
            var playerCombatState = player.PlayerCombatState;
            if (playerCombatState != null)
                foreach (var pet in playerCombatState.Pets)
                    if (pet.CombatId == combatId)
                        return pet;

            Logger.Warning($"Creature with combat_id {combatId} is not the player or a pet");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to resolve ally target with combat_id {combatId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Resolves a target creature based on the action's <see cref="TargetType" />.
    ///     Handles <c>AnyEnemy</c>, <c>Self</c>, <c>AnyPlayer</c>, and non-targeted types
    ///     (e.g. <c>AllEnemies</c>, <c>None</c>).
    /// </summary>
    /// <param name="player">The local player.</param>
    /// <param name="targetType">The target type of the card or potion.</param>
    /// <param name="targetCombatId">Optional combat ID supplied by the caller.</param>
    /// <param name="itemTitle">Display name of the card/potion for error messages.</param>
    /// <returns>
    ///     A tuple of (resolved creature, error response).
    ///     If error is non-null, the caller should return it immediately.
    /// </returns>
    public static (Creature? Target, object? Error) ResolveTarget(
        Player player, TargetType targetType, int? targetCombatId, string itemTitle)
    {
        switch (targetType)
        {
            case TargetType.AnyEnemy:
                if (targetCombatId == null)
                    return (null, new
                    {
                        ok = false, error = "TARGET_REQUIRED",
                        message = $"'{itemTitle}' requires a target. Provide 'target' with an enemy combat_id."
                    });

                var enemy = ResolveEnemyTarget((uint)targetCombatId.Value);
                if (enemy == null)
                    return (null, new
                    {
                        ok = false, error = "TARGET_NOT_FOUND",
                        message = $"No hittable enemy found with combat_id {targetCombatId}"
                    });

                return (enemy, null);

            case TargetType.Self:
                if (targetCombatId != null)
                    return (null, new
                    {
                        ok = false, error = "TARGET_NOT_ALLOWED",
                        message = $"'{itemTitle}' is self-targeting and does not accept a target"
                    });

                return (player.Creature, null);

            case TargetType.AnyPlayer:
                if (targetCombatId == null)
                    return (player.Creature, null); // Default to player

                var ally = ResolveAllyTarget(player, (uint)targetCombatId.Value);
                if (ally == null)
                    return (null, new
                    {
                        ok = false, error = "TARGET_NOT_FOUND",
                        message = $"No ally found with combat_id {targetCombatId}. Must be the player or a pet."
                    });

                return (ally, null);

            default:
                // AllEnemies, AllAllies, None, RandomEnemy, etc. — no target needed
                if (targetCombatId != null)
                    return (null, new
                    {
                        ok = false, error = "TARGET_NOT_ALLOWED",
                        message = $"'{itemTitle}' has target type '{targetType}' and does not accept a target"
                    });

                return (null, null);
        }
    }

    /// <summary>
    ///     Enqueues a <see cref="GameAction" />, subscribes to its lifecycle events,
    ///     and awaits completion with a timeout.
    ///     Bridges <c>AfterFinished</c> and <c>BeforeCancelled</c> to a <see cref="TaskCompletionSource{T}" />.
    /// </summary>
    /// <param name="action">The game action to enqueue.</param>
    /// <param name="timeoutMs">Maximum milliseconds to wait for the action to complete.</param>
    /// <returns>
    ///     <see cref="GameActionState.Finished" /> or <see cref="GameActionState.Canceled" /> on completion;
    ///     <c>null</c> if the timeout elapsed before the action resolved.
    /// </returns>
    public static async Task<GameActionState?> EnqueueAndAwaitAsync(GameAction action, int timeoutMs)
    {
        var tcs = new TaskCompletionSource<GameActionState>();
        action.AfterFinished += _ => tcs.TrySetResult(GameActionState.Finished);
        action.BeforeCancelled += _ => tcs.TrySetResult(GameActionState.Canceled);

        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
        return completedTask == tcs.Task ? tcs.Task.Result : null;
    }

    /// <summary>
    ///     Polls asynchronously until a condition is met or the timeout expires.
    ///     Uses <c>await Task.Delay</c> to yield the main thread between polls,
    ///     allowing Godot to process frames during the wait.
    /// </summary>
    /// <param name="condition">Predicate evaluated each poll cycle; returns <c>true</c> to stop waiting.</param>
    /// <param name="timeoutMs">Maximum milliseconds to wait.</param>
    /// <param name="pollIntervalMs">Milliseconds between each poll (default <see cref="DefaultPollIntervalMs" />).</param>
    /// <returns><c>true</c> if the condition was met; <c>false</c> if the timeout expired.</returns>
    public static async Task<bool> PollUntilAsync(Func<bool> condition, int timeoutMs,
        int pollIntervalMs = DefaultPollIntervalMs)
    {
        var elapsed = 0;
        while (elapsed < timeoutMs)
        {
            await Task.Delay(pollIntervalMs);
            elapsed += pollIntervalMs;

            if (condition())
                return true;
        }

        return false;
    }
}