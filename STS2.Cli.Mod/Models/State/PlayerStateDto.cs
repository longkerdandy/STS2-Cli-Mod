using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Player state DTO containing character stats and resources.
///     Field order follows game source: Player → Creature → PlayerCombatState.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class PlayerStateDto
{
    // ------ Player (run-scoped) ------

    /// <summary>
    ///     Character class ID (e.g., "IRONCLAD", "SILENT", "DEFECT").
    /// </summary>
    public required string CharacterId { get; set; }

    /// <summary>
    ///     Localized character display name.
    /// </summary>
    public required string CharacterName { get; set; }

    /// <summary>
    ///     Acquired relics.
    /// </summary>
    public List<RelicStateDto> Relics { get; set; } = [];

    /// <summary>
    ///     Active potions in inventory.
    /// </summary>
    public List<PotionStateDto> Potions { get; set; } = [];

    /// <summary>
    ///     Current gold amount.
    /// </summary>
    public int Gold { get; set; }

    /// <summary>
    ///     Total number of cards in the master deck (run-scoped, persists across combats).
    /// </summary>
    public int DeckCount { get; set; }

    /// <summary>
    ///     Maximum energy (run-scoped base, overridden by combat-scoped effective value when in combat).
    /// </summary>
    public int MaxEnergy { get; set; }

    // ------ Creature ------

    /// <summary>
    ///     Current block (temporary HP).
    /// </summary>
    public int Block { get; set; }

    /// <summary>
    ///     Current HP.
    /// </summary>
    public int Hp { get; set; }

    /// <summary>
    ///     Maximum HP.
    /// </summary>
    public int MaxHp { get; set; }

    /// <summary>
    ///     Active powers on the player.
    /// </summary>
    public List<PowerStateDto> Powers { get; set; } = [];

    // ------ PlayerCombatState (combat-scoped, zero/null outside combat) ------

    /// <summary>
    ///     Pet creatures in combat, e.g. Necrobinder's Osty (null if no pets).
    /// </summary>
    public List<PetStateDto>? Pets { get; set; }

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
    ///     Current energy.
    /// </summary>
    public int Energy { get; set; }

    /// <summary>
    ///     The Regent's stars resource (null for other characters).
    /// </summary>
    public int? Stars { get; set; }

    /// <summary>
    ///     Channeled orbs for the Defect (null for other characters).
    /// </summary>
    public List<OrbStateDto>? Orbs { get; set; }

    /// <summary>
    ///     Total orb slot capacity for the Defect (null for other characters).
    /// </summary>
    public int? OrbSlots { get; set; }
}