using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>abandon_run</c> CLI command.
///     Abandons the current saved run from the main menu, skipping the confirmation popup.
///     After abandoning, the menu refreshes to show the Singleplayer button instead of Continue.
/// </summary>
/// <remarks>
///     <para><b>CLI command:</b> <c>sts2 abandon_run</c></para>
///     <para><b>Scene:</b> Main menu, when a saved run exists.</para>
/// </remarks>
public static class AbandonRunHandler
{
    private static readonly ModLogger Logger = new("AbandonRunHandler");

    /// <summary>
    ///     Abandons the current saved run.
    ///     Validates the current screen state and calls <see cref="NMainMenu.AbandonRun" />.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    public static object Execute()
    {
        Logger.Info("Requested to abandon run");

        // Guard: Must be on the MENU screen
        var currentScreen = StateHandler.DetectScreen();
        if (currentScreen != "MENU")
        {
            Logger.Warning($"Cannot abandon run: not on menu screen (current: {currentScreen})");
            return new { ok = false, error = "NOT_ON_MENU", message = $"Not on main menu screen (current: {currentScreen})" };
        }

        // Guard: Must have a saved run
        var saveManager = MegaCrit.Sts2.Core.Saves.SaveManager.Instance;
        if (!saveManager.HasRunSave)
        {
            Logger.Warning("Cannot abandon run: no saved run exists");
            return new { ok = false, error = "NO_SAVED_RUN", message = "No saved run exists." };
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

            // Call AbandonRun() directly (public method on NMainMenu)
            // This skips the confirmation popup, updates progress, deletes the save, and refreshes buttons
            Logger.Info("Calling AbandonRun()");
            mainMenu.AbandonRun();

            Logger.Info("Run abandoned successfully");
            return new { ok = true, data = new { action = "ABANDON_RUN" } };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to abandon run: {ex.Message}");
            return new { ok = false, error = "ACTION_FAILED", message = $"Failed to abandon run: {ex.Message}" };
        }
    }
}
