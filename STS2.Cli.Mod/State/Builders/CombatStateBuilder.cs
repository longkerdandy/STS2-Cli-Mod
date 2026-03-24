using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2.Cli.Mod.State;
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

        var playerCombatState = player.PlayerCombatState;
        if (playerCombatState?.Hand == null) return hand;

        var cards = playerCombatState.Hand.Cards;
        for (var i = 0; i < cards.Count; i++)
        {
            try
            {
                hand.Add(CardStateBuilder.Build(cards[i], i));
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build card state for hand index {i}: {ex.Message}");
            }
        }

        return hand;
    }

    /// <summary>
    ///     Builds the enemies state from combat state.
    ///     Includes all enemies (alive and dead) with IsAlive flag for each.
    /// </summary>
    public static List<EnemyStateDto> BuildEnemies(CombatState combatState)
    {
        var enemies = new List<EnemyStateDto>();

        foreach (var creature in combatState.Enemies)
        {
            try
            {
                enemies.Add(EnemyStateBuilder.Build(creature, combatState));
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build enemy state for {creature.Monster?.Id}: {ex.Message}");
            }
        }

        return enemies;
    }
}