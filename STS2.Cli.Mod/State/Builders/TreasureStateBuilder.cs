using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds <see cref="TreasureStateDto" /> from the current <see cref="NTreasureRoom" />.
/// </summary>
public static class TreasureStateBuilder
{
    private static readonly ModLogger Logger = new("TreasureStateBuilder");

    /// <summary>
    ///     Builds the treasure room state from the current <see cref="NTreasureRoom" />.
    ///     Returns null if the treasure room is not found or not in the scene tree.
    /// </summary>
    public static TreasureStateDto? Build()
    {
        try
        {
            var treasureRoom = NRun.Instance?.TreasureRoom;
            if (treasureRoom == null || !treasureRoom.IsInsideTree())
            {
                Logger.Warning("NTreasureRoom is null or not in tree");
                return null;
            }

            var isChestOpened = UiUtils.GetPrivateFieldValue<bool>(treasureRoom, "_hasChestBeenOpened") ?? false;
            var relics = new List<TreasureRelicDto>();

            // Check proceed button state
            var proceedButton = treasureRoom.ProceedButton;
            var canProceed = proceedButton is { IsEnabled: true, IsSkip: false };
            var canSkip = proceedButton is { IsEnabled: true, IsSkip: true };

            // Extract available relics from TreasureRoomRelicSynchronizer
            if (isChestOpened)
            {
                var synchronizer = RunManager.Instance.TreasureRoomRelicSynchronizer;
                var currentRelics = synchronizer.CurrentRelics;

                if (currentRelics != null)
                    for (var i = 0; i < currentRelics.Count; i++)
                        try
                        {
                            var relic = currentRelics[i];
                            relics.Add(new TreasureRelicDto
                            {
                                Index = i,
                                Id = relic.Id.Entry,
                                Name = StripGameTags(relic.Title.GetFormattedText()),
                                Description = StripGameTags(relic.DynamicDescription.GetFormattedText()),
                                Rarity = relic.Rarity.ToString()
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"Failed to build treasure relic at index {i}: {ex.Message}");
                        }
            }

            Logger.Info(
                $"Built treasure state: chestOpened={isChestOpened}, relics={relics.Count}, canProceed={canProceed}, canSkip={canSkip}");

            return new TreasureStateDto
            {
                IsChestOpened = isChestOpened,
                Relics = relics,
                CanProceed = canProceed,
                CanSkip = canSkip
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build treasure state: {ex.Message}");
            return null;
        }
    }
}