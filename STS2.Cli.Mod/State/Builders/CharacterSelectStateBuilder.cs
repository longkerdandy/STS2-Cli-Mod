using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds <see cref="CharacterSelectStateDto" /> from the current <see cref="NCharacterSelectScreen" />.
/// </summary>
public static class CharacterSelectStateBuilder
{
    private static readonly ModLogger Logger = new("CharacterSelectStateBuilder");

    /// <summary>
    ///     Builds the character selection state from the current <see cref="NCharacterSelectScreen" />.
    ///     Returns null if the screen is not found.
    /// </summary>
    public static CharacterSelectStateDto? Build()
    {
        try
        {
            var screen = UiUtils.FindCharacterSelectScreen();
            if (screen == null)
            {
                Logger.Warning("NCharacterSelectScreen not found");
                return null;
            }

            var buttonContainer = screen.GetNodeOrNull<Control>("CharSelectButtons/ButtonContainer");
            if (buttonContainer == null)
            {
                Logger.Warning("Character button container not found");
                return null;
            }

            var buttons = UiUtils.FindAll<NCharacterSelectButton>(buttonContainer);
            var characters = new List<CharacterOptionDto>();
            string? selectedCharacter = null;
            var selectedButton = UiUtils.GetPrivateField<NCharacterSelectButton>(screen, "_selectedButton");

            foreach (var btn in buttons)
            {
                var character = btn.Character;

                var isSelected = btn == selectedButton;
                if (isSelected)
                    selectedCharacter = character.Id.Entry;

                characters.Add(new CharacterOptionDto
                {
                    CharacterId = character.Id.Entry,
                    CharacterName = StripGameTags(character.Title.GetFormattedText()),
                    IsLocked = btn.IsLocked,
                    IsSelected = isSelected
                });
            }

            var ascensionPanel = screen.GetNodeOrNull<NAscensionPanel>("%AscensionPanel");
            var currentAsc = ascensionPanel?.Ascension ?? 0;
            var maxAsc = ascensionPanel != null
                ? UiUtils.GetPrivateFieldValue<int>(ascensionPanel, "_maxAscension") ?? 20
                : 20;

            return new CharacterSelectStateDto
            {
                AvailableCharacters = characters,
                SelectedCharacter = selectedCharacter,
                CurrentAscension = currentAsc,
                MaxAscension = maxAsc,
                CanEmbark = selectedCharacter != null
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build character select state: {ex.Message}");
            return null;
        }
    }
}