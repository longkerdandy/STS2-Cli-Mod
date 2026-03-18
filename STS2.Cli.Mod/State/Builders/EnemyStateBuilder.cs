using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2.Cli.Mod.Models.Dto;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds EnemyStateDto from Creature and CombatState.
/// </summary>
public static class EnemyStateBuilder
{
    private static readonly ModLogger Logger = new("EnemyStateBuilder");

    /// <summary>
    ///     Builds an enemy state DTO.
    /// </summary>
    public static EnemyStateDto Build(Creature creature, int index, CombatState combatState)
    {
        var state = new EnemyStateDto
        {
            Index = index,
            Hp = creature.CurrentHp,
            MaxHp = creature.MaxHp,
            Block = creature.Block,
            IsMinion = creature.IsPet
        };

        try
        {
            var monster = creature.Monster;
            if (monster != null)
            {
                state.Id = monster.Id.Entry;
                state.Name = StripGameTags(monster.Title.GetFormattedText());

                // Intent
                var nextMove = monster.NextMove;
                // ReSharper disable once ConvertTypeCheckToNullCheck
                if (nextMove is MoveState) state.Intent = BuildIntent(nextMove, creature, combatState);
            }
            else
            {
                state.Id = "unknown";
                state.Name = StripGameTags(creature.Name);
            }

            // Buffs
            state.Buffs = BuildBuffs(creature.Powers);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to build enemy state: {ex.Message}");
        }

        return state;
    }

    /// <summary>
    ///     Builds intent state from MoveState.
    /// </summary>
    private static IntentStateDto BuildIntent(MoveState moveState, Creature creature, CombatState combatState)
    {
        var state = new IntentStateDto();

        try
        {
            var intents = moveState.Intents;
            if (intents.Count > 0)
            {
                // Use first intent for now
                var intent = intents[0];
                state.Type = intent.IntentType.ToString();

                // Try to get the label
                try
                {
                    var targets = combatState.PlayerCreatures;
                    var label = intent.GetIntentLabel(targets, creature);
                    state.Description = StripGameTags(label.GetFormattedText());
                }
                catch
                {
                    state.Description = "";
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to build intent state: {ex.Message}");
        }

        return state;
    }

    /// <summary>
    ///     Builds buffs/powers state.
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