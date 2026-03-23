using MegaCrit.Sts2.Core.Entities.Orbs;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds OrbStateDto list from a player's orb queue.
/// </summary>
public static class OrbStateBuilder
{
    private static readonly ModLogger Logger = new("OrbStateBuilder");

    /// <summary>
    ///     Builds orb states from the player's orb queue.
    /// </summary>
    public static List<OrbStateDto> Build(OrbQueue orbQueue)
    {
        var result = new List<OrbStateDto>();

        foreach (var orb in orbQueue.Orbs)
        {
            try
            {
                result.Add(new OrbStateDto
                {
                    Id = orb.Id.Entry,
                    Name = StripGameTags(orb.Title.GetFormattedText()),
                    PassiveValue = (int)orb.PassiveVal,
                    EvokeValue = (int)orb.EvokeVal
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build orb state: {ex.Message}");
            }
        }

        return result;
    }
}
