using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Intent state DTO representing single intent within an enemy's upcoming move.
///     A move can contain multiple intents (e.g., attack + buff simultaneously).
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class IntentStateDto
{
    /// <summary>
    ///     Intent type (Attack, DeathBlow, Buff, Debuff, DebuffStrong, Defend,
    ///     Heal, Summon, Escape, StatusCard, CardDebuff, Sleep, Stun, Hidden, Unknown).
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    ///     Per-hit damage for attack intents (after modifiers like Strength, Vulnerable).
    ///     Null for non-attack intents.
    /// </summary>
    public int? Damage { get; set; }

    /// <summary>
    ///     Number of hits for attack intents (1 for single, N for multi-attack).
    ///     Null for non-attack intents.
    /// </summary>
    public int? Hits { get; set; }

    /// <summary>
    ///     Human-readable description of the intent (localized).
    /// </summary>
    public required string Description { get; set; }
}
