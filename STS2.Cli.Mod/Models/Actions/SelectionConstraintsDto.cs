using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.Actions;

/// <summary>
///     Constraints for card selection screens (min/max cards, skip allowed).
///     Used by potion and deck card selection handlers.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class SelectionConstraintsDto
{
    /// <summary>
    ///     Minimum number of cards that must be selected (0 if selection is optional).
    /// </summary>
    public int MinSelect { get; set; }

    /// <summary>
    ///     Maximum number of cards that can be selected.
    /// </summary>
    public int MaxSelect { get; set; }

    /// <summary>
    ///     Whether the player can skip the selection entirely without choosing any card.
    /// </summary>
    public bool CanSkip { get; set; }
}
