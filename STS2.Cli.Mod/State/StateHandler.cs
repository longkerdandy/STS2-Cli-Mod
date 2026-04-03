using System.Collections;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Events.Custom.CrystalSphere;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Timeline.Epochs;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.State.Builders;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.State;

/// <summary>
///     Handles the 'state' command by extracting the current game state
///     from Slay the Spire 2 using direct type references.
/// </summary>
public static class StateHandler
{
    private static readonly ModLogger Logger = new("StateHandler");

    /// <summary>
    ///     Handles the state request.
    /// </summary>
    /// <param name="request">The parsed request object.</param>
    /// <returns>Response containing the game state DTO, or an error if extraction failed.</returns>
    public static object HandleRequest(Request request)
    {
        Logger.Info("Requested game state");

        var state = GetState(request.IncludePileDetails);

        if (state.Error != null)
            return new { ok = false, error = "STATE_EXTRACTION_ERROR", message = state.Error };

        return new { ok = true, data = state };
    }

    /// <summary>
    ///     Gets the current game state.
    /// </summary>
    /// <param name="includePileDetails">Whether to include full card descriptions in pile listings.</param>
    private static GameStateDto GetState(bool includePileDetails = false)
    {
        try
        {
            var state = new GameStateDto
            {
                Screen = DetectScreen(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            // Extract combat state if in combat
            if (state.Screen == "COMBAT") state.Combat = ExtractCombatState(includePileDetails);

            // Extract hand selection state if in hand card selection (combat sub-state)
            // Also include combat state so the AI has full context
            if (state.Screen == "HAND_SELECT")
            {
                state.Combat = ExtractCombatState(includePileDetails);
                state.HandSelect = ExtractHandSelectState();
            }

            // Extract map state if on map screen
            if (state.Screen == "MAP") state.Map = ExtractMapState();

            // Extract reward state if on reward screen
            if (state.Screen == "REWARD") state.Rewards = ExtractRewardState();

            // Extract event state if at an event
            if (state.Screen == "EVENT") state.Event = ExtractEventState();

            // Extract tri-select (choose-a-card) state if on tri-select screen
            // Also include combat state when tri-select is triggered during combat
            // so the AI has full context (e.g., Discovery/Quasar/Splash mid-combat)
            if (state.Screen == "TRI_SELECT")
            {
                state.TriSelect = ExtractTriSelectState();
                if (CombatManager.Instance.IsInProgress)
                    state.Combat = ExtractCombatState(includePileDetails);
            }

            // Extract character select state if on character select screen
            if (state.Screen == "CHARACTER_SELECT") state.CharacterSelect = ExtractCharacterSelectState();

            // Extract grid card selection state if on a grid-based card selection screen
            // Also include combat state when grid select is triggered during combat
            // so the AI has full context (e.g., Headbutt selecting from discard pile)
            if (state.Screen == "GRID_CARD_SELECT")
            {
                state.GridCardSelect = ExtractGridCardSelectState();
                if (CombatManager.Instance.IsInProgress)
                    state.Combat = ExtractCombatState(includePileDetails);
            }

            // Extract rest site state if at a rest site
            if (state.Screen == "REST_SITE") state.RestSite = ExtractRestSiteState();

            // Extract treasure room state if at a treasure room
            if (state.Screen == "TREASURE") state.Treasure = ExtractTreasureState();

            // Extract shop state if at a merchant room
            if (state.Screen == "SHOP") state.Shop = ExtractShopState();

            // Extract relic selection state if a "choose a relic" overlay is open
            if (state.Screen == "RELIC_SELECT") state.RelicSelect = ExtractRelicSelectState();

            // Extract bundle selection state if a "choose a bundle" overlay is open
            if (state.Screen == "BUNDLE_SELECT") state.BundleSelect = ExtractBundleSelectState();

            // Extract Crystal Sphere mini-game state if the overlay is open
            if (state.Screen == "CRYSTAL_SPHERE") state.CrystalSphere = ExtractCrystalSphereState();

            // Extract game over state if on game over screen
            if (state.Screen == "GAME_OVER") state.GameOver = ExtractGameOverState();

            // Extract menu state if on main menu
            if (state.Screen == "MENU") state.Menu = ExtractMenuState();

            // Extract singleplayer submenu state if on singleplayer submenu
            if (state.Screen == "SINGLEPLAYER_SUBMENU") state.SingleplayerSubmenu = ExtractSingleplayerSubmenuState();

            return state;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract game state: {ex.Message}");
            return new GameStateDto { Screen = "ERROR", Error = ex.Message };
        }
    }

    /// <summary>
    ///     Public accessor for the current screen detection.
    ///     Used by action handlers that need to report the resulting screen after an action.
    /// </summary>
    public static string DetectCurrentScreen()
    {
        return DetectScreen();
    }

    /// <summary>
    ///     Detects which screen the player is currently on.
    ///     Checks pre-run screens (menu / submenus) first, then in-run screens
    ///     grouped by: combat → map → overlays → rooms → game over.
    /// </summary>
    private static string DetectScreen()
    {
        // --- Pre-run: main menu and its submenu stack ---
        if (ScreenUtils.FindCharacterSelectScreen() != null) return "CHARACTER_SELECT";
        if (ScreenUtils.FindSingleplayerSubmenu() != null) return "SINGLEPLAYER_SUBMENU";
        if (ScreenUtils.FindMainMenu() != null) return "MENU";

        // All screens below require an active run
        if (!RunManager.Instance.IsInProgress) return "UNKNOWN";

        // --- Combat and its sub-states ---
        if (CombatManager.Instance.IsInProgress)
        {
            if (NPlayerHand.Instance is { IsInCardSelection: true }) return "HAND_SELECT";

            var combatOverlay = NOverlayStack.Instance;
            if (combatOverlay != null)
            {
                var children = combatOverlay.GetChildren();
                for (var i = children.Count - 1; i >= 0; i--)
                    switch (children[i])
                    {
                        case NCardGridSelectionScreen: return "GRID_CARD_SELECT";
                        case NChooseACardSelectionScreen: return "TRI_SELECT";
                        case NChooseABundleSelectionScreen: return "BUNDLE_SELECT";
                        case NCrystalSphereScreen: return "CRYSTAL_SPHERE";
                    }
            }

            return "COMBAT";
        }

        // Map takes priority over overlays — after proceeding from rewards,
        // NRewardsScreen may linger in the overlay stack.
        if (NMapScreen.Instance is { IsOpen: true }) return "MAP";

        // --- Overlay stack (checked before rooms because events/rest can trigger overlays) ---
        var overlayStack = NOverlayStack.Instance;
        if (overlayStack != null)
        {
            var children = overlayStack.GetChildren();
            for (var i = children.Count - 1; i >= 0; i--)
                switch (children[i])
                {
                    case NCardRewardSelectionScreen: return "CARD_REWARD";
                    case NCrystalSphereScreen: return "CRYSTAL_SPHERE";
                    case NRewardsScreen: return "REWARD";
                    case NChooseACardSelectionScreen: return "TRI_SELECT";
                    case NCardGridSelectionScreen: return "GRID_CARD_SELECT";
                    case NChooseARelicSelection: return "RELIC_SELECT";
                    case NChooseABundleSelectionScreen: return "BUNDLE_SELECT";
                    case NGameOverScreen: return "GAME_OVER";
                }
        }

        // --- Room-based screens ---
        var eventRoom = NEventRoom.Instance;
        if (eventRoom is not null && eventRoom.IsInsideTree()) return "EVENT";

        var restSiteRoom = NRestSiteRoom.Instance;
        if (restSiteRoom is not null && restSiteRoom.IsInsideTree()) return "REST_SITE";

        var treasureRoom = NRun.Instance?.TreasureRoom;
        if (treasureRoom is not null && treasureRoom.IsInsideTree()) return "TREASURE";

        var merchantRoom = NRun.Instance?.MerchantRoom;
        if (merchantRoom is not null && merchantRoom.IsInsideTree()) return "SHOP";

        return "UNKNOWN";
    }

    /// <summary>
    ///     Extracts combat state from CombatManager.
    /// </summary>
    /// <param name="includePileDetails">Whether to include full card descriptions in pile listings.</param>
    private static CombatStateDto? ExtractCombatState(bool includePileDetails = false)
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
                result.DrawPile = CombatStateBuilder.BuildDrawPile(player, includePileDetails);
                result.DiscardPile = CombatStateBuilder.BuildDiscardPile(player, includePileDetails);
                result.ExhaustPile = CombatStateBuilder.BuildExhaustPile(player, includePileDetails);
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
    ///     Extracts map state from <see cref="NMapScreen" /> and <see cref="RunState" />.
    /// </summary>
    private static MapStateDto? ExtractMapState()
    {
        try
        {
            return MapStateBuilder.Build();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract map state: {ex.Message}");
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
    ///     Extracts the tri-select (choose-a-card) state from <see cref="NChooseACardSelectionScreen" />.
    ///     Delegates to <see cref="TriSelectStateBuilder" /> for the actual extraction.
    /// </summary>
    private static TriSelectStateDto? ExtractTriSelectState()
    {
        try
        {
            return TriSelectStateBuilder.Build();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract tri-select state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Extracts the grid card selection state from a <see cref="NCardGridSelectionScreen" />.
    ///     Checks the overlay stack for any grid-based card selection screen subtype.
    /// </summary>
    private static GridCardSelectStateDto? ExtractGridCardSelectState()
    {
        try
        {
            var screen = FindGridSelectionScreen();
            if (screen == null)
            {
                Logger.Warning("GRID_CARD_SELECT screen detected but no grid selection screen found");
                return null;
            }

            return GridCardSelectStateBuilder.Build(screen);
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract grid card select state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Extracts the rest site (campfire) state from <see cref="NRestSiteRoom" />.
    /// </summary>
    private static RestSiteStateDto? ExtractRestSiteState()
    {
        try
        {
            return RestSiteStateBuilder.Build();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract rest site state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Extracts the treasure room state from <see cref="NTreasureRoom" />.
    /// </summary>
    private static TreasureStateDto? ExtractTreasureState()
    {
        try
        {
            return TreasureStateBuilder.Build();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract treasure state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Extracts the shop (merchant room) state from <see cref="NMerchantRoom" />.
    /// </summary>
    private static ShopStateDto? ExtractShopState()
    {
        try
        {
            return ShopStateBuilder.Build();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract shop state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Extracts the relic selection state from <see cref="NChooseARelicSelection" />.
    ///     Delegates to <see cref="RelicSelectStateBuilder" /> for the actual extraction.
    /// </summary>
    private static RelicSelectStateDto? ExtractRelicSelectState()
    {
        try
        {
            return RelicSelectStateBuilder.Build();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract relic select state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Extracts the bundle selection state from <see cref="NChooseABundleSelectionScreen" />.
    ///     Delegates to <see cref="BundleSelectStateBuilder" /> for the actual extraction.
    /// </summary>
    private static BundleSelectStateDto? ExtractBundleSelectState()
    {
        try
        {
            return BundleSelectStateBuilder.Build();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract bundle select state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Extracts the Crystal Sphere mini-game state from <see cref="NCrystalSphereScreen" />.
    ///     Delegates to <see cref="CrystalSphereStateBuilder" /> for the actual extraction.
    /// </summary>
    private static CrystalSphereStateDto? ExtractCrystalSphereState()
    {
        try
        {
            return CrystalSphereStateBuilder.Build();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract Crystal Sphere state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Extracts the hand card selection state from <see cref="NPlayerHand" />.
    /// </summary>
    private static HandSelectStateDto? ExtractHandSelectState()
    {
        try
        {
            return HandSelectStateBuilder.Build();
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract hand select state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Extracts the game over screen state from <see cref="NGameOverScreen" />.
    /// </summary>
    private static GameOverStateDto? ExtractGameOverState()
    {
        try
        {
            var overlayStack = NOverlayStack.Instance;
            if (overlayStack == null)
            {
                Logger.Warning("NOverlayStack.Instance is null, cannot extract game over state");
                return null;
            }

            var gameOverScreen = overlayStack.Peek() as NGameOverScreen;
            if (gameOverScreen == null)
            {
                Logger.Warning("Game over screen detected but NGameOverScreen not found in overlay stack");
                return null;
            }

            // Use reflection to access private fields for run state info
            var runStateField = typeof(NGameOverScreen).GetField("_runState",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var runState = runStateField?.GetValue(gameOverScreen);

            var scoreField = typeof(NGameOverScreen).GetField("_score",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var score = scoreField?.GetValue(gameOverScreen) as int? ?? 0;

            // Check button availability
            var mainMenuButton = gameOverScreen.GetNodeOrNull<Node>("%MainMenuButton");
            var continueButton = gameOverScreen.GetNodeOrNull<Node>("%ContinueButton");

            // Determine victory status from run state
            var isVictory = false;
            var floor = 0;
            string? characterId = null;

            if (runState != null)
            {
                // Try to get victory status
                var winProperty = runState.GetType().GetProperty("Win");
                if (winProperty != null) isVictory = (bool)(winProperty.GetValue(runState) ?? false);

                // Try to get current floor
                var floorProperty = runState.GetType().GetProperty("CurrentFloor");
                if (floorProperty != null) floor = (int)(floorProperty.GetValue(runState) ?? 0);

                // Try to get character info from run state
                var charactersProperty = runState.GetType().GetProperty("Characters");
                if (charactersProperty != null)
                {
                    var characters = charactersProperty.GetValue(runState) as IList;
                    if (characters != null && characters.Count > 0)
                    {
                        var firstChar = characters[0];
                        var idProperty = firstChar?.GetType().GetProperty("Id");
                        if (idProperty != null)
                        {
                            var idObj = idProperty.GetValue(firstChar);
                            var entryProperty = idObj?.GetType().GetProperty("Entry");
                            if (entryProperty != null) characterId = entryProperty.GetValue(idObj) as string;
                        }
                    }
                }
            }

            return new GameOverStateDto
            {
                IsVictory = isVictory,
                Floor = floor,
                CharacterId = characterId,
                Score = score,
                EpochsDiscovered = 0,
                CanReturnToMenu = mainMenuButton != null,
                CanContinue = continueButton != null
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract game over state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Finds a <see cref="NCardGridSelectionScreen" /> in the overlay stack.
    /// </summary>
    private static NCardGridSelectionScreen? FindGridSelectionScreen()
    {
        var overlayStack = NOverlayStack.Instance;
        if (overlayStack == null) return null;

        // Check the top of the stack first
        var overlay = overlayStack.Peek();
        if (overlay is NCardGridSelectionScreen gridScreen)
            return gridScreen;

        // Check children
        foreach (var child in overlayStack.GetChildren())
            if (child is NCardGridSelectionScreen childScreen)
                return childScreen;

        return null;
    }

    /// <summary>
    ///     Extracts the character selection state from NCharacterSelectScreen.
    /// </summary>
    private static CharacterSelectStateDto? ExtractCharacterSelectState()
    {
        try
        {
            var screen = ScreenUtils.FindCharacterSelectScreen();
            if (screen == null)
            {
                Logger.Warning("CHARACTER_SELECT screen detected but NCharacterSelectScreen not found");
                return null;
            }

            // Get character buttons from the button container
            var buttonContainer = screen.GetNodeOrNull<Control>("CharSelectButtons/ButtonContainer");
            if (buttonContainer == null)
            {
                Logger.Warning("Character button container not found");
                return null;
            }

            var buttons = CommonUiUtils.FindAll<NCharacterSelectButton>(buttonContainer);
            var characters = new List<CharacterOptionDto>();
            string? selectedCharacter = null;

            // Get the currently selected button via reflection
            var selectedButton = GetSelectedButton(screen);

            foreach (var btn in buttons)
            {
                // Get character model from public Character property
                var characterModel = GetCharacterModel(btn);
                if (characterModel == null) continue;

                var isSelected = btn == selectedButton;
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
            // Ascension is a public property on NAscensionPanel
            var current = panel.Ascension;

            // _maxAscension is a private field
            var max = 20;
            var maxField = typeof(NAscensionPanel).GetField("_maxAscension",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (maxField != null)
                max = (int)(maxField.GetValue(panel) ?? 20);

            return (current, max);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get ascension info: {ex.Message}");
            return (0, 20);
        }
    }

    /// <summary>
    ///     Gets the selected character button from the screen via reflection.
    /// </summary>
    private static NCharacterSelectButton? GetSelectedButton(NCharacterSelectScreen screen)
    {
        return CharacterSelectUtils.GetSelectedButton(screen);
    }

    /// <summary>
    ///     Gets the CharacterModel from a character select button's public Character property.
    /// </summary>
    private static CharacterModel? GetCharacterModel(NCharacterSelectButton btn)
    {
        return CharacterSelectUtils.GetCharacterModel(btn);
    }

    /// <summary>
    ///     Gets the locked status from a character select button.
    /// </summary>
    private static bool GetIsLocked(NCharacterSelectButton btn)
    {
        return CharacterSelectUtils.GetIsLocked(btn);
    }

    /// <summary>
    ///     Extracts the main menu state (saved run availability).
    /// </summary>
    private static MenuStateDto? ExtractMenuState()
    {
        try
        {
            return new MenuStateDto
            {
                HasRunSave = SaveManager.Instance.HasRunSave
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract menu state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Extracts the singleplayer submenu state (available game modes).
    /// </summary>
    private static SingleplayerSubmenuStateDto? ExtractSingleplayerSubmenuState()
    {
        try
        {
            return new SingleplayerSubmenuStateDto
            {
                StandardAvailable = true,
                DailyAvailable = SaveManager.Instance.IsEpochRevealed<DailyRunEpoch>(),
                CustomAvailable = SaveManager.Instance.IsEpochRevealed<CustomAndSeedsEpoch>()
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract singleplayer submenu state: {ex.Message}");
            return null;
        }
    }
}