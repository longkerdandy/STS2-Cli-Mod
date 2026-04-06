using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Simplified card DTO for pile listings (draw/discard/exhaust piles).
///     Excludes description to reduce payload size; use full CardStateDto for hand cards.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class PileCardDto
{
    /// <summary>
    ///     Card model ID (e.g., "STRIKE_IRONCLAD", "DEFEND_SILENT").
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     Localized display name (includes "+" suffix if upgraded).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Card type: Attack, Skill, Power, Status, Curse, Quest.
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    ///     Card rarity: Basic, Common, Uncommon, Rare, Ancient, Event, Token, Status, Curse, Quest.
    /// </summary>
    public required string Rarity { get; set; }

    /// <summary>
    ///     Energy cost to play (-1 for X-cost cards).
    /// </summary>
    public int Cost { get; set; }

    /// <summary>
    ///     Active card keywords: Exhaust, Ethereal, Innate, Retain, Sly, Eternal, Unplayable.
    /// </summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>
    ///     True if the card is upgraded.
    /// </summary>
    public bool IsUpgraded { get; set; }

    /// <summary>
    ///     Full description when --include-pile-details is enabled, null otherwise.
    /// </summary>
    public string? Description { get; set; }
}