using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Potion state DTO representing a potion in the player's belt.
///     Null slots (empty) are excluded from the list; use Slot to identify the position.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class PotionStateDto
{
    /// <summary>
    ///     Slot index in the player's potion belt (0-based).
    ///     Gaps indicate empty slots.
    /// </summary>
    public int Slot { get; set; }

    /// <summary>
    ///     Potion model ID (e.g., "FIRE_POTION", "ENTROPIC_BREW").
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     Localized display name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Effect description with dynamic values resolved (e.g., "Deal 20 damage.").
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    ///     Rarity tier: Common, Uncommon, Rare, Event, Token.
    /// </summary>
    public required string Rarity { get; set; }

    /// <summary>
    ///     When the potion can be used: CombatOnly, AnyTime, Automatic, None.
    /// </summary>
    public required string Usage { get; set; }

    /// <summary>
    ///     Targeting mode: Self, AnyEnemy, AllEnemies, AnyPlayer, etc.
    /// </summary>
    public required string TargetType { get; set; }
}