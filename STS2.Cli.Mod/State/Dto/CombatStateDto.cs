namespace STS2.Cli.Mod.State.Dto;

/// <summary>
///     Combat-specific state DTO containing player, hand, and enemy information.
/// </summary>
public class CombatStateDto
{
    /// <summary>
    ///     True if it's the player's turn and they can act.
    /// </summary>
    public bool IsPlayerTurn { get; set; } = true;

    /// <summary>
    ///     Current round/turn number.
    /// </summary>
    public int TurnNumber { get; set; } = 1;

    /// <summary>
    ///     Player state including HP, energy, block, etc.
    /// </summary>
    public PlayerStateDto Player { get; set; } = new();

    /// <summary>
    ///     Current hand cards.
    /// </summary>
    public List<CardStateDto> Hand { get; set; } = new();

    /// <summary>
    ///     Active enemies in combat.
    /// </summary>
    public List<EnemyStateDto> Enemies { get; set; } = new();
}
