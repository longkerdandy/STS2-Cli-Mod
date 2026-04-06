using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Enemy state DTO representing a single enemy in combat.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class EnemyStateDto
{
    /// <summary>
    ///     Unique combat ID assigned by the game engine.
    ///     Stable across the entire combat, unlike Index which shifts when enemies die.
    /// </summary>
    public required uint CombatId { get; set; }

    /// <summary>
    ///     Enemy model ID (e.g., "JAW_WORM", "CULTIST").
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     Enemy display name (localized).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Current HP.
    /// </summary>
    public int Hp { get; set; }

    /// <summary>
    ///     Maximum HP.
    /// </summary>
    public int MaxHp { get; set; }

    /// <summary>
    ///     Current block value.
    /// </summary>
    public int Block { get; set; }

    /// <summary>
    ///     Whether this enemy is alive.
    /// </summary>
    public bool IsAlive { get; set; }

    /// <summary>
    ///     Whether this is a secondary enemy (minion/summon).
    ///     Combat does not end when secondary enemies die; only primary enemies matter.
    /// </summary>
    public bool IsMinion { get; set; }

    /// <summary>
    ///     Current move ID from the monster's state machine (e.g., "BITE", "SCREECH", "STUNNED").
    /// </summary>
    public required string MoveId { get; set; }

    /// <summary>
    ///     Enemy's current intents (next action). A move can have multiple intents
    ///     (e.g., attack + buff simultaneously).
    /// </summary>
    public List<IntentStateDto> Intents { get; set; } = [];

    /// <summary>
    ///     Active powers on the enemy.
    /// </summary>
    public List<PowerStateDto> Powers { get; set; } = [];
}