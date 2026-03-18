using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.Dto;

/// <summary>
///     Enemy state DTO representing a single enemy in combat.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public class EnemyStateDto
{
    /// <summary>
    ///     Enemy index in combat (0-based).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Enemy ID (e.g., "Cultist", "JawWorm").
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    ///     Enemy display name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Current HP.
    /// </summary>
    public int Hp { get; set; }

    /// <summary>
    ///     Maximum HP.
    /// </summary>
    public int MaxHp { get; set; }

    /// <summary>
    ///     Current block (temporary HP).
    /// </summary>
    public int Block { get; set; }

    /// <summary>
    ///     True if this is a minion/pet.
    /// </summary>
    public bool IsMinion { get; set; }

    /// <summary>
    ///     Enemy's current intent (next action).
    /// </summary>
    public IntentStateDto? Intent { get; set; }

    /// <summary>
    ///     Active buffs/powers on the enemy.
    /// </summary>
    public List<BuffStateDto> Buffs { get; set; } = new();
}
