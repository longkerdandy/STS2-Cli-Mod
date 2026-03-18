using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.Dto;

/// <summary>
///     Player state DTO containing character stats and resources.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class PlayerStateDto
{
    /// <summary>
    ///     Current HP.
    /// </summary>
    public int Hp { get; set; }

    /// <summary>
    ///     Maximum HP.
    /// </summary>
    public int MaxHp { get; set; }

    /// <summary>
    ///     Current energy.
    /// </summary>
    public int Energy { get; set; }

    /// <summary>
    ///     Maximum energy (typically 3).
    /// </summary>
    public int MaxEnergy { get; set; }

    /// <summary>
    ///     Current block (temporary HP).
    /// </summary>
    public int Block { get; set; }

    /// <summary>
    ///     Number of cards in hand.
    /// </summary>
    public int HandCount { get; set; }

    /// <summary>
    ///     Number of cards in the draw pile.
    /// </summary>
    public int DeckCount { get; set; }

    /// <summary>
    ///     Number of cards in the discard pile.
    /// </summary>
    public int DiscardCount { get; set; }

    /// <summary>
    ///     Number of cards in the exhaust pile.
    /// </summary>
    public int ExhaustCount { get; set; }

    /// <summary>
    ///     Active buffs/powers on the player.
    /// </summary>
    public List<BuffStateDto> Buffs { get; set; } = new();
}
