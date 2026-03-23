namespace STS2.Cli.Mod.Models.Actions;

/// <summary>
///     DTO for selectable cards in potion selection screens.
/// </summary>
public class SelectableCardDto
{
    /// <summary>
    ///     Card index in the selection screen.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Card model ID (e.g., "STRIKE_IRONCLAD").
    /// </summary>
    public required string CardId { get; set; }

    /// <summary>
    ///     Localized card name.
    /// </summary>
    public required string CardName { get; set; }

    /// <summary>
    ///     Card type: Attack, Skill, Power, Status, Curse.
    /// </summary>
    public string? CardType { get; set; }

    /// <summary>
    ///     Energy cost to play (-1 for X-cost cards).
    /// </summary>
    public int? Cost { get; set; }

    /// <summary>
    ///     Card description with formatting tags stripped.
    /// </summary>
    public string? Description { get; set; }
}
