using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Rest site (campfire) state DTO containing available options and proceed status.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class RestSiteStateDto
{
    /// <summary>
    ///     Available rest site options (e.g., HEAL, SMITH, MEND, LIFT, DIG, etc.).
    /// </summary>
    public List<RestSiteOptionDto> Options { get; set; } = [];

    /// <summary>
    ///     Whether the proceed button is enabled (an option has been chosen and the player can leave).
    /// </summary>
    public bool CanProceed { get; set; }
}

/// <summary>
///     Individual rest site option (HEAL, SMITH, etc.).
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class RestSiteOptionDto
{
    /// <summary>
    ///     0-based index in the options list (used by choose_rest_option).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Option identifier (e.g., "HEAL", "SMITH", "MEND", "LIFT", "DIG", "HATCH", "COOK", "CLONE").
    /// </summary>
    public required string OptionId { get; set; }

    /// <summary>
    ///     Localized option name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Localized option description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Whether the option is currently enabled (e.g., SMITH is disabled if no upgradable cards).
    /// </summary>
    public bool IsEnabled { get; set; }
}