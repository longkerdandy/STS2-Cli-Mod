namespace STS2.Cli.Mod.Models.Actions;

/// <summary>
///     Constraints for card selection screens (min/max cards, skip allowed).
///     Used by potion and deck card selection handlers.
/// </summary>
public readonly struct SelectionConstraints
{
    public int MinSelect { get; }
    public int MaxSelect { get; }
    public bool CanSkip { get; }

    public SelectionConstraints(int minSelect, int maxSelect, bool canSkip)
    {
        MinSelect = minSelect;
        MaxSelect = maxSelect;
        CanSkip = canSkip;
    }
}
