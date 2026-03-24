using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using STS2.Cli.Mod.Models.State;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds EnemyStateDto from Creature and CombatState.
/// </summary>
public static class EnemyStateBuilder
{
    /// <summary>
    ///     Builds an enemy state DTO.
    /// </summary>
    public static EnemyStateDto Build(Creature creature, CombatState combatState)
    {
        // creature.Monster is never null for enemy-side creatures (constructed via monster constructor)
        // creature.CombatId is declared uint? in game, but always assigned before the creature enters combat
        var monster = creature.Monster!;

        var state = new EnemyStateDto
        {
            CombatId = creature.CombatId!.Value,
            Id = monster.Id.Entry,
            Name = StripGameTags(monster.Title.GetFormattedText()),
            Hp = creature.CurrentHp,
            MaxHp = creature.MaxHp,
            Block = creature.Block,
            IsAlive = creature.IsAlive,
            IsMinion = creature.IsSecondaryEnemy,
            MoveId = monster.NextMove.StateId,
            Intents = IntentStateBuilder.Build(monster.NextMove, creature, combatState.PlayerCreatures),
            Powers = PowerStateBuilder.Build(creature.Powers)
        };

        return state;
    }
}
