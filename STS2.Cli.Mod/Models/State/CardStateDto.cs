using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.State;

/// <summary>
///     Card state DTO representing a single card in the hand.
///     Fields are ordered to match the CardModel source layout:
///     Index (DTO) → Identity → Classification → Cost → Keywords/Tags/DynamicVars →
///     Enchantment/Affliction → Upgrade → Playability.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "CollectionNeverQueried.Global")]
public class CardStateDto
{
    // -- DTO context --

    /// <summary>
    ///     Card index in hand (0-based).
    /// </summary>
    public int Index { get; set; }

    // -- Identity & Metadata (CardModel lines 90-109) --

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

    // -- Core Classification (CardModel lines 266-411) --

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

    // -- Cost System (CardModel lines 316-398) --

    /// <summary>
    ///     Energy cost to play (-1 for X-cost cards that consume all remaining energy).
    /// </summary>
    public int Cost { get; set; }

    /// <summary>
    ///     Star cost to play (Regent-specific secondary resource).
    ///     Null if the card has no star cost. -1 for X-star cards that consume all remaining stars.
    /// </summary>
    public int? StarCost { get; set; }

    // -- Keywords, Tags & DynamicVars (CardModel lines 413-447) --

    /// <summary>
    ///     Active card keywords: Exhaust, Ethereal, Innate, Retain, Sly, Eternal, Unplayable.
    ///     Empty list if no keywords are active.
    /// </summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>
    ///     Mechanical tags for synergy purposes: Strike, Defend, Minion, OstyAttack, Shiv.
    ///     Empty list if no tags. Unlike keywords, tags are not displayed on the card.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    ///     Preview damage value after all modifiers (strength, powers, relics, enchantments).
    ///     Null if the card does not deal damage.
    /// </summary>
    public int? Damage { get; set; }

    /// <summary>
    ///     Preview block value after all modifiers (dexterity, powers, relics, enchantments).
    ///     Null if the card does not grant any block.
    /// </summary>
    public int? Block { get; set; }

    // -- Enchantment & Affliction (CardModel lines 512-514) --

    /// <summary>
    ///     Enchantment model ID (e.g., "SHARP", "NIMBLE", "GLAM").
    ///     Null if the card has no enchantment.
    /// </summary>
    public string? Enchantment { get; set; }

    /// <summary>
    ///     Affliction model ID (e.g., "HEXED", "BOUND", "GALVANIZED").
    ///     Null if the card has no affliction.
    /// </summary>
    public string? Affliction { get; set; }

    // -- Upgrade System (CardModel lines 600-621) --

    /// <summary>
    ///     True if the card is upgraded.
    /// </summary>
    public bool IsUpgraded { get; set; }

    // -- Playability & State (CardModel line 650+) --

    /// <summary>
    ///     True if the card can be played right now.
    /// </summary>
    public bool CanPlay { get; set; }

    /// <summary>
    ///     Reason why the card cannot be played, if applicable (e.g., "EnergyCostTooHigh").
    ///     Null when the card is playable.
    /// </summary>
    public string? UnplayableReason { get; set; }
}
