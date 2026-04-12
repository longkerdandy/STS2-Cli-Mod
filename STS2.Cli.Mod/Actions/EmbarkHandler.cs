using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using STS2.Cli.Mod.Actions.Utils;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>embark</c> CLI command.
///     Clicks the Embark button to start the game from character select,
///     then waits for the destination screen (map or Neow event) to appear before returning.
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
    ///     Clicks the Embark button to start the game, then polls until the destination screen appears.
    ///     If NeowEpoch is unlocked, the game enters the Neow Ancient event; otherwise, the map opens directly.
    ///     Validates the current screen state and returns a response indicating success or failure.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    public static async Task<object> ExecuteAsync()
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

        // Wait for the run to load: either the map screen opens (no Neow)
        // or an event room appears (Neow Ancient event when NeowEpoch is unlocked)
        await Task.Delay(ActionUtils.PostClickDelayMs);
        var screenReady = await ActionUtils.PollUntilAsync(
            () => NMapScreen.Instance is { IsOpen: true } ||
                  NEventRoom.Instance is { } er && er.IsInsideTree(),
            ActionUtils.ActionTimeoutMs);

        if (!screenReady)
        {
            Logger.Warning("Timed out waiting for map or event screen after embark");
            return new
            {
                ok = true,
                data = new { embarked = true },
                warning = "Timed out waiting for destination screen"
            };
        }

        // Detect which screen we landed on
        var screen_name = NEventRoom.Instance is { } eventRoom && eventRoom.IsInsideTree()
            ? "EVENT"
            : "MAP";
        Logger.Info($"Embark completed, landed on {screen_name} screen");
        return new
        {
            ok = true,
            data = new { embarked = true, screen = screen_name }
        };
    }
}