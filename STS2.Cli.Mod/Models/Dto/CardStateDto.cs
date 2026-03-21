using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.Dto;

/// <summary>
///     Card state DTO representing a single card in the hand.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class CardStateDto
{
    /// <summary>
    ///     Card index in hand (0-based).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Card model ID (e.g., "STRIKE_IRONCLAD", "DEFEND_SILENT").
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     Localized display name (includes "+" suffix if upgraded).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Card description with dynamic values resolved and formatting tags stripped.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    ///     Card type: Attack, Skill, Power, Status, Curse, Quest.
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    ///     Card rarity: Basic, Common, Uncommon, Rare, Ancient, Event, Token, Status, Curse, Quest.
    /// </summary>
    public required string Rarity { get; set; }

    /// <summary>
    ///     Targeting mode: None, Self, AnyEnemy, AllEnemies, RandomEnemy, AnyAlly, etc.
    ///     Cards with AnyEnemy require a target when played.
    /// </summary>
    public required string TargetType { get; set; }

    /// <summary>
    ///     Energy cost to play (-1 for X-cost cards that consume all remaining energy).
    /// </summary>
    public int Cost { get; set; }

    /// <summary>
    ///     True if the card can be played right now.
    /// </summary>
    public bool CanPlay { get; set; }

    /// <summary>
    ///     Reason why card cannot be played, if applicable (e.g., "EnergyCostTooHigh").
    ///     Null when the card is playable.
    /// </summary>
    public string? UnplayableReason { get; set; }

    /// <summary>
    ///     True if the card is upgraded.
    /// </summary>
    public bool IsUpgraded { get; set; }

    /// <summary>
    ///     Active card keywords: Exhaust, Ethereal, Innate, Retain, Sly, Eternal, Unplayable.
    ///     Empty list if no keywords are active.
    /// </summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>
    ///     Preview damage value after all modifiers (strength, powers, relics, enchantments).
    ///     Null if the card does not deal damage.
    /// </summary>
    public int? Damage { get; set; }

    /// <summary>
    ///     Preview block value after all modifiers (dexterity, powers, relics, enchantments).
    ///     Null if the card does not grant block.
    /// </summary>
    public int? Block { get; set; }
}
