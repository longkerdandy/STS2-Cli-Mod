using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using STS2.Cli.Mod.Models.Message;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles character selection on the character select screen.
/// </summary>
public static class SelectCharacterHandler
{
    private static readonly ModLogger Logger = new("SelectCharacterHandler");

    /// <summary>
    ///     Handles the select_character request.
    ///     Validates parameters and delegates to Execute.
    /// </summary>
    public static object HandleRequest(Request request)
    {
        if (string.IsNullOrEmpty(request.Id))
        {
            Logger.Warning("select_character requested with no character ID");
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Character ID is required" };
        }

        Logger.Info($"Requested to select character: {request.Id}");
        return Execute(request.Id);
    }

    /// <summary>
    ///     Selects a character by calling its Select() method.
    /// </summary>
    /// <param name="characterId">The character identifier (e.g., "ironclad", "silent").</param>
    /// <returns>Response object indicating success or failure.</returns>
    /// <remarks>
    ///     Must be called on the Godot main thread (PipeServer handles dispatching).
    /// </remarks>
    private static object Execute(string characterId)
    {
        // Guard: Must be on the character select screen
        var screen = CharacterSelectHelper.FindScreen();
        if (screen == null)
        {
            Logger.Warning("SelectCharacter requested but not on character select screen");
            return new
            {
                ok = false,
                error = "NOT_IN_CHARACTER_SELECT",
                message = "Not on character select screen"
            };
        }

        // Find the character button container
        var buttonContainer = screen.GetNodeOrNull<Control>("CharSelectButtons/ButtonContainer");
        if (buttonContainer == null)
        {
            Logger.Error("Character button container not found");
            return new
            {
                ok = false,
                error = "UI_NOT_FOUND",
                message = "Character button container not found"
            };
        }

        // Find all character buttons
        var buttons = UiHelper.FindAll<NCharacterSelectButton>(buttonContainer);

        // Find the target button
        NCharacterSelectButton? targetBtn = null;
        foreach (var btn in buttons)
        {
            var model = CharacterSelectHelper.GetCharacterModel(btn);
            if (model?.Id.Entry.Equals(characterId, StringComparison.OrdinalIgnoreCase) == true)
            {
                targetBtn = btn;
                break;
            }
        }

        if (targetBtn == null)
        {
            Logger.Warning($"Character '{characterId}' not found");
            return new
            {
                ok = false,
                error = "CHARACTER_NOT_FOUND",
                message = $"Character '{characterId}' not found"
            };
        }

        // Check if the character is locked
        if (CharacterSelectHelper.GetIsLocked(targetBtn))
        {
            Logger.Warning($"Character '{characterId}' is locked");
            return new
            {
                ok = false,
                error = "CHARACTER_LOCKED",
                message = $"Character '{characterId}' is locked"
            };
        }

        // Call Select() which triggers the character selection via the delegate
        // NCharacterSelectButton.Select() calls _delegate.SelectCharacter(this, _character)
        Logger.Info($"Selecting character: {characterId}");
        targetBtn.Select();

        return new
        {
            ok = true,
            data = new { character_id = characterId }
        };
    }
}