using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Models.Dto;
using STS2.Cli.Mod.State.Builders;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.State;

/// <summary>
///     Extracts game state from Slay the Spire 2 using direct type references.
/// </summary>
public static class GameStateExtractor
{
    private static readonly ModLogger Logger = new("GameStateExtractor");

    /// <summary>
    ///     Gets the current game state.
    /// </summary>
    public static GameStateDto GetState()
    {
        try
        {
            var state = new GameStateDto
            {
                Screen = DetectScreen(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            // Extract combat state if in combat
            if (state.Screen == "COMBAT")
            {
                state.Combat = ExtractCombatState();
            }

            return state;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract game state: {ex.Message}");
            return new GameStateDto { Screen = "ERROR", Error = ex.Message };
        }
    }

    /// <summary>
    ///     Detects which screen the player is currently on.
    /// </summary>
    private static string DetectScreen()
    {
        // Check if a run is in progress
        if (!RunManager.Instance.IsInProgress)
        {
            return "MENU";
        }

        // Check CombatManager for active combat
        if (CombatManager.Instance.IsInProgress)
        {
            return "COMBAT";
        }

        // TODO: Detect other screens (MAP, SHOP, EVENT, etc.)
        return "UNKNOWN";
    }

    /// <summary>
    ///     Extracts combat state from CombatManager.
    /// </summary>
    private static CombatStateDto? ExtractCombatState()
    {
        try
        {
            var combatManager = CombatManager.Instance;
            if (!combatManager.IsInProgress)
            {
                Logger.Warning("CombatManager reports IsInProgress = false");
                return null;
            }

            // Get CombatState via DebugOnlyGetState
            var combatState = combatManager.DebugOnlyGetState();
            if (combatState == null)
            {
                Logger.Warning("CombatState is null");
                return null;
            }

            var result = new CombatStateDto
            {
                IsPlayerTurn = combatManager.IsPlayPhase,
                TurnNumber = combatState.RoundNumber
            };

            // Extract player state
            var player = GetLocalPlayer(combatState);
            if (player != null)
            {
                result.Player = PlayerStateBuilder.Build(player);
                result.Hand = CombatStateBuilder.BuildHand(player);
            }

            // Extract enemies
            result.Enemies = CombatStateBuilder.BuildEnemies(combatState);

            return result;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract combat state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the local player from the combat state.
    /// </summary>
    private static Player? GetLocalPlayer(CombatState combatState)
    {
        try
        {
            // In a single player game, get the first player
            var players = combatState.Players;
            if (players.Count > 0)
            {
                return players[0];
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get local player: {ex.Message}");
        }
        return null;
    }
}
