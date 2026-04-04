using MegaCrit.Sts2.Core.Models;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds RelicStateDto list from a player's relics.
/// </summary>
public static class RelicStateBuilder
{
    private static readonly ModLogger Logger = new("RelicStateBuilder");

    /// <summary>
    ///     Builds relic states from a player's relic collection.
    /// </summary>
    public static List<RelicStateDto> Build(IEnumerable<RelicModel> relics)
    {
        var result = new List<RelicStateDto>();

        foreach (var relic in relics)
            try
            {
                result.Add(new RelicStateDto
                {
                    Id = relic.Id.Entry,
                    Name = StripGameTags(relic.Title.GetFormattedText()),
                    Description = StripGameTags(relic.DynamicDescription.GetFormattedText()),
                    Rarity = relic.Rarity.ToString(),
                    Status = relic.Status.ToString(),
                    Counter = relic.ShowCounter ? relic.DisplayAmount : null
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build relic state: {ex.Message}");
            }

        return result;
    }
}