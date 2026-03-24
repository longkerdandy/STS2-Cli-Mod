using MegaCrit.Sts2.Core.Entities.Players;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds <see cref="PlayerStateDto" /> from <see cref="Player" /> and its sub-objects.
/// </summary>
public static class PlayerStateBuilder
{
    private static readonly ModLogger Logger = new("PlayerStateBuilder");

    /// <summary>
    ///     Builds a player state DTO.
    ///     Field assignment order follows game source: Player → Creature → PlayerCombatState.
    /// </summary>
    public static PlayerStateDto Build(Player player)
    {
        var creature = player.Creature;
        var playerCombatState = player.PlayerCombatState;

        var state = new PlayerStateDto
        {
            // Player (run-scoped)
            CharacterId = player.Character.Id.Entry,
            CharacterName = StripGameTags(player.Character.Title.GetFormattedText()),
            Relics = RelicStateBuilder.Build(player.Relics),
            Potions = PotionStateBuilder.Build(player.PotionSlots),
            Gold = player.Gold,
            DeckCount = player.Deck.Cards.Count,
            MaxEnergy = player.MaxEnergy,

            // Creature
            Block = creature.Block,
            Hp = creature.CurrentHp,
            MaxHp = creature.MaxHp,
            Powers = PowerStateBuilder.Build(creature.Powers)
        };

        // PlayerCombatState (combat-scoped)
        if (playerCombatState != null)
            try
            {
                // Pets
                var pets = playerCombatState.Pets;
                if (pets.Count > 0)
                    state.Pets = PetStateBuilder.Build(pets);

                // Card pile counts
                state.HandCount = playerCombatState.Hand.Cards.Count;
                state.DrawCount = playerCombatState.DrawPile.Cards.Count;
                state.DiscardCount = playerCombatState.DiscardPile.Cards.Count;
                state.ExhaustCount = playerCombatState.ExhaustPile.Cards.Count;

                // Energy (override run-scoped base with combat-scoped effective value)
                state.Energy = playerCombatState.Energy;
                state.MaxEnergy = playerCombatState.MaxEnergy;

                // Stars (Regent exclusive)
                if (player.Character.ShouldAlwaysShowStarCounter || playerCombatState.Stars > 0)
                    state.Stars = playerCombatState.Stars;

                // Orbs (Defect exclusive)
                var orbQueue = playerCombatState.OrbQueue;
                if (orbQueue.Capacity > 0)
                {
                    state.Orbs = OrbStateBuilder.Build(orbQueue);
                    state.OrbSlots = orbQueue.Capacity;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to read combat state fields: {ex.Message}");
            }

        return state;
    }
}
