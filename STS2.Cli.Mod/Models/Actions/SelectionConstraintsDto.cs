namespace STS2.Cli.Mod.Models.Actions;

/// <summary>
///     Constraints for card selection screens (min/max cards, skip allowed).
///     Used by potion and deck card selection handlers.
/// </summary>
public class SelectionConstraintsDto
{
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; }
    public bool CanSkip { get; set; }
}
