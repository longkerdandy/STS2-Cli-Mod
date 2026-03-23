using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.Dto;

/// <summary>
///     Character selection screen state DTO containing available characters and selection status.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class CharacterSelectStateDto
{
    /// <summary>
    ///     Available characters for selection.
    /// </summary>
    public List<CharacterOptionDto> AvailableCharacters { get; set; } = [];

    /// <summary>
    ///     Currently selected character ID (null if none selected).
    /// </summary>
    public string? SelectedCharacter { get; set; }

    /// <summary>
    ///     Current ascension level.
    /// </summary>
    public int CurrentAscension { get; set; }

    /// <summary>
    ///     Maximum available ascension level.
    /// </summary>
    public int MaxAscension { get; set; }

    /// <summary>
    ///     Whether the player can click Embark (character selected).
    /// </summary>
    public bool CanEmbark { get; set; }
}

/// <summary>
///     Individual character option in the character selection screen.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class CharacterOptionDto
{
    /// <summary>
    ///     Character identifier (e.g., "ironclad", "silent").
    /// </summary>
    public string CharacterId { get; set; } = string.Empty;

    /// <summary>
    ///     Localized character name.
    /// </summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>
    ///     Whether the character is locked.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    ///     Whether this character is currently selected.
    /// </summary>
    public bool IsSelected { get; set; }
}
