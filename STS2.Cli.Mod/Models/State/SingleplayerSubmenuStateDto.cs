using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Singleplayer submenu state DTO.
///     Shows which game modes are available (Standard is always available;
///     Daily and Custom may be locked behind epoch discovery).
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class SingleplayerSubmenuStateDto
{
    /// <summary>
    ///     Whether the Standard game mode is available (always true).
    /// </summary>
    public bool StandardAvailable { get; set; }

    /// <summary>
    ///     Whether the Daily Challenge game mode is unlocked.
    ///     Requires <c>DailyRunEpoch</c> to be revealed.
    /// </summary>
    public bool DailyAvailable { get; set; }

    /// <summary>
    ///     Whether the Custom Run game mode is unlocked.
    ///     Requires <c>CustomAndSeedsEpoch</c> to be revealed.
    /// </summary>
    public bool CustomAvailable { get; set; }
}