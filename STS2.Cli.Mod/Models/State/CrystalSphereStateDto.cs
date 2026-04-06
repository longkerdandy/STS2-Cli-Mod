using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Crystal Sphere mini-game state DTO. Represents the divination grid overlay
///     that appears during the Crystal Sphere event. The player uses Big or Small
///     divination tools to clear fog from cells and reveal hidden items.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class CrystalSphereStateDto
{
    /// <summary>
    ///     Grid width (always 11).
    /// </summary>
    public int GridWidth { get; set; }

    /// <summary>
    ///     Grid height (always 11).
    /// </summary>
    public int GridHeight { get; set; }

    /// <summary>
    ///     All cells in the grid, ordered by Y then X.
    ///     Hidden cells do NOT expose item information (anti-cheat).
    /// </summary>
    public List<CrystalSphereCellDto> Cells { get; set; } = [];

    /// <summary>
    ///     Convenience list of clickable cell coordinates (hidden and visible cells).
    /// </summary>
    public List<CrystalSphereCellPositionDto> ClickableCells { get; set; } = [];

    /// <summary>
    ///     Items that have been fully revealed (all occupied cells cleared).
    /// </summary>
    public List<CrystalSphereRevealedItemDto> RevealedItems { get; set; } = [];

    /// <summary>
    ///     Current divination tool: "big" or "small".
    /// </summary>
    public required string Tool { get; set; }

    /// <summary>
    ///     Whether the big divination button is available.
    /// </summary>
    public bool CanUseBigTool { get; set; }

    /// <summary>
    ///     Whether the small divination button is available.
    /// </summary>
    public bool CanUseSmallTool { get; set; }

    /// <summary>
    ///     Remaining divination uses.
    /// </summary>
    public int DivinationsLeft { get; set; }

    /// <summary>
    ///     Whether the proceed button is enabled (minigame finished).
    /// </summary>
    public bool CanProceed { get; set; }
}

/// <summary>
///     A single cell in the Crystal Sphere grid.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class CrystalSphereCellDto
{
    /// <summary>
    ///     X coordinate (0-based, range 0..10).
    /// </summary>
    public int X { get; set; }

    /// <summary>
    ///     Y coordinate (0-based, range 0..10).
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    ///     True if the cell is still hidden (fog). Item info is NOT exposed for hidden cells.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    ///     True if the cell can be clicked (hidden and visible in the UI).
    /// </summary>
    public bool IsClickable { get; set; }

    /// <summary>
    ///     Item type name if the cell is revealed and contains an item, null otherwise.
    ///     Example values: "CrystalSphereRelic", "CrystalSphereGold", "CrystalSphereCurse".
    /// </summary>
    public string? ItemType { get; set; }

    /// <summary>
    ///     Whether the item on this cell is beneficial (non-curse).
    ///     Null if no item or cell is hidden.
    /// </summary>
    public bool? IsGood { get; set; }
}

/// <summary>
///     A cell position in the Crystal Sphere grid (used for clickable cell list).
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class CrystalSphereCellPositionDto
{
    /// <summary>
    ///     X coordinate (0-based).
    /// </summary>
    public int X { get; set; }

    /// <summary>
    ///     Y coordinate (0-based).
    /// </summary>
    public int Y { get; set; }
}

/// <summary>
///     A fully revealed item in the Crystal Sphere grid.
///     Only included when ALL cells the item occupies have been cleared.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class CrystalSphereRevealedItemDto
{
    /// <summary>
    ///     Item class name (e.g., "CrystalSphereRelic", "CrystalSphereGold", "CrystalSphereCurse").
    /// </summary>
    public required string ItemType { get; set; }

    /// <summary>
    ///     Whether this item is beneficial (true for all items except curse).
    /// </summary>
    public bool IsGood { get; set; }

    /// <summary>
    ///     Item position X on the grid (top-left corner of the item).
    /// </summary>
    public int X { get; set; }

    /// <summary>
    ///     Item position Y on the grid (top-left corner of the item).
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    ///     Item width in cells.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     Item height in cells.
    /// </summary>
    public int Height { get; set; }
}
