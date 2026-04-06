using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Root reward state DTO containing the list of available rewards on the reward screen.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "CollectionNeverQueried.Global")]
public class RewardStateDto
{
    /// <summary>
    ///     List of available rewards on the reward screen.
    ///     Ordered by their position in the UI (index 0 = top reward).
    /// </summary>
    public List<RewardItemDto> Rewards { get; set; } = [];
}
