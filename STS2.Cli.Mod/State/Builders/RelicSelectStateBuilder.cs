using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds the relic selection screen state DTO from <see cref="NChooseARelicSelection" />.
///     Extracts available relics and skip availability from the overlay screen
///     that appears after boss fights or certain events.
/// </summary>
public static class RelicSelectStateBuilder
{
    private static readonly ModLogger Logger = new("RelicSelectStateBuilder");

    /// <summary>
    ///     Builds the relic selection state from the currently open <see cref="NChooseARelicSelection" />.
    ///     Finds the screen via <see cref="CommonUiUtils.FindScreenInOverlay{T}" />.
    ///     Returns null if no screen is found.
    /// </summary>
    public static RelicSelectStateDto? Build()
    {
        var screen = CommonUiUtils.FindScreenInOverlay<NChooseARelicSelection>();
        if (screen == null)
        {
            Logger.Warning("No NChooseARelicSelection found in overlay stack");
            return null;
        }

        return Build(screen);
    }

    /// <summary>
    ///     Builds the relic selection state from the given <see cref="NChooseARelicSelection" /> screen.
    /// </summary>
    /// <param name="screen">The relic selection screen to extract data from.</param>
    /// <returns>DTO with selectable relics and skip availability; null on failure.</returns>
    public static RelicSelectStateDto? Build(NChooseARelicSelection screen)
    {
        try
        {
            var holders = CommonUiUtils.FindAll<NRelicBasicHolder>(screen);
            var relics = new List<SelectableRelicDto>();

            for (var i = 0; i < holders.Count; i++)
            {
                var holder = holders[i];
                var relic = holder.Relic?.Model;
                if (relic == null) continue;

                relics.Add(new SelectableRelicDto
                {
                    Index = i,
                    Id = relic.Id.Entry,
                    Name = StripGameTags(relic.Title.GetFormattedText()),
                    Description = StripGameTags(relic.DynamicDescription.GetFormattedText()),
                    Rarity = relic.Rarity.ToString()
                });
            }

            return new RelicSelectStateDto
            {
                Relics = relics,
                // Skip button is always present and animated in during _Ready()
                CanSkip = true
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build relic select state: {ex.Message}");
            return null;
        }
    }
}
