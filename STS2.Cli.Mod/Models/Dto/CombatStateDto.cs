using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.Dto;

/// <summary>
///     Combat-specific state DTO containing player, hand, and enemy information.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class CombatStateDto
{
    /// <summary>
    ///     True if it's the player's turn and they can act.
    /// </summary>
    public bool IsPlayerTurn { get; set; }

    /// <summary>
    ///     Current round/turn number.
    /// </summary>
    public int TurnNumber { get; set; }

    /// <summary>
    ///     Player state including HP, energy, block, etc.
    /// </summary>
    public PlayerStateDto? Player { get; set; }

    /// <summary>
    ///     Current hand cards.
    /// </summary>
    public List<CardStateDto> Hand { get; set; } = [];

    /// <summary>
    ///     Active enemies in combat.
    /// </summary>
    public List<EnemyStateDto> Enemies { get; set; } = [];
}
