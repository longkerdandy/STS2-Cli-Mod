using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Timeline.Epochs;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds <see cref="SingleplayerSubmenuStateDto" /> from epoch discovery state.
/// </summary>
public static class SingleplayerSubmenuStateBuilder
{
    private static readonly ModLogger Logger = new("SingleplayerSubmenuStateBuilder");

    /// <summary>
    ///     Builds the singleplayer submenu state with available game modes.
    /// </summary>
    public static SingleplayerSubmenuStateDto Build()
    {
        return new SingleplayerSubmenuStateDto
        {
            StandardAvailable = true,
            DailyAvailable = SaveManager.Instance.IsEpochRevealed<DailyRunEpoch>(),
            CustomAvailable = SaveManager.Instance.IsEpochRevealed<CustomAndSeedsEpoch>()
        };
    }
}
