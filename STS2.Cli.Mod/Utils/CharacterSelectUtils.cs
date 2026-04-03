using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Utility methods for character select screen operations.
/// </summary>
public static class CharacterSelectUtils
{
    private static readonly ModLogger Logger = new("CharacterSelectUtils");

    /// <summary>
    ///     Finds the Character Select screen via the main menu's submenu stack.
    ///     Delegates to <see cref="UiUtils.FindCharacterSelectScreen" />.
    /// </summary>
    public static NCharacterSelectScreen? FindScreen()
    {
        return UiUtils.FindCharacterSelectScreen();
    }

    /// <summary>
    ///     Gets the CharacterModel from a character select button.
    ///     NCharacterSelectButton.Character is a public property.
    /// </summary>
    public static CharacterModel? GetCharacterModel(NCharacterSelectButton btn)
    {
        try
        {
            return btn.Character;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get character model: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the locked status from a character select button.
    ///     NCharacterSelectButton.IsLocked is a public property.
    /// </summary>
    public static bool GetIsLocked(NCharacterSelectButton btn)
    {
        try
        {
            return btn.IsLocked;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get locked status: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Gets the selected character button from the screen via reflection.
    ///     NCharacterSelectScreen._selectedButton is a private field.
    /// </summary>
    public static NCharacterSelectButton? GetSelectedButton(NCharacterSelectScreen screen)
    {
        try
        {
            var field = typeof(NCharacterSelectScreen).GetField("_selectedButton",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(screen) as NCharacterSelectButton;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get selected button: {ex.Message}");
            return null;
        }
    }
}
