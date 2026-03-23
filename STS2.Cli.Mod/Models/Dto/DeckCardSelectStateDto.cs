using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.Dto;

/// <summary>
///     Deck card selection screen state DTO.
///     Contains information about cards available for selection from a grid-based card selection overlay.
///     Covers: card removal (Precise Scissors), upgrade, transform, enchant, and generic grid selections.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class DeckCardSelectStateDto
{
    /// <summary>
    ///     Type of selection: remove, upgrade, transform, enchant, or generic.
    /// </summary>
    public string SelectionType { get; set; } = "unknown";

    /// <summary>
    ///     Prompt text shown to the player (e.g., "Choose a card to remove.").
    /// </summary>
    public string? Prompt { get; set; }

    /// <summary>
    ///     Minimum number of cards that must be selected.
    /// </summary>
    public int MinSelect { get; set; }

    /// <summary>
    ///     Maximum number of cards that can be selected.
    /// </summary>
    public int MaxSelect { get; set; }

    /// <summary>
    ///     Whether the player can cancel this selection (close button available).
    /// </summary>
    public bool Cancelable { get; set; }

    /// <summary>
    ///     List of cards available for selection.
    /// </summary>
    public List<SelectableCardDto> Cards { get; set; } = [];
}
