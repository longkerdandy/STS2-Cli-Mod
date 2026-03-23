using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.State;

/// <summary>
///     Potion card selection screen state DTO.
///     Contains information about cards available for selection when a potion opens a selection screen.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class PotionSelectionStateDto
{
    /// <summary>
    ///     Type of selection: choose_from_pool_attack, choose_from_pool_skill, choose_from_pool_power,
    ///     choose_from_pool_colorless, choose_from_discard, choose_from_draw, choose_from_hand, etc.
    /// </summary>
    public string SelectionType { get; set; } = "unknown";

    /// <summary>
    ///     Minimum number of cards that must be selected.
    /// </summary>
    public int MinSelect { get; set; }

    /// <summary>
    ///     Maximum number of cards that can be selected.
    /// </summary>
    public int MaxSelect { get; set; }

    /// <summary>
    ///     Whether the player can skip this selection (select 0 cards).
    /// </summary>
    public bool CanSkip { get; set; }

    /// <summary>
    ///     List of cards available for selection.
    /// </summary>
    public List<SelectableCardDto> Cards { get; set; } = [];
}

/// <summary>
///     Individual card available for selection in a potion selection screen.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class SelectableCardDto
{
    /// <summary>
    ///     Index of the card in the selection screen (0-based).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Card ID (e.g., "STRIKE_IRONCLAD").
    /// </summary>
    public string CardId { get; set; } = string.Empty;

    /// <summary>
    ///     Localized card name.
    /// </summary>
    public string CardName { get; set; } = string.Empty;

    /// <summary>
    ///     Card type: Attack, Skill, Power, Status, Curse.
    /// </summary>
    public string? CardType { get; set; }

    /// <summary>
    ///     Energy cost to play the card.
    /// </summary>
    public int? Cost { get; set; }

    /// <summary>
    ///     Card description text.
    /// </summary>
    public string? Description { get; set; }
}
