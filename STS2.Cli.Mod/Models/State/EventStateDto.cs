using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.State;

/// <summary>
///     Event state DTO representing the current event room state.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class EventStateDto
{
    /// <summary>
    ///     Event identifier (ModelId as string).
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    ///     Localized event title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     Localized event description (current page).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Layout type: "Default", "Combat", "Ancient", "Custom".
    /// </summary>
    public string LayoutType { get; set; } = "Default";

    /// <summary>
    ///     Whether the event has concluded.
    /// </summary>
    public bool IsFinished { get; set; }

    /// <summary>
    ///     Current available options.
    /// </summary>
    public List<EventOptionDto> Options { get; set; } = [];

    /// <summary>
    ///     For Ancient events: whether we're currently in the dialogue phase.
    ///     True when dialogue is still in progress, false when options are available.
    /// </summary>
    public bool IsInDialogue { get; set; }

    /// <summary>
    ///     For Ancient events: current dialogue line index (0-based).
    ///     Null for non-Ancient events or when dialogue is finished.
    /// </summary>
    public int? CurrentDialogueLine { get; set; }

    /// <summary>
    ///     For Ancient events: total number of dialogue lines.
    ///     Null for non-Ancient events or when dialogue is finished.
    /// </summary>
    public int? TotalDialogueLines { get; set; }
}
