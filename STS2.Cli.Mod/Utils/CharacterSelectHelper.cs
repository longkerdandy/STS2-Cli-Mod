using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Helper methods for character select screen operations.
/// </summary>
public static class CharacterSelectHelper
{
    private static readonly ModLogger Logger = new("CharacterSelectHelper");

    /// <summary>
    ///     Finds the Character Select screen in the scene tree.
    /// </summary>
    public static NCharacterSelectScreen? FindScreen()
    {
        try
        {
            // Try to find through NGame instance
            var game = NGame.Instance;
            if (game == null) return null;

            // Search in the current scene
            var currentScene = game.RootSceneContainer?.CurrentScene;
            if (currentScene != null)
            {
                var charSelect = UiHelper.FindFirst<NCharacterSelectScreen>(currentScene);
                if (charSelect != null && charSelect.IsInsideTree())
                    return charSelect;
            }

            // Search in the entire scene tree
            var root = game.GetTree()?.Root;
            if (root != null)
            {
                return UiHelper.FindFirst<NCharacterSelectScreen>(root);
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to find character select screen: {ex.Message}");
            return null;
        }
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
