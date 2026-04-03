using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
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
    public static string DetectCurrentScreen() => DetectScreen();

    /// <summary>
    ///     Detects which screen the player is currently on.
    ///     Priority order: CHARACTER_SELECT → COMBAT (with HAND_SELECT, GRID_CARD_SELECT,
    ///     TRI_SELECT, and BUNDLE_SELECT sub-states) → MAP → overlay screens (CARD_REWARD,
    ///     TRI_SELECT, GRID_CARD_SELECT, RELIC_SELECT, BUNDLE_SELECT, REWARD) → EVENT →
    ///     REST_SITE → TREASURE → SHOP → UNKNOWN.
    ///     Overlay screens take priority over EVENT because events can trigger overlays
    ///     (e.g., Neow's Lead Paperweight opens NCardRewardSelectionScreen while NEventRoom
    ///     is still in the scene tree).
    ///     REST_SITE is checked after overlays because SMITH opens a card selection overlay.
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

        // Check for singleplayer submenu (Standard/Daily/Custom) on the main menu.
        // This must be checked before the generic MENU fallback because the submenu
        // is a sub-state of the main menu (RunManager.IsInProgress is still false).
        if (ScreenUtils.FindSingleplayerSubmenu() != null)
        {
            Logger.Info("Detected SINGLEPLAYER_SUBMENU screen");
            return "SINGLEPLAYER_SUBMENU";
        }

        // Check if a run is in progress
        if (!RunManager.Instance.IsInProgress) return "MENU";

        // Check CombatManager for active combat
        // Hand selection and grid card selection are sub-states of combat — detected here
        // before generic COMBAT because they require different commands.
        // Grid overlays (NSimpleCardSelectScreen) can appear during combat when cards like
        // Headbutt, Hologram, SecretWeapon etc. trigger selection from draw/discard pile.
        if (CombatManager.Instance.IsInProgress)
        {
            if (NPlayerHand.Instance is { IsInCardSelection: true })
            {
                Logger.Info("Detected HAND_SELECT screen (combat sub-state)");
                return "HAND_SELECT";
            }

            // Check overlay stack for grid selection screens triggered during combat
            var combatOverlay = NOverlayStack.Instance;
            if (combatOverlay != null)
            {
                var topOverlay = combatOverlay.Peek();
                if (topOverlay is NCardGridSelectionScreen)
                {
                    Logger.Info($"Detected GRID_CARD_SELECT screen during combat ({topOverlay.GetType().Name})");
                    return "GRID_CARD_SELECT";
                }

                // Cards like Discovery, Quasar, Splash open NChooseACardSelectionScreen during combat
                if (topOverlay is NChooseACardSelectionScreen)
                {
                    Logger.Info("Detected TRI_SELECT screen during combat");
                    return "TRI_SELECT";
                }

                if (topOverlay is NChooseABundleSelectionScreen)
                {
                    Logger.Info("Detected BUNDLE_SELECT screen during combat");
                    return "BUNDLE_SELECT";
                }

                if (topOverlay is NCrystalSphereScreen)
                {
                    Logger.Info("Detected CRYSTAL_SPHERE screen during combat");
                    return "CRYSTAL_SPHERE";
                }

                // Also check children in case it's not on top
                foreach (var child in combatOverlay.GetChildren())
                {
                    if (child is NCardGridSelectionScreen)
                    {
                        Logger.Info($"Detected GRID_CARD_SELECT screen during combat in children ({child.GetType().Name})");
                        return "GRID_CARD_SELECT";
                    }

                    if (child is NChooseACardSelectionScreen)
                    {
                        Logger.Info("Detected TRI_SELECT screen during combat in children");
                        return "TRI_SELECT";
                    }

                    if (child is NChooseABundleSelectionScreen)
                    {
                        Logger.Info("Detected BUNDLE_SELECT screen during combat in children");
                        return "BUNDLE_SELECT";
                    }

                    if (child is NCrystalSphereScreen)
                    {
                        Logger.Info("Detected CRYSTAL_SPHERE screen during combat in children");
                        return "CRYSTAL_SPHERE";
                    }
                }
            }

            return "COMBAT";
        }

        // Check NMapScreen.IsOpen BEFORE overlay stack.
        // After proceeding from rewards, the map opens but NRewardsScreen may linger
        // in the overlay stack. NMapScreen.IsOpen is the authoritative signal that
        // the player has moved past the current room to the map.
        if (NMapScreen.Instance is { IsOpen: true }) return "MAP";

        // Check NOverlayStack for interactive screens BEFORE event detection.
        // Events can trigger overlay screens (e.g., Neow opening card reward selection),
        // and NEventRoom.Instance remains in the scene tree while the overlay is active.
        // CARD_REWARD must be checked before REWARD because NCardRewardSelectionScreen
        // is pushed on top of NRewardsScreen in the overlay stack.
        var overlayStack = NOverlayStack.Instance;
        if (overlayStack != null)
        {
            var overlay = overlayStack.Peek();
            if (overlay != null)
            {
                Logger.Info($"NOverlayStack.Peek() returned: {overlay.GetType().FullName}");

                if (overlay is NCardRewardSelectionScreen) return "CARD_REWARD";

                if (overlay is NCrystalSphereScreen)
                {
                    Logger.Info("Detected CRYSTAL_SPHERE screen");
                    return "CRYSTAL_SPHERE";
                }

                if (overlay is NRewardsScreen) return "REWARD";

                if (overlay is NChooseACardSelectionScreen)
                {
                    Logger.Info("Detected TRI_SELECT screen");
                    return "TRI_SELECT";
                }

                if (overlay is NCardGridSelectionScreen)
                {
                    Logger.Info($"Detected GRID_CARD_SELECT screen ({overlay.GetType().Name})");
                    return "GRID_CARD_SELECT";
                }

                if (overlay is NChooseARelicSelection)
                {
                    Logger.Info("Detected RELIC_SELECT screen");
                    return "RELIC_SELECT";
                }

                if (overlay is NChooseABundleSelectionScreen)
                {
                    Logger.Info("Detected BUNDLE_SELECT screen");
                    return "BUNDLE_SELECT";
                }
            }

            // Check children for screens that may not be on top
            foreach (var child in overlayStack.GetChildren())
            {
                if (child is NChooseACardSelectionScreen)
                {
                    Logger.Info("Detected TRI_SELECT screen (in children)");
                    return "TRI_SELECT";
                }

                if (child is NCardGridSelectionScreen)
                {
                    Logger.Info($"Detected GRID_CARD_SELECT screen in children ({child.GetType().Name})");
                    return "GRID_CARD_SELECT";
                }

                if (child is NChooseARelicSelection)
                {
                    Logger.Info("Detected RELIC_SELECT screen (in children)");
                    return "RELIC_SELECT";
                }

                if (child is NChooseABundleSelectionScreen)
                {
                    Logger.Info("Detected BUNDLE_SELECT screen (in children)");
                    return "BUNDLE_SELECT";
                }

                if (child is NCrystalSphereScreen)
                {
                    Logger.Info("Detected CRYSTAL_SPHERE screen (in children)");
                    return "CRYSTAL_SPHERE";
                }
            }
        }
        else
        {
            Logger.Warning("NOverlayStack.Instance is null (NRun.Instance?.GlobalUi.Overlays)");
        }

        // Check for event room AFTER overlay stack.
        // Event rooms don't use the overlay stack — the event UI is part of the room node.
        // However, events can trigger overlays (card rewards, deck selection, etc.),
        // so overlay detection must happen first.
        var eventRoom = NEventRoom.Instance;
        if (eventRoom is { } && eventRoom.IsInsideTree())
        {
            Logger.Info("Detected EVENT screen");
            return "EVENT";
        }

        // Check for rest site room.
        // NRestSiteRoom.Instance resolves to NRun.Instance?.RestSiteRoom.
        // Must check AFTER overlays because SMITH opens a card selection overlay.
        var restSiteRoom = NRestSiteRoom.Instance;
        if (restSiteRoom is { } && restSiteRoom.IsInsideTree())
        {
            Logger.Info("Detected REST_SITE screen");
            return "REST_SITE";
        }

        // Check for treasure room.
        // NRun.Instance?.TreasureRoom returns non-null if the current room is a treasure room.
        var treasureRoom = NRun.Instance?.TreasureRoom;
        if (treasureRoom is { } && treasureRoom.IsInsideTree())
        {
            Logger.Info("Detected TREASURE screen");
            return "TREASURE";
        }

        // Check for merchant room (shop).
        // NRun.Instance?.MerchantRoom returns non-null if the current room is a merchant room.
        var merchantRoom = NRun.Instance?.MerchantRoom;
        if (merchantRoom is { } && merchantRoom.IsInsideTree())
        {
            Logger.Info("Detected SHOP screen");
            return "SHOP";
        }

        // Check for game over screen via overlay stack.
        // NGameOverScreen implements IOverlayScreen and is pushed to NOverlayStack.
        var overlayStackForGameOver = NOverlayStack.Instance;
        if (overlayStackForGameOver != null)
        {
            var topOverlay = overlayStackForGameOver.Peek();
            if (topOverlay is NGameOverScreen)
            {
                Logger.Info("Detected GAME_OVER screen");
                return "GAME_OVER";
            }
        }

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
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var runState = runStateField?.GetValue(gameOverScreen);

            var scoreField = typeof(NGameOverScreen).GetField("_score",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var score = scoreField?.GetValue(gameOverScreen) as int? ?? 0;

            // Check button availability
            var mainMenuButton = gameOverScreen.GetNodeOrNull<Godot.Node>("%MainMenuButton");
            var continueButton = gameOverScreen.GetNodeOrNull<Godot.Node>("%ContinueButton");

            // Determine victory status from run state
            bool isVictory = false;
            int floor = 0;
            string? characterId = null;

            if (runState != null)
            {
                // Try to get victory status
                var winProperty = runState.GetType().GetProperty("Win");
                if (winProperty != null)
                {
                    isVictory = (bool)(winProperty.GetValue(runState) ?? false);
                }

                // Try to get current floor
                var floorProperty = runState.GetType().GetProperty("CurrentFloor");
                if (floorProperty != null)
                {
                    floor = (int)(floorProperty.GetValue(runState) ?? 0);
                }

                // Try to get character info from run state
                var charactersProperty = runState.GetType().GetProperty("Characters");
                if (charactersProperty != null)
                {
                    var characters = charactersProperty.GetValue(runState) as System.Collections.IList;
                    if (characters != null && characters.Count > 0)
                    {
                        var firstChar = characters[0];
                        var idProperty = firstChar?.GetType().GetProperty("Id");
                        if (idProperty != null)
                        {
                            var idObj = idProperty.GetValue(firstChar);
                            var entryProperty = idObj?.GetType().GetProperty("Entry");
                            if (entryProperty != null)
                            {
                                characterId = entryProperty.GetValue(idObj) as string;
                            }
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
        {
            if (child is NCardGridSelectionScreen childScreen)
                return childScreen;
        }

        return null;
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
            // Ascension is a public property on NAscensionPanel
            int current = panel.Ascension;

            // _maxAscension is a private field
            int max = 20;
            var maxField = typeof(NAscensionPanel).GetField("_maxAscension",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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
    ///     Finds the Character Select screen in the scene tree.
    /// </summary>
    private static NCharacterSelectScreen? FindCharacterSelectScreen()
    {
        return CharacterSelectUtils.FindScreen();
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
    private static MegaCrit.Sts2.Core.Models.CharacterModel? GetCharacterModel(NCharacterSelectButton btn)
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
                DailyAvailable = SaveManager.Instance.IsEpochRevealed<MegaCrit.Sts2.Core.Timeline.Epochs.DailyRunEpoch>(),
                CustomAvailable = SaveManager.Instance.IsEpochRevealed<MegaCrit.Sts2.Core.Timeline.Epochs.CustomAndSeedsEpoch>()
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract singleplayer submenu state: {ex.Message}");
            return null;
        }
    }
}