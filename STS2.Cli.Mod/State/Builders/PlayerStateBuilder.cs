using MegaCrit.Sts2.Core.Entities.Players;
using STS2.Cli.Mod.Models.Dto;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds PlayerStateDto from Player and Creature.
/// </summary>
public static class PlayerStateBuilder
{
    private static readonly ModLogger Logger = new("PlayerStateBuilder");

    /// <summary>
    ///     Builds a player state DTO.
    /// </summary>
    public static PlayerStateDto Build(Player player)
    {
        var creature = player.Creature;
        var playerCombatState = player.PlayerCombatState;

        var state = new PlayerStateDto
        {
            // Character information
            CharacterId = player.Character.Id.Entry,
            CharacterName = StripGameTags(player.Character.Title.GetFormattedText()),
            Gold = player.Gold,

            // Potions and Relics
            Potions = PotionStateBuilder.Build(player.PotionSlots),
            Relics = RelicStateBuilder.Build(player.Relics),

            // Basic stats from Creature
            Hp = creature.CurrentHp,
            MaxHp = creature.MaxHp,
            Block = creature.Block,

            // Base max energy (run-scoped, available outside combat)
            MaxEnergy = player.MaxEnergy,

            // Master deck size (run-scoped, persists across combats)
            DeckCount = player.Deck.Cards.Count,

            // Powers from Creature
            Powers = PowerStateBuilder.Build(creature.Powers)
        };

        // Combat stats from PlayerCombatState
        if (playerCombatState != null)
        {
            state.Energy = playerCombatState.Energy;
            // In combat, use the effective max energy (includes hook modifications)
            state.MaxEnergy = playerCombatState.MaxEnergy;

            // Combat pile counts
            state.DrawCount = playerCombatState.DrawPile.Cards.Count;
            state.DiscardCount = playerCombatState.DiscardPile.Cards.Count;
            state.ExhaustCount = playerCombatState.ExhaustPile.Cards.Count;
            state.HandCount = playerCombatState.Hand.Cards.Count;

            // The Regent's stars resource
            if (player.Character.ShouldAlwaysShowStarCounter || playerCombatState.Stars > 0)
                state.Stars = playerCombatState.Stars;

            // Orbs (Defect exclusive)
            var orbQueue = playerCombatState.OrbQueue;
            if (orbQueue.Capacity > 0)
            {
                state.OrbSlots = orbQueue.Capacity;
                state.Orbs = OrbStateBuilder.Build(orbQueue);
            }

            // Pets (Necrobinder's Osty, Byrdpip, etc.)
            var pets = playerCombatState.Pets;
            if (pets.Count > 0)
                state.Pets = PetStateBuilder.Build(pets);
        }

        return state;
    }
}
