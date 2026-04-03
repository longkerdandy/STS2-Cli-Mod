using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Events.Custom.CrystalSphere;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
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

            switch (state.Screen)
            {
                // --- Pre-run ---
                case "CHARACTER_SELECT":
                    state.CharacterSelect = CharacterSelectStateBuilder.Build();
                    break;
                case "SINGLEPLAYER_SUBMENU":
                    state.SingleplayerSubmenu = SingleplayerSubmenuStateBuilder.Build();
                    break;
                case "MENU":
                    state.Menu = MenuStateBuilder.Build();
                    break;

                // --- Combat and its sub-states ---
                case "HAND_SELECT":
                    state.Combat = CombatStateBuilder.Build(includePileDetails);
                    state.HandSelect = HandSelectStateBuilder.Build();
                    break;
                case "GRID_CARD_SELECT":
                    state.GridCardSelect = GridCardSelectStateBuilder.Build();
                    if (CombatManager.Instance.IsInProgress)
                        state.Combat = CombatStateBuilder.Build(includePileDetails);
                    break;
                case "TRI_SELECT":
                    state.TriSelect = TriSelectStateBuilder.Build();
                    if (CombatManager.Instance.IsInProgress)
                        state.Combat = CombatStateBuilder.Build(includePileDetails);
                    break;
                case "BUNDLE_SELECT":
                    state.BundleSelect = BundleSelectStateBuilder.Build();
                    break;
                case "CRYSTAL_SPHERE":
                    state.CrystalSphere = CrystalSphereStateBuilder.Build();
                    break;
                case "COMBAT":
                    state.Combat = CombatStateBuilder.Build(includePileDetails);
                    break;

                // --- Map ---
                case "MAP":
                    state.Map = MapStateBuilder.Build();
                    break;

                // --- Overlay stack ---
                case "REWARD":
                    state.Rewards = RewardStateBuilder.Build();
                    break;
                case "RELIC_SELECT":
                    state.RelicSelect = RelicSelectStateBuilder.Build();
                    break;
                case "GAME_OVER":
                    state.GameOver = GameOverStateBuilder.Build();
                    break;

                // --- Room-based ---
                case "EVENT":
                    state.Event = EventStateBuilder.Build();
                    break;
                case "REST_SITE":
                    state.RestSite = RestSiteStateBuilder.Build();
                    break;
                case "TREASURE":
                    state.Treasure = TreasureStateBuilder.Build();
                    break;
                case "SHOP":
                    state.Shop = ShopStateBuilder.Build();
                    break;
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
    ///     Checks pre-run screens (menu / submenus) first, then in-run screens
    ///     grouped by: combat → map → overlays → rooms → game over.
    /// </summary>
    public static string DetectScreen()
    {
        // --- Pre-run: main menu and its submenu stack ---
        if (UiUtils.FindCharacterSelectScreen() != null) return "CHARACTER_SELECT";
        if (UiUtils.FindSingleplayerSubmenu() != null) return "SINGLEPLAYER_SUBMENU";
        if (UiUtils.FindMainMenu() != null) return "MENU";

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
}