using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Common utilities for action handlers.
/// </summary>
public static class ActionUtils
{
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
}