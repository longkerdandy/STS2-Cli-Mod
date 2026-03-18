using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.Dto;

/// <summary>
///     Relic state DTO representing an acquired relic.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class RelicStateDto
{
    /// <summary>
    ///     Relic ID (e.g., "BurningBlood").
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    ///     Display name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Effect description with dynamic values resolved.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Counter value for relics that track counts (null if not applicable).
    /// </summary>
    public int? Counter { get; set; }
}
