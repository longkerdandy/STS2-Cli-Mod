using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2.Cli.Mod.State.Dto;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds CombatStateDto and related collections (hand, enemies).
/// </summary>
public static class CombatStateBuilder
{
    private static readonly ModLogger Logger = new("CombatStateBuilder");

    /// <summary>
    ///     Builds the hand state from the player's combat state.
    /// </summary>
    public static List<CardStateDto> BuildHand(Player player)
    {
        var hand = new List<CardStateDto>();

        try
        {
            var playerCombatState = player.PlayerCombatState;
            if (playerCombatState?.Hand == null) return hand;

            var cards = playerCombatState.Hand.Cards;
            for (var i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                hand.Add(CardStateBuilder.Build(card, i));
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to build hand state: {ex.Message}");
        }

        return hand;
    }

    /// <summary>
    ///     Builds the enemies state from combat state.
    /// </summary>
    public static List<EnemyStateDto> BuildEnemies(CombatState combatState)
    {
        var enemies = new List<EnemyStateDto>();

        try
        {
            var creatures = combatState.Enemies;
            for (var i = 0; i < creatures.Count; i++)
            {
                var creature = creatures[i];
                if (!creature.IsAlive) continue;

                enemies.Add(EnemyStateBuilder.Build(creature, i, combatState));
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to build enemies state: {ex.Message}");
        }

        return enemies;
    }
}