using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>choose_game_mode</c> CLI command.
///     Selects a game mode (standard, daily, custom) from the singleplayer submenu,
///     which navigates to the corresponding screen (character select, daily run, or custom run).
/// </summary>
/// <remarks>
///     <para><b>CLI command:</b> <c>sts2 choose_game_mode &lt;mode&gt;</c></para>
///     <para><b>Scene:</b> Singleplayer submenu (after clicking Singleplayer on main menu).</para>
/// </remarks>
public static class ChooseGameModeHandler
{
    private static readonly ModLogger Logger = new("ChooseGameModeHandler");

    /// <summary>
    ///     Handles the choose_game_mode request.
    /// </summary>
    public static object HandleRequest(Request request)
    {
        var mode = request.Id?.ToLower();
        Logger.Info($"Requested to choose game mode: {mode}");

        if (string.IsNullOrEmpty(mode))
        {
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Game mode is required (standard, daily, custom)" };
        }

        if (mode != "standard" && mode != "daily" && mode != "custom")
        {
            return new { ok = false, error = "INVALID_GAME_MODE", message = $"Invalid game mode: {mode}. Valid modes: standard, daily, custom" };
        }

        return Execute(mode);
    }

    /// <summary>
    ///     Clicks the corresponding button on the singleplayer submenu.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    private static object Execute(string mode)
    {
        // Guard: Must be on the SINGLEPLAYER_SUBMENU screen
        var currentScreen = StateHandler.DetectScreen();
        if (currentScreen != "SINGLEPLAYER_SUBMENU")
        {
            Logger.Warning($"Cannot choose game mode: not on singleplayer submenu (current: {currentScreen})");
            return new { ok = false, error = "NOT_ON_SINGLEPLAYER_SUBMENU", message = $"Not on singleplayer submenu screen (current: {currentScreen})" };
        }

        try
        {
            // Find the singleplayer submenu via ScreenUtils
            var submenu = ScreenUtils.FindSingleplayerSubmenu();
            if (submenu == null)
            {
                Logger.Error("NSingleplayerSubmenu not found");
                return new { ok = false, error = "UI_NOT_FOUND", message = "Singleplayer submenu not found" };
            }

            // Find the button for the requested mode
            var buttonName = mode switch
            {
                "standard" => "StandardButton",
                "daily" => "DailyButton",
                "custom" => "CustomRunButton",
                _ => null
            };

            if (buttonName == null)
            {
                return new { ok = false, error = "INVALID_GAME_MODE", message = $"Invalid game mode: {mode}" };
            }

            var button = submenu.GetNodeOrNull<NButton>(buttonName);
            if (button == null)
            {
                Logger.Error($"Button not found: {buttonName}");
                return new { ok = false, error = "BUTTON_NOT_FOUND", message = $"Button not found: {buttonName}" };
            }

            if (!button.IsEnabled)
            {
                Logger.Warning($"Button is disabled: {buttonName} (game mode not unlocked)");
                return new { ok = false, error = "MODE_NOT_UNLOCKED", message = $"Game mode '{mode}' is not unlocked yet" };
            }

            // Click the button via EmitSignal
            Logger.Info($"Clicking {buttonName}");
            button.EmitSignal(NClickableControl.SignalName.Released, button);

            // Wait a moment for the UI transition
            Thread.Sleep(100);

            Logger.Info($"Game mode '{mode}' selected successfully");
            return new { ok = true, data = new { action = "CHOOSE_GAME_MODE", mode } };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to choose game mode: {ex.Message}");
            return new { ok = false, error = "ACTION_FAILED", message = $"Failed to choose game mode: {ex.Message}" };
        }
    }
}
