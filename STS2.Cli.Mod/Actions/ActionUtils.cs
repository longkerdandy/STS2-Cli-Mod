using MegaCrit.Sts2.Core.Combat;
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
    ///     Requires an active combat (caller must validate <see cref="CombatManager.IsInProgress"/> first).
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
}