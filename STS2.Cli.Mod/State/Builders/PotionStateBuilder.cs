using MegaCrit.Sts2.Core.Models;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds PotionStateDto list from a player's potion slots.
/// </summary>
public static class PotionStateBuilder
{
    private static readonly ModLogger Logger = new("PotionStateBuilder");

    /// <summary>
    ///     Builds potion states from a player's potion belt.
    ///     Iterates all slots; null (empty) slots are skipped.
    /// </summary>
    public static List<PotionStateDto> Build(IReadOnlyList<PotionModel?> potionSlots)
    {
        var result = new List<PotionStateDto>();

        for (var i = 0; i < potionSlots.Count; i++)
        {
            var potion = potionSlots[i];
            if (potion == null) continue;

            try
            {
                result.Add(new PotionStateDto
                {
                    Slot = i,
                    Id = potion.Id.Entry,
                    Name = StripGameTags(potion.Title.GetFormattedText()),
                    Description = StripGameTags(potion.DynamicDescription.GetFormattedText()),
                    Rarity = potion.Rarity.ToString(),
                    Usage = potion.Usage.ToString(),
                    TargetType = potion.TargetType.ToString()
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build potion state for slot {i}: {ex.Message}");
            }
        }

        return result;
    }
}
