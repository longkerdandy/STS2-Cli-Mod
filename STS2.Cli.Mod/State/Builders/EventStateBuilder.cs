using System.Reflection;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2.Cli.Mod.Models.Dto;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds <see cref="EventStateDto" /> from the current <see cref="NEventRoom" />.
/// </summary>
public static class EventStateBuilder
{
    private static readonly ModLogger Logger = new("EventStateBuilder");

    /// <summary>
    ///     Cached reflection field for <see cref="NEventRoom" />._event (private).
    /// </summary>
    private static readonly FieldInfo? EventRoomEventField =
        typeof(NEventRoom).GetField("_event", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    ///     Builds the event state from the current <see cref="NEventRoom" />.
    ///     Returns null if the event room is not found or has no event model.
    /// </summary>
    public static EventStateDto? Build()
    {
        try
        {
            var eventRoom = NEventRoom.Instance;
            if (eventRoom == null || !eventRoom.IsInsideTree())
            {
                Logger.Warning("NEventRoom.Instance is null or not in tree");
                return null;
            }

            // Access private _event field via reflection
            var eventModel = EventRoomEventField?.GetValue(eventRoom) as EventModel;
            if (eventModel == null)
            {
                Logger.Warning("Failed to access EventModel from NEventRoom");
                return null;
            }

            var result = new EventStateDto
            {
                EventId = eventModel.Id.Entry,
                Title = StripGameTags(eventModel.Title.GetFormattedText()),
                Description = eventModel.Description != null ? StripGameTags(eventModel.Description.GetFormattedText()) : null,
                LayoutType = eventModel.LayoutType.ToString(),
                IsFinished = eventModel.IsFinished,
                Options = BuildOptions(eventModel)
            };

            Logger.Info($"Built event state for '{result.EventId}' with {result.Options.Count} options");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build event state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Builds the list of event options from the event model.
    /// </summary>
    private static List<EventOptionDto> BuildOptions(EventModel eventModel)
    {
        var options = new List<EventOptionDto>();
        var currentOptions = eventModel.CurrentOptions;

        if (currentOptions == null)
        {
            Logger.Warning("EventModel.CurrentOptions is null");
            return options;
        }

        var index = 0;
        foreach (var option in currentOptions)
        {
            try
            {
                var optionDto = new EventOptionDto
                {
                    Index = index,
                    Title = StripGameTags(option.Title.GetFormattedText()),
                    Description = StripGameTags(option.Description.GetFormattedText()),
                    TextKey = option.TextKey,
                    IsLocked = option.IsLocked,
                    IsProceed = option.IsProceed,
                    WasChosen = option.WasChosen
                };

                // Extract relic info if present
                if (option.Relic != null)
                {
                    optionDto.RelicId = option.Relic.Id.Entry;
                    optionDto.RelicName = StripGameTags(option.Relic.Title.GetFormattedText());
                }

                options.Add(optionDto);
                index++;
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build option at index {index}: {ex.Message}");
                index++;
            }
        }

        return options;
    }
}
