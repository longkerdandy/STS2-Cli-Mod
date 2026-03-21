using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Entities.Creatures;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Converts <see cref="CombatHistoryEntry" /> objects into JSON-friendly anonymous objects
///     for inclusion in action responses. Focuses on core combat effects: damage, block, and powers.
/// </summary>
public static class ResultBuilder
{
    private static readonly ModLogger Logger = new("ResultBuilder");

    /// <summary>
    ///     Collects new <see cref="CombatHistoryEntry" /> items added after <paramref name="historyCountBefore" />
    ///     and converts them into a list of JSON-serializable result objects.
    /// </summary>
    /// <param name="historyCountBefore">
    ///     The <c>CombatHistory.Entries.Count()</c> snapshot taken before the action was enqueued.
    /// </param>
    /// <returns>A list of anonymous objects representing the action's effects.</returns>
    public static List<object> BuildFromHistory(int historyCountBefore)
    {
        var results = new List<object>();

        try
        {
            var entries = CombatManager.Instance.History.Entries.Skip(historyCountBefore);

            foreach (var entry in entries)
            {
                var result = ConvertEntry(entry);
                if (result != null)
                    results.Add(result);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to collect history results: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    ///     Converts a single <see cref="CombatHistoryEntry" /> to a JSON-friendly object.
    ///     Returns null for entry types we don't report (e.g., CardPlayStarted/Finished).
    /// </summary>
    private static object? ConvertEntry(CombatHistoryEntry entry)
    {
        try
        {
            return entry switch
            {
                DamageReceivedEntry e => new
                {
                    type = "damage",
                    target_id = (int?)e.Receiver.CombatId,
                    target_name = GetCreatureId(e.Receiver),
                    damage = e.Result.TotalDamage,
                    blocked = e.Result.BlockedDamage,
                    hp_loss = e.Result.UnblockedDamage,
                    killed = e.Result.WasTargetKilled
                },
                BlockGainedEntry e => new
                {
                    type = "block",
                    target_id = (int?)e.Receiver.CombatId,
                    target_name = GetCreatureId(e.Receiver),
                    amount = e.Amount
                },
                PowerReceivedEntry e => new
                {
                    type = "power",
                    target_id = (int?)e.Actor.CombatId,
                    target_name = GetCreatureId(e.Actor),
                    power_id = e.Power.Id.Entry,
                    amount = (int)e.Amount
                },
                _ => null
            };
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to convert history entry {entry.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the model ID string for a creature (character ID for players, monster ID for enemies).
    /// </summary>
    private static string GetCreatureId(Creature creature)
    {
        if (creature.IsPlayer)
            return creature.Player?.Character.Id.Entry ?? "UNKNOWN";
        return creature.Monster?.Id.Entry ?? "UNKNOWN";
    }
}