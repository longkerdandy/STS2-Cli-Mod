using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Orb state DTO representing a channeled orb (Defect exclusive).
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class OrbStateDto
{
    /// <summary>
    ///     Orb model ID (e.g., "LIGHTNING", "FROST", "DARK", "PLASMA", "GLASS").
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     Localized display name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Current passive value (affected by Focus).
    /// </summary>
    public int PassiveValue { get; set; }

    /// <summary>
    ///     Current evoke value (affected by Focus; accumulated for Dark orb).
    /// </summary>
    public int EvokeValue { get; set; }
}
