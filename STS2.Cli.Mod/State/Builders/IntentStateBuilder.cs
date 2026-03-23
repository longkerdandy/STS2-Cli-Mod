using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds IntentStateDto list from a MoveState.
/// </summary>
public static class IntentStateBuilder
{
    private static readonly ModLogger Logger = new("IntentStateBuilder");

    /// <summary>
    ///     Builds all intent states from a MoveState.
    ///     A move can have multiple intents (e.g., attack + buff simultaneously).
    /// </summary>
    public static List<IntentStateDto> Build(MoveState moveState, Creature creature,
        IReadOnlyList<Creature> targets)
    {
        var intents = new List<IntentStateDto>();

        foreach (var intent in moveState.Intents)
        {
            try
            {
                var label = intent.GetIntentLabel(targets, creature);

                var intentDto = new IntentStateDto
                {
                    Type = intent.IntentType.ToString(),
                    Description = StripGameTags(label.GetFormattedText())
                };

                // Extract damage info for attack intents
                if (intent is AttackIntent attackIntent)
                {
                    try
                    {
                        intentDto.Damage = attackIntent.GetSingleDamage(targets, creature);
                        intentDto.Hits = attackIntent.Repeats > 0 ? attackIntent.Repeats : 1;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Failed to extract attack damage: {ex.Message}");
                    }
                }

                intents.Add(intentDto);
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build intent state: {ex.Message}");
            }
        }

        return intents;
    }
}
