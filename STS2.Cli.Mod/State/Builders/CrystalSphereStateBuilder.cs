using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Events.Custom.CrystalSphereEvent;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Events.Custom.CrystalSphere;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds the Crystal Sphere mini-game state DTO from <see cref="NCrystalSphereScreen" />.
///     Extracts grid cells, divination tool state, revealed items, and proceed button availability.
///     Hidden cells do NOT expose item information to prevent cheating (anti-cheat).
/// </summary>
public static class CrystalSphereStateBuilder
{
    private static readonly ModLogger Logger = new("CrystalSphereStateBuilder");

    /// <summary>
    ///     Cached reflection accessor for the private <c>_entity</c> field on
    ///     <see cref="NCrystalSphereScreen" />, which holds the
    ///     <see cref="CrystalSphereMinigame" /> instance.
    /// </summary>
    private static readonly FieldInfo? EntityField =
        typeof(NCrystalSphereScreen).GetField("_entity",
            BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    ///     Builds the Crystal Sphere state from the currently open <see cref="NCrystalSphereScreen" />.
    ///     Finds the screen via <see cref="UiUtils.FindScreenInOverlay{T}" />.
    ///     Returns null if no screen is found.
    /// </summary>
    public static CrystalSphereStateDto? Build()
    {
        var screen = UiUtils.FindScreenInOverlay<NCrystalSphereScreen>();
        if (screen == null)
        {
            Logger.Warning("No NCrystalSphereScreen found in overlay stack");
            return null;
        }

        return Build(screen);
    }

    /// <summary>
    ///     Builds the Crystal Sphere state from the given <see cref="NCrystalSphereScreen" />.
    /// </summary>
    /// <param name="screen">The Crystal Sphere screen to extract data from.</param>
    /// <returns>DTO with grid, tool, divination, and proceed state; null on failure.</returns>
    private static CrystalSphereStateDto? Build(NCrystalSphereScreen screen)
    {
        try
        {
            // Access the CrystalSphereMinigame entity via reflection (_entity is private)
            var entity = GetEntity(screen);
            if (entity == null)
            {
                Logger.Warning("Failed to access CrystalSphereMinigame entity from screen");
                return null;
            }

            // Build cell list from NCrystalSphereCell UI nodes
            var cells = new List<CrystalSphereCellDto>();
            var clickable = new List<CrystalSphereCellPositionDto>();

            var cellContainer = screen.GetNodeOrNull<Control>("%Cells");
            if (cellContainer != null)
            {
                var cellNodes = UiUtils.FindAll<NCrystalSphereCell>(cellContainer);
                foreach (var cellNode in cellNodes)
                {
                    var cell = cellNode.Entity;
                    // A cell is clickable if it is still hidden AND the node is visible in the UI
                    var isClickable = cell.IsHidden && cellNode.Visible;

                    var dto = new CrystalSphereCellDto
                    {
                        X = cell.X,
                        Y = cell.Y,
                        IsHidden = cell.IsHidden,
                        IsClickable = isClickable
                    };

                    // ANTI-CHEAT: Only expose item info for revealed (non-hidden) cells.
                    // cell.Item is set before cells are cleared — reading it on hidden cells
                    // would leak item positions to the AI agent.
                    if (cell is { IsHidden: false, Item: not null })
                    {
                        dto.ItemType = cell.Item.GetType().Name;
                        dto.IsGood = cell.Item.IsGood;
                    }

                    cells.Add(dto);
                    if (isClickable)
                        clickable.Add(new CrystalSphereCellPositionDto { X = cell.X, Y = cell.Y });
                }
            }

            // Sort by Y then X for consistent ordering
            cells.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            clickable.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));

            // Build revealed items list (items where ALL occupied cells are cleared)
            var revealed = BuildRevealedItems(entity);

            // Detect current tool via button outline visibility
            var bigButton = screen.GetNodeOrNull<NDivinationButton>("%BigDivinationButton");
            var smallButton = screen.GetNodeOrNull<NDivinationButton>("%SmallDivinationButton");
            var tool = DetectActiveTool(bigButton, smallButton);

            // Divination count — read directly from entity
            var divLeft = entity.DivinationCount;

            // Proceed button
            var proceedButton = screen.GetNodeOrNull<NProceedButton>("%ProceedButton");
            var canProceed = proceedButton is { Visible: true, IsEnabled: true };

            return new CrystalSphereStateDto
            {
                GridWidth = entity.GridSize.X,
                GridHeight = entity.GridSize.Y,
                Cells = cells,
                ClickableCells = clickable,
                RevealedItems = revealed,
                Tool = tool,
                CanUseBigTool = bigButton?.Visible == true,
                CanUseSmallTool = smallButton?.Visible == true,
                DivinationsLeft = divLeft,
                CanProceed = canProceed
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build Crystal Sphere state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the <see cref="CrystalSphereMinigame" /> entity from the screen via reflection.
    ///     The <c>_entity</c> field is private on <see cref="NCrystalSphereScreen" />.
    /// </summary>
    private static CrystalSphereMinigame? GetEntity(NCrystalSphereScreen screen)
    {
        if (EntityField == null)
        {
            Logger.Warning("Could not find _entity field on NCrystalSphereScreen via reflection");
            return null;
        }

        return EntityField.GetValue(screen) as CrystalSphereMinigame;
    }

    /// <summary>
    ///     Builds the list of fully revealed items. An item is considered revealed
    ///     when ALL cells it occupies have <c>IsHidden == false</c>.
    /// </summary>
    private static List<CrystalSphereRevealedItemDto> BuildRevealedItems(CrystalSphereMinigame entity)
    {
        var revealed = new List<CrystalSphereRevealedItemDto>();
        var grid = entity.cells;

        foreach (var item in entity.Items)
        {
            // Check if all cells occupied by this item are cleared
            var allCleared = true;
            for (var dx = 0; dx < item.Size.X; dx++)
            {
                for (var dy = 0; dy < item.Size.Y; dy++)
                {
                    var cx = item.Position.X + dx;
                    var cy = item.Position.Y + dy;

                    // Bounds check (should not happen with valid placement, but be safe)
                    if (cx < 0 || cx >= grid.GetLength(0) || cy < 0 || cy >= grid.GetLength(1))
                    {
                        allCleared = false;
                        break;
                    }

                    if (grid[cx, cy].IsHidden)
                    {
                        allCleared = false;
                        break;
                    }
                }

                if (!allCleared)
                    break;
            }

            if (allCleared)
            {
                revealed.Add(new CrystalSphereRevealedItemDto
                {
                    ItemType = item.GetType().Name,
                    IsGood = item.IsGood,
                    X = item.Position.X,
                    Y = item.Position.Y,
                    Width = item.Size.X,
                    Height = item.Size.Y
                });
            }
        }

        return revealed;
    }

    /// <summary>
    ///     Detects the currently active divination tool by checking the <c>%Outline</c>
    ///     child visibility on each button. The active tool has its outline visible.
    /// </summary>
    private static string DetectActiveTool(NDivinationButton? bigBtn, NDivinationButton? smallBtn)
    {
        var bigOutline = bigBtn?.GetNodeOrNull<Control>("%Outline");
        var smallOutline = smallBtn?.GetNodeOrNull<Control>("%Outline");

        if (smallOutline?.Visible == true) return "small";
        if (bigOutline?.Visible == true) return "big";

        // Fallback: neither outline visible (should not happen during normal gameplay)
        return "none";
    }
}
