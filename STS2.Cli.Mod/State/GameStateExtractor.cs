using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Actions;
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

        // Extract potion selection state if in potion card selection
        if (state.Screen == "POTION_SELECTION") state.PotionSelection = ExtractPotionSelectionState();

        // Extract character select state if on character select screen
        if (state.Screen == "CHARACTER_SELECT") state.CharacterSelect = ExtractCharacterSelectState();

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
    ///     Priority order: CHARACTER_SELECT → COMBAT → MAP (takes precedence over stale overlays)
    ///     → EVENT → CARD_REWARD → REWARD → UNKNOWN.
    /// </summary>
    private static string DetectScreen()
    {
        // Check Character Select screen FIRST (before checking IsInProgress)
        // Character select screen is valid even when RunManager.IsInProgress is false
        if (FindCharacterSelectScreen() != null)
        {
            Logger.Info("Detected CHARACTER_SELECT screen");
            return "CHARACTER_SELECT";
        }

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

        // Check for potion card selection screen
        // This can appear on top of other screens when using selection potions
        if (overlay is NChooseACardSelectionScreen)
        {
            Logger.Info("Detected POTION_SELECTION screen");
            return "POTION_SELECTION";
        }

        // Check children for potion selection screen (it may not be on top)
        foreach (var child in overlayStack.GetChildren())
        {
            if (child is NChooseACardSelectionScreen)
            {
                Logger.Info("Detected POTION_SELECTION screen (in children)");
                return "POTION_SELECTION";
            }
        }

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

    /// <summary>
    ///     Extracts the potion card selection state from <see cref="NChooseACardSelectionScreen" />.
    /// </summary>
    private static PotionSelectionStateDto? ExtractPotionSelectionState()
    {
        try
        {
            var screen = PotionUtils.FindSelectionScreen();
            if (screen == null)
            {
                Logger.Warning("POTION_SELECTION screen detected but no selection screen found");
                return null;
            }

            var cardHolders = UiHelper.FindAll<NCardHolder>(screen);
            var constraints = InferSelectionConstraints(cardHolders);
            var cards = new List<SelectableCardDto>();

            for (int i = 0; i < cardHolders.Count; i++)
            {
                var holder = cardHolders[i];
                var card = holder.CardModel;
                if (card == null) continue;

                cards.Add(new SelectableCardDto
                {
                    Index = i,
                    CardId = card.Id.Entry,
                    CardName = TextUtils.StripGameTags(card.Title),
                    CardType = card.Type.ToString(),
                    Cost = card.EnergyCost.Canonical,
                    Description = TextUtils.StripGameTags(card.Description.GetFormattedText())
                });
            }

            return new PotionSelectionStateDto
            {
                SelectionType = InferSelectionType(cards),
                MinSelect = constraints.MinSelect,
                MaxSelect = constraints.MaxSelect,
                CanSkip = constraints.CanSkip,
                Cards = cards
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract potion selection state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Infers selection constraints from the number and type of available cards.
    /// </summary>
    private static (int MinSelect, int MaxSelect, bool CanSkip) InferSelectionConstraints(List<NCardHolder> cardHolders)
    {
        var cardCount = cardHolders.Count;

        // If we have exactly 3 cards, it's likely a "choose 1 of 3" potion
        if (cardCount == 3)
        {
            return (0, 1, true); // Can skip (Colorless Potion allows skip)
        }

        // If we have many cards, it's likely a hand-based selection
        if (cardCount > 3)
        {
            return (0, cardCount, true);
        }

        // Default: single select, cannot skip
        return (1, 1, false);
    }

    /// <summary>
    ///     Infers selection type from available cards.
    /// </summary>
    private static string InferSelectionType(List<SelectableCardDto> cards)
    {
        if (cards.Count == 3)
        {
            // Check if all same type for pool selection
            var types = cards.Select(c => c.CardType).Distinct().ToList();
            if (types.Count == 1)
            {
                return $"choose_from_pool_{types[0]?.ToLower()}";
            }
            return "choose_from_pool";
        }

        if (cards.Count > 3)
        {
            return "choose_from_hand";
        }

        return "unknown";
    }

    /// <summary>
    ///     Extracts the character selection state from NCharacterSelectScreen.
    /// </summary>
    private static CharacterSelectStateDto? ExtractCharacterSelectState()
    {
        try
        {
            var screen = FindCharacterSelectScreen();
            if (screen == null)
            {
                Logger.Warning("CHARACTER_SELECT screen detected but NCharacterSelectScreen not found");
                return null;
            }

            // Get character buttons from the button container
            var buttonContainer = screen.GetNodeOrNull<Godot.Control>("CharSelectButtons/ButtonContainer");
            if (buttonContainer == null)
            {
                Logger.Warning("Character button container not found");
                return null;
            }

            var buttons = UiHelper.FindAll<NCharacterSelectButton>(buttonContainer);
            var characters = new List<CharacterOptionDto>();
            string? selectedCharacter = null;

            // Get the currently selected button via reflection
            var selectedButton = GetSelectedButton(screen);

            foreach (var btn in buttons)
            {
                // Get character model via reflection
                var characterModel = GetCharacterModel(btn);
                if (characterModel == null) continue;

                var isSelected = (btn == selectedButton);
                if (isSelected)
                    selectedCharacter = characterModel.Id.Entry;

                characters.Add(new CharacterOptionDto
                {
                    CharacterId = characterModel.Id.Entry,
                    CharacterName = TextUtils.StripGameTags(characterModel.Title.GetFormattedText()),
                    IsLocked = GetIsLocked(btn),
                    IsSelected = isSelected
                });
            }

            // Get ascension info
            var ascensionPanel = screen.GetNodeOrNull<NAscensionPanel>("%AscensionPanel");
            var (currentAsc, maxAsc) = GetAscensionInfo(ascensionPanel);

            return new CharacterSelectStateDto
            {
                AvailableCharacters = characters,
                SelectedCharacter = selectedCharacter,
                CurrentAscension = currentAsc,
                MaxAscension = maxAsc,
                CanEmbark = selectedCharacter != null
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract character select state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets ascension information from the ascension panel.
    /// </summary>
    private static (int Current, int Max) GetAscensionInfo(NAscensionPanel? panel)
    {
        if (panel == null) return (0, 20);

        try
        {
            // Try to get current ascension level
            var currentProp = typeof(NAscensionPanel).GetProperty("CurrentLevel");
            int current = 0;
            if (currentProp != null)
                current = (int)(currentProp.GetValue(panel) ?? 0);

            // Try to get max ascension level
            var maxProp = typeof(NAscensionPanel).GetProperty("MaxLevel");
            int max = 20;
            if (maxProp != null)
                max = (int)(maxProp.GetValue(panel) ?? 20);

            return (current, max);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get ascension info: {ex.Message}");
            return (0, 20);
        }
    }

    /// <summary>
    ///     Finds the Character Select screen in the scene tree.
    /// </summary>
    private static NCharacterSelectScreen? FindCharacterSelectScreen()
    {
        return CharacterSelectHelper.FindScreen();
    }

    /// <summary>
    ///     Gets the selected character button from the screen via reflection.
    /// </summary>
    private static NCharacterSelectButton? GetSelectedButton(NCharacterSelectScreen screen)
    {
        return CharacterSelectHelper.GetSelectedButton(screen);
    }

    /// <summary>
    ///     Gets the CharacterModel from a character select button via reflection.
    /// </summary>
    private static MegaCrit.Sts2.Core.Models.CharacterModel? GetCharacterModel(NCharacterSelectButton btn)
    {
        return CharacterSelectHelper.GetCharacterModel(btn);
    }

    /// <summary>
    ///     Gets the locked status from a character select button.
    /// </summary>
    private static bool GetIsLocked(NCharacterSelectButton btn)
    {
        return CharacterSelectHelper.GetIsLocked(btn);
    }
}