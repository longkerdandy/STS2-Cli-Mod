using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using STS2.Cli.Mod.Actions.Utils;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the return_to_menu command from the game over screen.
///     Clicks the main menu button and waits for the main menu to appear before returning.
/// </summary>
internal static class ReturnToMenuHandler
{
    private static readonly ModLogger Logger = new("ReturnToMenuHandler");

    /// <summary>
    ///     Clicks the main menu button on the game over screen, then polls until the main menu appears.
    ///     Validates the current screen state and returns a response indicating success or failure.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    public static async Task<object> ExecuteAsync()
    {
        Logger.Info("Executing return to menu");

        // Verify we're on the game over screen
        var currentScreen = StateHandler.DetectScreen();
        if (currentScreen != "GAME_OVER")
        {
            Logger.Warning($"Cannot return to menu: not on game over screen (current: {currentScreen})");
            return new
            {
                ok = false, error = "NOT_ON_GAME_OVER_SCREEN",
                message = "Return to menu is only available on the game over screen"
            };
        }

        try
        {
            // Get the game over screen from overlay stack
            var overlayStack = NOverlayStack.Instance;
            if (overlayStack == null)
            {
                Logger.Error("NOverlayStack.Instance is null");
                return new { ok = false, error = "UI_NOT_FOUND", message = "Cannot access overlay stack" };
            }

            if (overlayStack.Peek() is not NGameOverScreen gameOverScreen)
            {
                Logger.Error("Game over screen not found in overlay stack");
                return new { ok = false, error = "UI_NOT_FOUND", message = "Game over screen not found" };
            }

            // Find the main menu button
            var mainMenuButton = gameOverScreen.GetNodeOrNull<NReturnToMainMenuButton>("%MainMenuButton");
            if (mainMenuButton == null)
            {
                Logger.Error("Main menu button not found (%MainMenuButton)");
                return new
                {
                    ok = false, error = "BUTTON_NOT_FOUND", message = "Main menu button not found on game over screen"
                };
            }

            if (!mainMenuButton.IsEnabled)
            {
                Logger.Warning("Main menu button is disabled");
                return new { ok = false, error = "BUTTON_DISABLED", message = "Main menu button is disabled" };
            }

            // Click the button using EmitSignal (same pattern as other handlers)
            Logger.Info("Clicking main menu button");
            mainMenuButton.EmitSignal(NClickableControl.SignalName.Released, mainMenuButton);

            // Wait for the main menu to appear (scene unload + menu load)
            await Task.Delay(ActionUtils.PostClickDelayMs);
            var menuReady = await ActionUtils.PollUntilAsync(
                () => UiUtils.FindMainMenu() != null,
                ActionUtils.ActionTimeoutMs);

            if (!menuReady)
            {
                Logger.Warning("Timed out waiting for main menu after return_to_menu");
                return new
                {
                    ok = true,
                    data = new { action = "RETURN_TO_MENU" },
                    warning = "Timed out waiting for main menu to appear"
                };
            }

            Logger.Info("Return to menu completed successfully");
            return new { ok = true, data = new { action = "RETURN_TO_MENU", screen = "MENU" } };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to return to menu: {ex.Message}");
            return new { ok = false, error = "ACTION_FAILED", message = $"Failed to return to menu: {ex.Message}" };
        }
    }
}