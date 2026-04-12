using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using STS2.Cli.Mod.Actions.Utils;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>continue_run</c> CLI command.
///     Clicks the Continue button on the main menu to load a saved run,
///     then waits for the run to load and the destination screen to appear before returning.
/// </summary>
/// <remarks>
///     <para>
///         <b>CLI command:</b> <c>sts2 continue_run</c>
///     </para>
///     <para><b>Scene:</b> Main menu, when a saved run exists.</para>
///     <para>
///         This is an async handler because loading a saved run involves fade-out,
///         asset loading, and scene transition across multiple frames.
///     </para>
/// </remarks>
public static class ContinueRunHandler
{
    private static readonly ModLogger Logger = new("ContinueRunHandler");

    /// <summary>
    ///     Clicks the Continue button, waits for the run to load and the game screen to be ready.
    ///     Validates the current screen state and saved run existence.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    public static async Task<object> ExecuteAsync()
    {
        Logger.Info("Requested to continue run");

        // Guard: Must be on the MENU screen
        var currentScreen = StateHandler.DetectScreen();
        if (currentScreen != "MENU")
        {
            Logger.Warning($"Cannot continue run: not on menu screen (current: {currentScreen})");
            return new
            {
                ok = false, error = "NOT_ON_MENU", message = $"Not on main menu screen (current: {currentScreen})"
            };
        }

        // Guard: Must have a saved run
        var saveManager = SaveManager.Instance;
        if (!saveManager.HasRunSave)
        {
            Logger.Warning("Cannot continue run: no saved run exists");
            return new { ok = false, error = "NO_SAVED_RUN", message = "No saved run exists. Use new_run instead." };
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

            // Find the Continue button
            var continueButton = mainMenu.GetNodeOrNull<NMainMenuTextButton>("MainMenuTextButtons/ContinueButton");
            if (continueButton == null)
            {
                Logger.Error("Continue button not found");
                return new { ok = false, error = "BUTTON_NOT_FOUND", message = "Continue button not found" };
            }

            if (!continueButton.IsEnabled)
            {
                Logger.Warning("Continue button is disabled");
                return new { ok = false, error = "BUTTON_DISABLED", message = "Continue button is disabled" };
            }

            // Click the Continue button via EmitSignal
            Logger.Info("Clicking Continue button");
            continueButton.EmitSignal(NClickableControl.SignalName.Released, continueButton);

            // Wait for the run to load (RunManager.Instance.IsInProgress becomes true)
            await Task.Delay(ActionUtils.PostClickDelayMs);
            var runLoaded = await ActionUtils.PollUntilAsync(
                () => RunManager.Instance.IsInProgress,
                ActionUtils.ActionTimeoutMs);

            if (!runLoaded)
            {
                Logger.Warning("Timed out waiting for run to load after continue_run");
                return new
                {
                    ok = true,
                    data = new { action = "CONTINUE_RUN" },
                    warning = "Timed out waiting for run to load"
                };
            }

            // Run data is loaded, but the game screen may not be ready yet.
            // Wait for the actual destination screen to appear (not MENU or UNKNOWN).
            var screenReady = await ActionUtils.PollUntilAsync(
                () =>
                {
                    var s = StateHandler.DetectScreen();
                    return s != "MENU" && s != "UNKNOWN";
                },
                ActionUtils.ActionTimeoutMs);

            var screen = StateHandler.DetectScreen();
            if (!screenReady)
            {
                Logger.Warning($"Timed out waiting for game screen after continue_run (current: {screen})");
                return new
                {
                    ok = true,
                    data = new { action = "CONTINUE_RUN", screen },
                    warning = "Timed out waiting for game screen to appear"
                };
            }

            Logger.Info($"Run loaded successfully, landed on {screen} screen");
            return new { ok = true, data = new { action = "CONTINUE_RUN", screen } };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to continue run: {ex.Message}");
            return new { ok = false, error = "ACTION_FAILED", message = $"Failed to continue run: {ex.Message}" };
        }
    }
}