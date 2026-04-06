using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>select_character</c> CLI command.
///     Selects a character on the character select screen by character ID.
/// </summary>
/// <remarks>
///     <para>
///         <b>CLI command:</b> <c>sts2 select_character &lt;character_id&gt;</c>
///     </para>
///     <para><b>Scene:</b> Character select screen.</para>
/// </remarks>
public static class SelectCharacterHandler
{
    private static readonly ModLogger Logger = new("SelectCharacterHandler");

    /// <summary>
    ///     Selects a character by calling its Select() method.
    ///     Validates parameters and current screen state.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    public static Task<object> ExecuteAsync(Request request)
    {
        return Task.FromResult<object>(ExecuteCore(request));
    }

    private static object ExecuteCore(Request request)
    {
        if (string.IsNullOrEmpty(request.Id))
        {
            Logger.Warning("select_character requested with no character ID");
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Character ID is required" };
        }

        var characterId = request.Id;
        Logger.Info($"Requested to select character: {characterId}");

        // Guard: Must be on the character select screen
        var screen = UiUtils.FindCharacterSelectScreen();
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
        var buttons = UiUtils.FindAll<NCharacterSelectButton>(buttonContainer);

        // Find the target button
        NCharacterSelectButton? targetBtn = null;
        foreach (var btn in buttons)
        {
            var model = GetCharacterModel(btn);
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
        if (GetIsLocked(targetBtn))
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

    /// <summary>
    ///     Gets the CharacterModel from a character select button.
    ///     NCharacterSelectButton.Character is a public property.
    /// </summary>
    private static CharacterModel? GetCharacterModel(NCharacterSelectButton btn)
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
    private static bool GetIsLocked(NCharacterSelectButton btn)
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
}