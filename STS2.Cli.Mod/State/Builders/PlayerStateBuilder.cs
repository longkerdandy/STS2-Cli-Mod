using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Models;
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
        var state = new PlayerStateDto();

        try
        {
            // Character information
            state.CharacterId = player.Character.Id.Entry;
            state.CharacterName = StripGameTags(player.Character.Title.GetFormattedText());
            state.Gold = player.Gold;

            // Potions
            var slotIndex = 0;
            foreach (var potion in player.PotionSlots)
            {
                if (potion != null)
                    state.Potions.Add(new PotionStateDto
                    {
                        Slot = slotIndex,
                        Id = potion.Id.Entry,
                        Name = StripGameTags(potion.Title.GetFormattedText()),
                        Description = StripGameTags(potion.DynamicDescription.GetFormattedText()),
                        CanUseInCombat = potion.Usage is PotionUsage.CombatOnly or PotionUsage.AnyTime,
                        TargetType = potion.TargetType.ToString()
                    });
                slotIndex++;
            }

            // Relics
            foreach (var relic in player.Relics)
                state.Relics.Add(new RelicStateDto
                {
                    Id = relic.Id.Entry,
                    Name = StripGameTags(relic.Title.GetFormattedText()),
                    Description = StripGameTags(relic.DynamicDescription.GetFormattedText()),
                    Counter = relic.ShowCounter ? relic.DisplayAmount : null
                });

            var creature = player.Creature;
            var playerCombatState = player.PlayerCombatState;

            // Basic stats from Creature
            state.Hp = creature.CurrentHp;
            state.MaxHp = creature.MaxHp;
            state.Block = creature.Block;

            // Combat stats from PlayerCombatState
            if (playerCombatState != null)
            {
                state.Energy = playerCombatState.Energy;
                state.MaxEnergy = playerCombatState.MaxEnergy;

                // Pile counts
                state.DeckCount = playerCombatState.DrawPile.Cards.Count;
                state.DiscardCount = playerCombatState.DiscardPile.Cards.Count;
                state.ExhaustCount = playerCombatState.ExhaustPile.Cards.Count;
                state.HandCount = playerCombatState.Hand.Cards.Count;

                // The Regent's stars resource
                if (player.Character.ShouldAlwaysShowStarCounter || playerCombatState.Stars > 0)
                    state.Stars = playerCombatState.Stars;
            }

            // Buffs/Powers from Creature
            state.Buffs = BuildBuffs(creature.Powers);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to build player state: {ex.Message}");
        }

        return state;
    }

    /// <summary>
    ///     Builds buffs/powers state for a creature.
    /// </summary>
    private static List<BuffStateDto> BuildBuffs(IEnumerable<PowerModel> powers)
    {
        var buffs = new List<BuffStateDto>();

        try
        {
            foreach (var power in powers)
            {
                if (!power.IsVisible) continue;

                var buff = new BuffStateDto
                {
                    Id = power.Id.Entry,
                    Name = StripGameTags(power.Title.GetFormattedText()),
                    Amount = power.DisplayAmount,
                    Type = power.Type.ToString(),
                    Description = StripGameTags(power.SmartDescription.GetFormattedText())
                };

                buffs.Add(buff);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to build buffs state: {ex.Message}");
        }

        return buffs;
    }
}