using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.Dto;

/// <summary>
///     Combat-specific state DTO containing player, hand, and enemy information.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class CombatStateDto
{
    // --- Combat metadata ---

    /// <summary>
    ///     The encounter ID for this combat (e.g. "jaw_worm", "hexaghost").
    ///     Null if encounter information is unavailable.
    /// </summary>
    /// <remarks>
    ///     Source: <c>CombatState.Encounter?.Id.Entry</c>.
    /// </remarks>
    public string? Encounter { get; set; }

    /// <summary>
    ///     Current round/turn number. 1-indexed, increments at the start of each player turn.
    /// </summary>
    /// <remarks>
    ///     Source: <c>CombatState.RoundNumber</c>.
    /// </remarks>
    public int TurnNumber { get; set; }

    // --- Turn phase ---

    /// <summary>
    ///     True if it's the player's turn and they can play cards.
    /// </summary>
    /// <remarks>
    ///     Source: <c>CombatManager.IsPlayPhase</c>.
    /// </remarks>
    public bool IsPlayerTurn { get; set; }

    /// <summary>
    ///     True if the player has pressed End Turn and actions are no longer accepted.
    ///     When true, the AI should not send any more actions even if <see cref="IsPlayerTurn"/> is still true.
    /// </summary>
    /// <remarks>
    ///     Source: <c>CombatManager.PlayerActionsDisabled</c>.
    /// </remarks>
    public bool IsPlayerActionsDisabled { get; set; }

    /// <summary>
    ///     True if the combat is ending or already over (victory or defeat).
    ///     When true, the AI should stop sending actions.
    /// </summary>
    /// <remarks>
    ///     Source: <c>CombatManager.IsOverOrEnding</c>.
    /// </remarks>
    public bool IsCombatEnding { get; set; }

    // --- Player ---

    /// <summary>
    ///     Player state including HP, energy, block, etc.
    /// </summary>
    public PlayerStateDto? Player { get; set; }

    // --- Cards ---

    /// <summary>
    ///     Current hand cards.
    /// </summary>
    public List<CardStateDto> Hand { get; set; } = [];

    // --- Enemies ---

    /// <summary>
    ///     Active enemies in combat (includes dead enemies with IsAlive=false).
    /// </summary>
    public List<EnemyStateDto> Enemies { get; set; } = [];
}
