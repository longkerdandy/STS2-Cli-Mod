using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles clicking the Embark button to start the game from character select.
/// </summary>
public static class EmbarkHandler
{
    private static readonly ModLogger Logger = new("EmbarkHandler");

    /// <summary>
    ///     Clicks the Embark button to start the game.
    /// </summary>
    /// <returns>Response object indicating success or failure.</returns>
    /// <remarks>
    ///     Must be called on the Godot main thread (PipeServer handles dispatching).
    /// </remarks>
    public static object Execute()
    {
        // Guard: Must be on character select screen
        var screen = CharacterSelectHelper.FindScreen();
        if (screen == null)
        {
            Logger.Warning("Embark requested but not on character select screen");
            return new
            {
                ok = false,
                error = "NOT_IN_CHARACTER_SELECT",
                message = "Not on character select screen"
            };
        }

        // Check if a character is selected
        var selectedBtn = CharacterSelectHelper.GetSelectedButton(screen);
        if (selectedBtn == null)
        {
            Logger.Warning("Embark requested but no character selected");
            return new
            {
                ok = false,
                error = "NO_CHARACTER_SELECTED",
                message = "No character selected"
            };
        }

        // Find embark button
        var embarkBtn = screen.GetNodeOrNull<NConfirmButton>("ConfirmButton");
        if (embarkBtn == null)
        {
            Logger.Error("Embark button not found");
            return new
            {
                ok = false,
                error = "EMBARK_BUTTON_NOT_FOUND",
                message = "Embark button not found"
            };
        }

        // Check if button is enabled
        if (!embarkBtn.IsEnabled)
        {
            Logger.Warning("Embark button is disabled");
            return new
            {
                ok = false,
                error = "EMBARK_NOT_AVAILABLE",
                message = "Embark button is not available"
            };
        }

        // Click embark via ForceClick which emits Released signal
        Logger.Info("Clicking embark button");
        embarkBtn.ForceClick();

        return new
        {
            ok = true,
            data = new { embarked = true }
        };
    }
}
