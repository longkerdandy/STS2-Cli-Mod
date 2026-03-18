using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Executes end turn action using the game's native ActionQueue.
/// </summary>
public static class EndTurnAction
{
    private static readonly ModLogger Logger = new("EndTurnAction");

    /// <summary>
    ///     Ends the player's turn.
    /// </summary>
    public static object Execute()
    {
        try
        {
            // Validate combat state
            if (!CombatManager.Instance.IsInProgress)
            {
                return new { ok = false, error = "NOT_IN_COMBAT", message = "Not currently in combat" };
            }

            if (!CombatManager.Instance.IsPlayPhase)
            {
                return new { ok = false, error = "NOT_PLAYER_TURN", message = "Not in play phase - cannot end turn during enemy turn" };
            }

            if (CombatManager.Instance.PlayerActionsDisabled)
            {
                return new { ok = false, error = "ACTIONS_DISABLED", message = "Player actions are currently disabled (turn may already be ending)" };
            }

            // Get player
            var player = ActionUtils.GetLocalPlayer();
            if (player == null)
            {
                return new { ok = false, error = "NO_PLAYER", message = "Player not found" };
            }

            // TODO: Find correct EndTurn command/action class
            // For now, this is a placeholder - need to research the correct API
            Logger.Warning("EndTurn action not yet implemented - need to find correct Command class");
            return new { ok = false, error = "NOT_IMPLEMENTED", message = "End turn action requires finding the correct game API (EndTurnCommand or similar)" };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to end turn: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }
}
