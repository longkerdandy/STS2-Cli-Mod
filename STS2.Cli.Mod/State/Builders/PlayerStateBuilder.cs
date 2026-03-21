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
        var state = new PlayerStateDto();

        try
        {
            // Character information
            state.CharacterId = player.Character.Id.Entry;
            state.CharacterName = StripGameTags(player.Character.Title.GetFormattedText());
            state.Gold = player.Gold;

            // Potions
            state.Potions = PotionStateBuilder.Build(player.PotionSlots);

            // Relics
            state.Relics = RelicStateBuilder.Build(player.Relics);

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

            // Powers from Creature
            state.Powers = PowerStateBuilder.Build(creature.Powers);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to build player state: {ex.Message}");
        }

        return state;
    }
}