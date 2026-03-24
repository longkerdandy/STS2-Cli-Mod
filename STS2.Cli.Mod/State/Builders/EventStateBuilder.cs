using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2.Cli.Mod.Models.State;
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
    ///     Cached reflection field for <see cref="NAncientEventLayout" />._dialogue (private).
    /// </summary>
    private static readonly FieldInfo? AncientDialogueField =
        typeof(NAncientEventLayout).GetField("_dialogue", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    ///     Cached reflection field for <see cref="NAncientEventLayout" />._currentDialogueLine (private).
    /// </summary>
    private static readonly FieldInfo? AncientCurrentLineField =
        typeof(NAncientEventLayout).GetField("_currentDialogueLine", BindingFlags.NonPublic | BindingFlags.Instance);

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

            var options = BuildOptions(eventModel);

            // When event is finished, the game UI injects a synthetic "proceed" option
            // via NEventRoom.SetOptions() that is NOT reflected in CurrentOptions.
            // We mirror this behavior so AI agents see a proceed option they can select.
            if (eventModel.IsFinished && options.Count == 0)
            {
                options.Add(new EventOptionDto
                {
                    Index = 0,
                    Title = "Proceed",
                    IsProceed = true
                });
            }

            var result = new EventStateDto
            {
                EventId = eventModel.Id.Entry,
                Title = SafeGetText(eventModel.Title) ?? eventModel.Id.Entry,
                Description = SafeGetText(eventModel.Description),
                LayoutType = eventModel.LayoutType.ToString(),
                IsFinished = eventModel.IsFinished,
                Options = options
            };

            // Detect Ancient layout and extract dialogue info
            ExtractAncientDialogueInfo(eventRoom, result);

            Logger.Info($"Built event state for '{result.EventId}' with {result.Options.Count} options, IsInDialogue={result.IsInDialogue}");
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
                    Title = SafeGetText(option.Title) ?? option.TextKey ?? $"Option {index}",
                    Description = SafeGetText(option.Description),
                    TextKey = option.TextKey,
                    IsLocked = option.IsLocked,
                    IsProceed = option.IsProceed,
                    WasChosen = option.WasChosen
                };

                // Extract relic info if present
                if (option.Relic != null)
                {
                    optionDto.RelicId = option.Relic.Id.Entry;
                    optionDto.RelicName = SafeGetText(option.Relic.Title);
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

    /// <summary>
    ///     Detects Ancient layout and extracts dialogue information.
    /// </summary>
    private static void ExtractAncientDialogueInfo(NEventRoom eventRoom, EventStateDto result)
    {
        try
        {
            // Check if layout is NAncientEventLayout
            if (eventRoom.Layout is not NAncientEventLayout ancientLayout)
            {
                // Not an Ancient event, leave dialogue fields as default (false/null)
                return;
            }

            // Access IsDialogueOnLastLine property
            var isDialogueOnLastLineProperty = typeof(NAncientEventLayout).GetProperty("IsDialogueOnLastLine");
            bool isDialogueOnLastLine = isDialogueOnLastLineProperty?.GetValue(ancientLayout) as bool? ?? true;

            // We're in dialogue phase if not on the last line
            result.IsInDialogue = !isDialogueOnLastLine;

            if (result.IsInDialogue)
            {
                // Extract current line index
                var currentLine = AncientCurrentLineField?.GetValue(ancientLayout) as int?;
                result.CurrentDialogueLine = currentLine;

                // Extract total lines count
                var dialogue = AncientDialogueField?.GetValue(ancientLayout) as System.Collections.IList;
                if (dialogue != null)
                {
                    result.TotalDialogueLines = dialogue.Count;
                }

                Logger.Info($"Ancient event dialogue: line {currentLine + 1} of {dialogue?.Count}");
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to extract Ancient dialogue info: {ex.Message}");
            // Don't fail the whole state extraction, just leave dialogue fields unset
        }
    }

    /// <summary>
    ///     Safely resolves a <see cref="LocString" /> to its formatted text.
    ///     Returns null if the <see cref="LocString" /> is null or its localization key does not exist in the table.
    ///     This prevents <c>LocException</c> from being thrown for missing keys (e.g., Neow event).
    /// </summary>
    private static string? SafeGetText(LocString? locString)
    {
        if (locString == null)
            return null;

        try
        {
            if (!locString.Exists())
            {
                Logger.Warning($"LocString key not found: table={locString.LocTable}, key={locString.LocEntryKey}");
                return null;
            }

            return StripGameTags(locString.GetFormattedText());
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to resolve LocString: {ex.Message}");
            return null;
        }
    }
}
