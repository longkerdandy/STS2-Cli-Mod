using MegaCrit.Sts2.Core.Models;
using STS2.Cli.Mod.Models.Dto;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds PowerStateDto list from a creature's powers.
/// </summary>
public static class PowerStateBuilder
{
    private static readonly ModLogger Logger = new("PowerStateBuilder");

    /// <summary>
    ///     Builds power states from a creature's powers.
    ///     Only includes visible powers.
    /// </summary>
    public static List<PowerStateDto> Build(IEnumerable<PowerModel> powers)
    {
        var result = new List<PowerStateDto>();

        foreach (var power in powers)
        {
            try
            {
                if (!power.IsVisible) continue;

                result.Add(new PowerStateDto
                {
                    Id = power.Id.Entry,
                    Name = StripGameTags(power.Title.GetFormattedText()),
                    Amount = power.DisplayAmount,
                    Type = power.Type.ToString(),
                    StackType = power.StackType.ToString(),
                    Description = StripGameTags(power.SmartDescription.GetFormattedText())
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build power state: {ex.Message}");
            }
        }

        return result;
    }
}
