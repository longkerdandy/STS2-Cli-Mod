using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.State;

/// <summary>
///     Power state DTO representing a status effect on a creature.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class PowerStateDto
{
    /// <summary>
    ///     Power model ID (e.g., "STRENGTH_POWER", "VULNERABLE_POWER").
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     Power display name (localized).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Power stack amount (display value).
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    ///     Power type: "Buff", "Debuff", or "None".
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    ///     Power stack type: "Counter" (numeric stacks, e.g., Strength, Poison),
    ///     "Single" (binary on/off, e.g., Barricade, Corruption), or "None".
    /// </summary>
    public required string StackType { get; set; }

    /// <summary>
    ///     Power description text (localized).
    /// </summary>
    public required string Description { get; set; }
}
