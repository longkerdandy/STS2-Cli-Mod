using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds <see cref="RestSiteStateDto" /> from the current <see cref="NRestSiteRoom" />.
/// </summary>
public static class RestSiteStateBuilder
{
    private static readonly ModLogger Logger = new("RestSiteStateBuilder");

    /// <summary>
    ///     Builds the rest site state from the current <see cref="NRestSiteRoom" />.
    ///     Returns null if the rest site room is not found or not in the scene tree.
    /// </summary>
    public static RestSiteStateDto? Build()
    {
        try
        {
            var restSiteRoom = NRestSiteRoom.Instance;
            if (restSiteRoom == null || !restSiteRoom.IsInsideTree())
            {
                Logger.Warning("NRestSiteRoom.Instance is null or not in tree");
                return null;
            }

            var options = new List<RestSiteOptionDto>();
            var gameOptions = restSiteRoom.Options;

            for (int i = 0; i < gameOptions.Count; i++)
            {
                var option = gameOptions[i];
                try
                {
                    options.Add(new RestSiteOptionDto
                    {
                        Index = i,
                        OptionId = option.OptionId,
                        Name = GetLocText(option.Title) ?? option.OptionId,
                        Description = GetLocText(option.Description),
                        IsEnabled = option.IsEnabled
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to build rest site option at index {i}: {ex.Message}");
                }
            }

            var canProceed = restSiteRoom.ProceedButton is { IsEnabled: true };

            Logger.Info($"Built rest site state with {options.Count} options, canProceed={canProceed}");

            return new RestSiteStateDto
            {
                Options = options,
                CanProceed = canProceed
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build rest site state: {ex.Message}");
            return null;
        }
    }
}
