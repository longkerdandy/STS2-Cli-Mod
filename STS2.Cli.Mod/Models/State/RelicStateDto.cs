using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.State;

/// <summary>
///     Relic state DTO representing an acquired relic.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class RelicStateDto
{
    /// <summary>
    ///     Relic model ID (e.g., "BURNING_BLOOD", "SOZU").
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     Localized display name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Effect description with dynamic values resolved.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    ///     Rarity tier: Starter, Common, Uncommon, Rare, Shop, Event, Ancient.
    /// </summary>
    public required string Rarity { get; set; }

    /// <summary>
    ///     Current status: Normal, Active, or Disabled (e.g., melted wax relics).
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    ///     Counter value for relics that track counts (null if the relic has no counter).
    /// </summary>
    public int? Counter { get; set; }
}
