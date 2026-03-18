using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.State.Dto;

/// <summary>
///     Card state DTO representing a single card in the hand.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public class CardStateDto
{
    /// <summary>
    ///     Card index in hand (0-based).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Card ID (e.g., "Strike", "Defend").
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    ///     Card display name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Energy cost (-1 for X cost).
    /// </summary>
    public int Cost { get; set; }

    /// <summary>
    ///     Display string for cost (e.g., "1", "2", "X").
    /// </summary>
    public string? CostDisplay { get; set; }

    /// <summary>
    ///     True if the card can be played now.
    /// </summary>
    public bool CanPlay { get; set; }

    /// <summary>
    ///     Reason why card cannot be played, if applicable.
    /// </summary>
    public string? UnplayableReason { get; set; }

    /// <summary>
    ///     Card description text.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Card type (Attack, Skill, Power, etc.).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    ///     True if the card is upgraded.
    /// </summary>
    public bool IsUpgraded { get; set; }
}
