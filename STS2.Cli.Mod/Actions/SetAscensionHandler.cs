using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles setting ascension level on the character select screen.
/// </summary>
public static class SetAscensionHandler
{
    private static readonly ModLogger Logger = new("SetAscensionHandler");

    /// <summary>
    ///     Sets the ascension level.
    /// </summary>
    /// <param name="level">The ascension level to set (0-20).</param>
    /// <returns>Response object indicating success or failure.</returns>
    public static object Execute(int level)
    {
        return MainThreadExecutor.RunOnMainThread<object>(() =>
        {
            // Guard: Must be on character select screen
            var screen = CharacterSelectHelper.FindScreen();
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

            // Get max ascension
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

            // Set ascension level via reflection
            Logger.Info($"Setting ascension level to: {level}");
            if (!SetAscensionLevel(ascensionPanel, level))
            {
                return new
                {
                    ok = false,
                    error = "SET_ASCENSION_FAILED",
                    message = "Failed to set ascension level"
                };
            }

            return new
            {
                ok = true,
                data = new { ascension_level = level }
            };
        });
    }

    /// <summary>
    ///     Gets the maximum ascension level from the panel.
    /// </summary>
    private static int GetMaxAscension(NAscensionPanel panel)
    {
        try
        {
            // Try MaxLevel property
            var prop = typeof(NAscensionPanel).GetProperty("MaxLevel");
            if (prop != null)
                return (int)(prop.GetValue(panel) ?? 20);

            // Try field
            var field = typeof(NAscensionPanel).GetField("_maxLevel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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

    /// <summary>
    ///     Sets the ascension level on the panel.
    /// </summary>
    private static bool SetAscensionLevel(NAscensionPanel panel, int level)
    {
        try
        {
            // Try SetAscensionLevel method
            var method = typeof(NAscensionPanel).GetMethod("SetAscensionLevel");
            if (method != null)
            {
                method.Invoke(panel, new object[] { level });
                return true;
            }

            // Try setting CurrentLevel property
            var prop = typeof(NAscensionPanel).GetProperty("CurrentLevel");
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(panel, level);
                return true;
            }

            // Try _currentLevel field
            var field = typeof(NAscensionPanel).GetField("_currentLevel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(panel, level);
                return true;
            }

            Logger.Error("Could not find way to set ascension level");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to set ascension level: {ex.Message}");
            return false;
        }
    }
}
