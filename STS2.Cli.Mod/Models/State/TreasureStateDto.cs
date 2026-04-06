using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Treasure room state DTO containing chest status, available relics, and proceed/skip status.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class TreasureStateDto
{
    /// <summary>
    ///     Whether the chest has been opened.
    /// </summary>
    public bool IsChestOpened { get; set; }

    /// <summary>
    ///     Available relics to pick from (non-null only after chest is opened and relics are shown).
    /// </summary>
    public List<TreasureRelicDto> Relics { get; set; } = [];

    /// <summary>
    ///     Whether the proceed button is enabled (relic has been picked and player can leave).
    /// </summary>
    public bool CanProceed { get; set; }

    /// <summary>
    ///     Whether the skip button is available (can skip relic picking).
    /// </summary>
    public bool CanSkip { get; set; }
}

/// <summary>
///     Individual relic available for picking in the treasure room.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class TreasureRelicDto
{
    /// <summary>
    ///     0-based index in the relic list (used by pick_relic command).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Relic model ID (e.g., "GORGET", "BURNING_BLOOD").
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
    ///     Rarity tier: Common, Uncommon, Rare, etc.
    /// </summary>
    public required string Rarity { get; set; }
}
