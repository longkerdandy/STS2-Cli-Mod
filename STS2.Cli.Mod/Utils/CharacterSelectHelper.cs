using Godot;
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
    ///     Gets the CharacterModel from a character select button via reflection.
    /// </summary>
    public static MegaCrit.Sts2.Core.Models.CharacterModel? GetCharacterModel(NCharacterSelectButton btn)
    {
        try
        {
            // Try field first
            var field = typeof(NCharacterSelectButton).GetField("_characterModel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                return field.GetValue(btn) as MegaCrit.Sts2.Core.Models.CharacterModel;

            // Try property
            var prop = typeof(NCharacterSelectButton).GetProperty("CharacterModel");
            if (prop != null)
                return prop.GetValue(btn) as MegaCrit.Sts2.Core.Models.CharacterModel;

            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get character model: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the locked status from a character select button.
    /// </summary>
    public static bool GetIsLocked(NCharacterSelectButton btn)
    {
        try
        {
            var field = typeof(NCharacterSelectButton).GetField("_isLocked",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                return (bool)(field.GetValue(btn) ?? false);

            // Try IsLocked property
            var prop = typeof(NCharacterSelectButton).GetProperty("IsLocked");
            if (prop != null)
                return (bool)(prop.GetValue(btn) ?? false);

            return false;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get locked status: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Gets the selected character button from the screen via reflection.
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
