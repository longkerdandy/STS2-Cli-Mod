using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
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
            if (state.Screen == "COMBAT") state.Combat = ExtractCombatState();

            // Extract reward state if on reward screen
            if (state.Screen == "REWARD") state.Rewards = ExtractRewardState();

            // Extract event state if at an event
            if (state.Screen == "EVENT") state.Event = ExtractEventState();

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
    ///     Priority order: MENU → COMBAT → MAP (takes precedence over stale overlays)
    ///     → EVENT → CARD_REWARD → REWARD → UNKNOWN.
    /// </summary>
    private static string DetectScreen()
    {
        // Check if a run is in progress
        if (!RunManager.Instance.IsInProgress) return "MENU";

        // Check CombatManager for active combat
        if (CombatManager.Instance.IsInProgress) return "COMBAT";

        // Check NMapScreen.IsOpen BEFORE overlay stack.
        // After proceeding from rewards, the map opens but NRewardsScreen may linger
        // in the overlay stack. NMapScreen.IsOpen is the authoritative signal that
        // the player has moved past the current room to the map.
        if (NMapScreen.Instance is { IsOpen: true }) return "MAP";

        // Check for event room BEFORE overlay stack.
        // Event rooms don't use the overlay stack — the event UI is part of the room node.
        // When proceeding from an event, the map opens but NEventRoom.Instance may still
        // be valid momentarily. Checking MAP first avoids this stale reference.
        var eventRoom = NEventRoom.Instance;
        if (eventRoom is { } && eventRoom.IsInsideTree())
        {
            Logger.Info("Detected EVENT screen");
            return "EVENT";
        }

        // Check NOverlayStack for reward-related screens
        // CARD_REWARD must be checked before REWARD because NCardRewardSelectionScreen
        // is pushed on top of NRewardsScreen in the overlay stack
        var overlayStack = NOverlayStack.Instance;
        if (overlayStack == null)
        {
            Logger.Warning("NOverlayStack.Instance is null (NRun.Instance?.GlobalUi.Overlays)");
            return "UNKNOWN";
        }

        var overlay = overlayStack.Peek();
        if (overlay == null)
        {
            Logger.Info("NOverlayStack.Peek() returned null (no overlays on stack)");
            return "UNKNOWN";
        }

        Logger.Info($"NOverlayStack.Peek() returned: {overlay.GetType().FullName}");

        if (overlay is NCardRewardSelectionScreen) return "CARD_REWARD";
        if (overlay is NRewardsScreen) return "REWARD";

        // TODO: Detect other screens (SHOP, EVENT, etc.)
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
                Encounter = combatState.Encounter?.Id.Entry,
                TurnNumber = combatState.RoundNumber,
                IsPlayerTurn = combatManager.IsPlayPhase,
                IsPlayerActionsDisabled = combatManager.PlayerActionsDisabled,
                IsCombatEnding = combatManager.IsOverOrEnding
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
            if (players.Count > 0) return players[0];
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get local player: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    ///     Extracts the reward state from the <see cref="NRewardsScreen" />.
    ///     Finds the rewards screen in the overlay stack and reads reward buttons.
    /// </summary>
    private static RewardStateDto? ExtractRewardState()
    {
        try
        {
            return RewardStateBuilder.Build();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract reward state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Extracts the event state from the <see cref="NEventRoom" />.
    ///     Uses reflection to access the private EventModel field.
    /// </summary>
    private static EventStateDto? ExtractEventState()
    {
        try
        {
            return EventStateBuilder.Build();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract event state: {ex.Message}");
            return null;
        }
    }
}