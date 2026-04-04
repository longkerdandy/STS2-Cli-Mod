using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>new_run</c> CLI command.
///     Clicks the Singleplayer button on the main menu to start a new game.
///     If NumberOfRuns > 0, opens the singleplayer submenu (Standard/Daily/Custom).
///     If NumberOfRuns == 0 (first game ever), goes directly to character select.
/// </summary>
/// <remarks>
///     <para><b>CLI command:</b> <c>sts2 new_run</c></para>
///     <para><b>Scene:</b> Main menu, when no saved run exists.</para>
/// </remarks>
public static class NewRunHandler
{
    private static readonly ModLogger Logger = new("NewRunHandler");

    /// <summary>
    ///     Handles the new_run request.
    /// </summary>
    public static object HandleRequest()
    {
        Logger.Info("Requested to start new run");
        return Execute();
    }

    /// <summary>
    ///     Clicks the Singleplayer button on the main menu.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    private static object Execute()
    {
        // Guard: Must be on the MENU screen
        var currentScreen = StateHandler.DetectScreen();
        if (currentScreen != "MENU")
        {
            Logger.Warning($"Cannot start new run: not on menu screen (current: {currentScreen})");
            return new { ok = false, error = "NOT_ON_MENU", message = $"Not on main menu screen (current: {currentScreen})" };
        }

        // Guard: Must NOT have a saved run (abandon first)
        var saveManager = MegaCrit.Sts2.Core.Saves.SaveManager.Instance;
        if (saveManager.HasRunSave)
        {
            Logger.Warning("Cannot start new run: a saved run exists. Use abandon_run first.");
            return new { ok = false, error = "RUN_SAVE_EXISTS", message = "A saved run exists. Use abandon_run to abandon it first, then use new_run." };
        }

        try
        {
            // Find the NMainMenu instance
            var mainMenu = NGame.Instance?.MainMenu;
            if (mainMenu == null)
            {
                Logger.Error("NMainMenu instance not found");
                return new { ok = false, error = "UI_NOT_FOUND", message = "Main menu not found" };
            }

            // Find the Singleplayer button
            var singleplayerButton = mainMenu.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/SingleplayerButton");
            if (singleplayerButton == null)
            {
                Logger.Error("Singleplayer button not found");
                return new { ok = false, error = "BUTTON_NOT_FOUND", message = "Singleplayer button not found" };
            }

            if (!singleplayerButton.IsVisible())
            {
                Logger.Warning("Singleplayer button is not visible (saved run may exist)");
                return new { ok = false, error = "BUTTON_NOT_FOUND", message = "Singleplayer button is not visible" };
            }

            // Click the Singleplayer button via EmitSignal
            Logger.Info("Clicking Singleplayer button");
            singleplayerButton.EmitSignal(NClickableControl.SignalName.Released, singleplayerButton);

            // Wait a moment for the UI transition
            Thread.Sleep(100);

            Logger.Info("New run initiated successfully");
            return new { ok = true, data = new { action = "NEW_RUN" } };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to start new run: {ex.Message}");
            return new { ok = false, error = "ACTION_FAILED", message = $"Failed to start new run: {ex.Message}" };
        }
    }
}
