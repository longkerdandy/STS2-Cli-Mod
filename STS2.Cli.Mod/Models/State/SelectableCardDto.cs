using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Individual card available for selection in a card selection screen.
///     Shared by tri-select and grid card select screens.
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
