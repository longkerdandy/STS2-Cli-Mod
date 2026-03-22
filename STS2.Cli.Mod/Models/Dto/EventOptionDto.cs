using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.Dto;

/// <summary>
///     Event option DTO representing a single choice in an event.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class EventOptionDto
{
    /// <summary>
    ///     0-based index for choose_event command.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Localized option display text.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     Localized option description/tooltip.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Raw text key for the option.
    /// </summary>
    public string? TextKey { get; set; }

    /// <summary>
    ///     True when option cannot be selected (OnChosen == null).
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    ///     True for the final "proceed to map" option.
    /// </summary>
    public bool IsProceed { get; set; }

    /// <summary>
    ///     True if already selected in a previous page.
    /// </summary>
    public bool WasChosen { get; set; }

    /// <summary>
    ///     Relic ID if option shows a relic.
    /// </summary>
    public string? RelicId { get; set; }

    /// <summary>
    ///     Relic name if option shows a relic.
    /// </summary>
    public string? RelicName { get; set; }
}
