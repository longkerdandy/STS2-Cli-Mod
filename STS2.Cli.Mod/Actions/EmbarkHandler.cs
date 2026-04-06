using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>embark</c> CLI command.
///     Clicks the Embark button to start the game from character select.
/// </summary>
/// <remarks>
///     <para>
///         <b>CLI command:</b> <c>sts2 embark</c>
///     </para>
///     <para><b>Scene:</b> Character select screen, after selecting a character and ascension level.</para>
/// </remarks>
public static class EmbarkHandler
{
    private static readonly ModLogger Logger = new("EmbarkHandler");

    /// <summary>
    ///     Clicks the Embark button to start the game.
    ///     Validates the current screen state and returns a response indicating success or failure.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    public static object Execute()
    {
        Logger.Info("Requested to embark");

        // Guard: Must be on the character select screen
        var screen = UiUtils.FindCharacterSelectScreen();
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
        var selectedBtn = UiUtils.GetPrivateField<NCharacterSelectButton>(screen, "_selectedButton");
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

        // Check if the button is enabled
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

        // Click embark via ForceClick, which emits the Released signal
        Logger.Info("Clicking embark button");
        embarkBtn.ForceClick();

        return new
        {
            ok = true,
            data = new { embarked = true }
        };
    }
}