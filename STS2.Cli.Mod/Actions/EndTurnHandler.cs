using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles end turn action using the game's native PlayerCmd.
/// </summary>
public static class EndTurnHandler
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
                return new { ok = false, error = "NOT_IN_COMBAT", message = "Not currently in combat" };

            if (CombatManager.Instance.IsOverOrEnding)
                return new { ok = false, error = "COMBAT_ENDING", message = "Combat is over or ending" };

            if (!CombatManager.Instance.IsPlayPhase)
                return new { ok = false, error = "NOT_PLAYER_TURN", message = "Not in play phase - cannot end turn during enemy turn" };

            if (CombatManager.Instance.PlayerActionsDisabled)
                return new { ok = false, error = "ACTIONS_DISABLED", message = "Player actions are currently disabled (turn may already be ending)" };

            // Get player
            var player = ActionUtils.GetLocalPlayer();
            if (player == null)
                return new { ok = false, error = "NO_PLAYER", message = "Player not found" };

            // Use PlayerCmd.EndTurn (same as game UI)
            // canBackOut: false means the AI cannot undo the end-turn decision
            PlayerCmd.EndTurn(player, canBackOut: false);

            Logger.Info("EndTurn action executed via PlayerCmd");
            return new { ok = true, data = new { action = "END_TURN" } };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to end turn: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }
}
