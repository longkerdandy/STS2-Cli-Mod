using System.Reflection;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>set_ascension</c> CLI command.
///     Sets the ascension level on the character select screen.
/// </summary>
/// <remarks>
///     <para><b>CLI command:</b> <c>sts2 set_ascension &lt;level&gt;</c></para>
///     <para><b>Scene:</b> Character select screen.</para>
/// </remarks>
public static class SetAscensionHandler
{
    private static readonly ModLogger Logger = new("SetAscensionHandler");

    /// <summary>
    ///     Sets the ascension level.
    ///     Validates parameters and current screen state.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    public static object Execute(Request request)
    {
        if (request.Args == null || request.Args.Length == 0)
        {
            Logger.Warning("set_ascension requested with no arguments");
            return new
            {
                ok = false,
                error = "MISSING_ARGUMENT",
                message = "Ascension level is required (e.g., 'sts2 set_ascension 10')"
            };
        }

        var level = request.Args[0];
        Logger.Info($"Requested to set ascension level: {level}");

        // Guard: Must be on the character select screen
        var screen = UiUtils.FindCharacterSelectScreen();
        if (screen == null)
        {
            Logger.Warning("SetAscension requested but not on character select screen");
            return new
            {
                ok = false,
                error = "NOT_IN_CHARACTER_SELECT",
                message = "Not on character select screen"
            };
        }

        // Get ascension panel
        var ascensionPanel = screen.GetNodeOrNull<NAscensionPanel>("%AscensionPanel");
        if (ascensionPanel == null)
        {
            Logger.Error("Ascension panel not found");
            return new
            {
                ok = false,
                error = "UI_NOT_FOUND",
                message = "Ascension panel not found"
            };
        }

        // Get max ascension from private field _maxAscension
        var maxAscension = GetMaxAscension(ascensionPanel);

        if (level < 0 || level > maxAscension)
        {
            Logger.Warning($"Invalid ascension level: {level} (max: {maxAscension})");
            return new
            {
                ok = false,
                error = "INVALID_ASCENSION_LEVEL",
                message = $"Ascension level must be between 0 and {maxAscension}"
            };
        }

        // SetAscensionLevel is a public method on NAscensionPanel
        Logger.Info($"Setting ascension level to: {level}");
        ascensionPanel.SetAscensionLevel(level);

        return new
        {
            ok = true,
            data = new { ascension_level = level }
        };
    }

    /// <summary>
    ///     Gets the maximum ascension level from the panel's private _maxAscension field.
    /// </summary>
    private static int GetMaxAscension(NAscensionPanel panel)
    {
        try
        {
            var field = typeof(NAscensionPanel).GetField("_maxAscension",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                return (int)(field.GetValue(panel) ?? 20);

            return 20; // Default
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get max ascension: {ex.Message}");
            return 20;
        }
    }
}
