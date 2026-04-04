using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>continue_run</c> CLI command.
///     Clicks the Continue button on the main menu to load a saved run.
/// </summary>
/// <remarks>
///     <para><b>CLI command:</b> <c>sts2 continue_run</c></para>
///     <para><b>Scene:</b> Main menu, when a saved run exists.</para>
///     <para>
///         This is an async handler because loading a saved run involves fade-out,
///         asset loading, and scene transition across multiple frames.
///     </para>
/// </remarks>
public static class ContinueRunHandler
{
    private static readonly ModLogger Logger = new("ContinueRunHandler");
    private const int PollIntervalMs = 100;
    private const int MaxWaitMs = 15000;

    /// <summary>
    ///     Handles the continue_run request.
    /// </summary>
    public static async Task<object> HandleRequestAsync()
    {
        Logger.Info("Requested to continue run");
        return await ExecuteAsync();
    }

    /// <summary>
    ///     Clicks the Continue button and waits for the run to load.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    private static async Task<object> ExecuteAsync()
    {
        // Guard: Must be on the MENU screen
        var currentScreen = StateHandler.DetectScreen();
        if (currentScreen != "MENU")
        {
            Logger.Warning($"Cannot continue run: not on menu screen (current: {currentScreen})");
            return new { ok = false, error = "NOT_ON_MENU", message = $"Not on main menu screen (current: {currentScreen})" };
        }

        // Guard: Must have a saved run
        var saveManager = MegaCrit.Sts2.Core.Saves.SaveManager.Instance;
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
            var elapsed = 0;
            while (elapsed < MaxWaitMs)
            {
                await Task.Delay(PollIntervalMs);
                elapsed += PollIntervalMs;

                if (RunManager.Instance.IsInProgress)
                {
                    Logger.Info($"Run loaded successfully after {elapsed}ms");
                    return new { ok = true, data = new { action = "CONTINUE_RUN" } };
                }
            }

            // Timeout — run may still be loading
            Logger.Warning($"Timed out waiting for run to load after {MaxWaitMs}ms");
            return new { ok = false, error = "TIMEOUT", message = $"Timed out waiting for run to load after {MaxWaitMs}ms" };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to continue run: {ex.Message}");
            return new { ok = false, error = "ACTION_FAILED", message = $"Failed to continue run: {ex.Message}" };
        }
    }
}
