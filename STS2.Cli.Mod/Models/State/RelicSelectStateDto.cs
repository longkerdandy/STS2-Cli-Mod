using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Relic selection screen state DTO. Represents the "choose a relic" overlay
///     that appears after boss fights, certain events, or other relic reward scenarios.
///     Contains the list of available relics and whether the selection can be skipped.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class RelicSelectStateDto
{
    /// <summary>
    ///     List of relics available for selection.
    /// </summary>
    public List<SelectableRelicDto> Relics { get; set; } = [];

    /// <summary>
    ///     Whether the player can skip this selection (choose no relic).
    ///     Always true for boss relic selection.
    /// </summary>
    public bool CanSkip { get; set; }
}

/// <summary>
///     A single selectable relic in the relic selection screen.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class SelectableRelicDto
{
    /// <summary>
    ///     0-based index of the relic in the selection screen.
    /// </summary>
    public int Index { get; set; }

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
}
