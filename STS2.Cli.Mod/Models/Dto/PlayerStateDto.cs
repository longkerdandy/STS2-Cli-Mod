using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.Dto;

/// <summary>
///     Player state DTO containing character stats and resources.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "CollectionNeverQueried.Global")]
public class PlayerStateDto
{
    /// <summary>
    ///     Character class ID (e.g., "IRONCLAD", "SILENT", "DEFECT").
    /// </summary>
    public required string CharacterId { get; set; }

    /// <summary>
    ///     Localized character display name.
    /// </summary>
    public required string CharacterName { get; set; }

    /// <summary>
    ///     Current gold amount.
    /// </summary>
    public int Gold { get; set; }

    /// <summary>
    ///     Active potions in inventory.
    /// </summary>
    public List<PotionStateDto> Potions { get; set; } = [];

    /// <summary>
    ///     Acquired relics.
    /// </summary>
    public List<RelicStateDto> Relics { get; set; } = [];
    
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
    public int DrawCount { get; set; }

    /// <summary>
    ///     Number of cards in the discard pile.
    /// </summary>
    public int DiscardCount { get; set; }

    /// <summary>
    ///     Number of cards in the exhaust pile.
    /// </summary>
    public int ExhaustCount { get; set; }

    /// <summary>
    ///     Total number of cards in the master deck (run-scoped, persists across combats).
    /// </summary>
    public int DeckCount { get; set; }

    /// <summary>
    ///     The Regent's stars resource (null for other characters).
    /// </summary>
    public int? Stars { get; set; }

    /// <summary>
    ///     Active powers on the player.
    /// </summary>
    public List<PowerStateDto> Powers { get; set; } = [];

    /// <summary>
    ///     Channeled orbs for the Defect (null for other characters).
    /// </summary>
    public List<OrbStateDto>? Orbs { get; set; }

    /// <summary>
    ///     Total orb slot capacity for the Defect (null for other characters).
    /// </summary>
    public int? OrbSlots { get; set; }

    /// <summary>
    ///     Pet creatures in combat, e.g. Necrobinder's Osty (null if no pets).
    /// </summary>
    public List<PetStateDto>? Pets { get; set; }
}
