using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Game over screen state DTO.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class GameOverStateDto
{
    /// <summary>
    ///     True if the player won (defeated final boss), false if defeated.
    /// </summary>
    public bool IsVictory { get; set; }

    /// <summary>
    ///     The floor number where the run ended.
    /// </summary>
    public int Floor { get; set; }

    /// <summary>
    ///     Character ID used in this run.
    /// </summary>
    public string? CharacterId { get; set; }

    /// <summary>
    ///     Final score.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    ///     Number of epochs discovered (if any).
    /// </summary>
    public int EpochsDiscovered { get; set; }

    /// <summary>
    ///     Whether the return to menu button is available.
    /// </summary>
    public bool CanReturnToMenu { get; set; }

    /// <summary>
    ///     Whether the continue/summary button is available.
    /// </summary>
    public bool CanContinue { get; set; }
}
