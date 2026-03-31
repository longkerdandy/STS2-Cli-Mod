# Crystal Sphere Mini-Game — Implementation Design

Feature 2.1 from [missing-features.md](missing-features.md). The Crystal Sphere is an event-driven mini-game where the player uses divination tools to reveal hidden items on a grid.

## Game Mechanics Summary

### Event Trigger

- **Class**: `CrystalSphere : EventModel` (sealed)
- **Conditions**: All players have >= 100 gold, current Act index > 0 (not Act 1)
- **Non-deterministic** (`IsDeterministic = false`)

### Two Entry Options

| Option | Cost | Divinations |
|---|---|---|
| **Uncover Future** | 50 + random(1..50) gold | 3 |
| **Payment Plan** | Add `Debt` curse to deck | 6 |

### Grid Layout

- **Size**: 11x11 (121 cells)
- **Initial state**: Four corners and their adjacent cells are pre-cleared (visible)
- **Cell model**: `CrystalSphereCell` with properties `X`, `Y`, `Item?`, `IsHidden`, `IsHighlighted`, `IsHovered`

### Divination Tools

| Tool | Effect | Node Path |
|---|---|---|
| **Big** (default) | Clears clicked cell + 8 adjacent (3x3 area) | `%BigDivinationButton` |
| **Small** | Clears only the clicked cell (1 cell) | `%SmallDivinationButton` |

Tool selection is done via `NDivinationButton` nodes. The active tool is indicated by an `%Outline` child node visibility.

### Hidden Items

| Item | Count | Size | Reward |
|---|---|---|---|
| Relic | 1 | 4x4 | `RelicReward` |
| Common Potion | 2 | 1x3 | `PotionReward` |
| Rare Potion | 1 | 2x2 | `PotionReward` |
| Common Card | 1 | 2x2 | 3 Common cards to choose |
| Uncommon Card | 1 | 2x2 | 3 Uncommon cards to choose |
| Rare Card | 1 | 2x2 | 3 Rare cards to choose |
| Curse | 1 | 2x2 | Adds `Doubt` curse to deck |
| Small Gold | 5 | 1x1 | 10 gold each |
| Big Gold | 2 | 2x1 | 30 gold each |

Items are placed randomly (up to 10 placement attempts). An item is **revealed** when all cells it occupies are cleared. After all divinations are used, revealed rewards are offered via `RewardsCmd.OfferCustom`.

## Key Game Classes

### Data Layer (`MegaCrit.Sts2.Core.Events.Custom.CrystalSphereEvent`)

| Class | Role |
|---|---|
| `CrystalSphereMinigame` | Core game logic: grid init, tool switching, cell clearing, divination count, item reveal, completion |
| `CrystalSphereCell` | Single cell data: `X`, `Y`, `Item?`, `IsHidden`, `IsHighlighted`, `IsHovered`. Events: `FogUpdated`, `HighlightUpdated` |
| `CrystalSphereItem` (abstract) | Base item: `Size` (Vector2I), `IsGood` (bool), `Position`, `PlaceItem()`, `RevealItem()`. Event: `Revealed` |
| `CrystalSphereRelic` | 4x4, `IsGood = true` |
| `CrystalSpherePotion` | Common 1x3 / Rare 2x2, `IsGood = true` |
| `CrystalSphereCardReward` | 2x2, Common/Uncommon/Rare variants, `IsGood = true` |
| `CrystalSphereGold` | Small 1x1 (10g) / Big 2x1 (30g), `IsGood = true` |
| `CrystalSphereCurse` | 2x2, `IsGood = false`, adds `Doubt` curse |

Key properties on `CrystalSphereMinigame`:
- `Cells` — 2D grid of `CrystalSphereCell`
- `Items` — list of placed `CrystalSphereItem`
- `DivinationCount` — remaining uses (decremented on each click)
- `ToolType` — current tool (`CrystalSphereToolType.Big` or `.Small`)
- `IsFinished` — true when `DivinationCount == 0`
- Events: `DivinationCountChanged`, `Finished`

### UI Layer (`MegaCrit.Sts2.Core.Nodes.Events.Custom.CrystalSphere`)

| Class | Role |
|---|---|
| `NCrystalSphereScreen` | Main screen (extends `Control`, implements `IOverlayScreen`). Creates 11x11 grid of `NCrystalSphereCell` nodes at 57px intervals. Manages divination buttons, proceed button, item rendering |
| `NCrystalSphereCell` | Clickable cell (extends `NClickableControl`). Wraps `CrystalSphereCell Entity`. Fog tween animations on reveal |
| `NCrystalSphereItem` | Item display node |
| `NCrystalSphereMask` | Shader-based fog mask |
| `NCrystalSphereDialogue` | NPC dialogue bubble |
| `NDivinationButton` | Tool switch button (extends `NButton`). `SetActive(bool)` toggles `%Outline` visibility |

Key UI nodes on `NCrystalSphereScreen`:
- `%BigDivinationButton` — Big tool button
- `%SmallDivinationButton` — Small tool button
- `%DivinationsLeft` — Label showing remaining count
- `%ProceedButton` — `NProceedButton`, enabled after minigame finishes
- `%InstructionsTitle` / `%InstructionsDescription` — Instruction labels

### AutoSlay Reference (`CrystalSphereScreenHandler`)

The game's built-in auto-play handler provides a reference for how to interact:
- Clicks cells via `cell.EmitSignal(NClickableControl.SignalName.Released, cell)`
- Checks `cell.Entity.IsHidden && cell.Visible` for clickable state
- Clicks proceed via `proceedButton.ForceClick()`
- Polls for child overlay screens (rewards) appearing mid-click
- 500ms delay between clicks, 120s overall timeout

## Anti-Cheat: Hidden Cell Data Protection

`CrystalSphereCell.Item` is a `public` property that is set **before** the cell is revealed. This means the mod can read every hidden cell's item type and position — effectively a full "X-ray" of the board.

**Design rule**: The state builder MUST NOT expose `cell.Item` when `cell.IsHidden == true`. Only cells with `IsHidden == false` should have their `ItemType` and `IsGood` fields populated. The `RevealedItems` list must also only include items whose **every** occupied cell has been cleared.

This ensures the AI agent operates with the same information as a human player — it can only see items after they have been naturally uncovered by divination.

```csharp
// CORRECT — only expose item info for revealed cells
if (!cell.IsHidden && cell.Item != null)
{
    dto.ItemType = cell.Item.GetType().Name;
    dto.IsGood = cell.Item.IsGood;
}

// WRONG — this would leak hidden item positions (cheating)
if (cell.Item != null)
{
    dto.ItemType = cell.Item.GetType().Name;
}
```

Similarly, the `RevealedItems` list must verify all occupied cells are cleared before including an item:

```csharp
// For each item, check ALL cells it occupies are no longer hidden
bool allRevealed = true;
for (int dx = 0; dx < item.Size.X; dx++)
    for (int dy = 0; dy < item.Size.Y; dy++)
        if (grid[item.Position.X + dx, item.Position.Y + dy].IsHidden)
            allRevealed = false;

if (allRevealed)
    revealed.Add(new CrystalSphereRevealedItemDto { ... });
```

## Implementation Plan

### 1. Screen Detection

Add `CRYSTAL_SPHERE` to `StateHandler.DetectScreen()`.

`NCrystalSphereScreen` implements `IOverlayScreen`, so it appears in `NOverlayStack`. Detection follows the same pattern as `BUNDLE_SELECT` — check both `Peek()` and `GetChildren()`.

**Location**: `STS2.Cli.Mod/State/StateHandler.cs`

```csharp
// In the combat overlay check block (after BUNDLE_SELECT):
if (topOverlay is NCrystalSphereScreen)
{
    Logger.Info("Detected CRYSTAL_SPHERE screen during combat");
    return "CRYSTAL_SPHERE";
}

// In the non-combat overlay check block (after NChooseABundleSelectionScreen):
if (overlay is NCrystalSphereScreen)
{
    Logger.Info("Detected CRYSTAL_SPHERE screen");
    return "CRYSTAL_SPHERE";
}
```

Also add children iteration checks, same as existing overlays. Crystal Sphere is an event overlay — it should be checked after `NChooseABundleSelectionScreen` but before `NRewardsScreen` (because the minigame finishes by offering rewards via `RewardsCmd.OfferCustom`, which pushes `NRewardsScreen` on top).

**Import**: `using MegaCrit.Sts2.Core.Nodes.Events.Custom.CrystalSphere;`

### 2. State DTO

**New file**: `STS2.Cli.Mod/Models/State/CrystalSphereStateDto.cs`

```csharp
namespace STS2.Cli.Mod.Models.State;

[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class CrystalSphereStateDto
{
    /// <summary>Grid width (always 11).</summary>
    public int GridWidth { get; set; }

    /// <summary>Grid height (always 11).</summary>
    public int GridHeight { get; set; }

    /// <summary>All cells in the grid, ordered by Y then X.</summary>
    public List<CrystalSphereCellDto> Cells { get; set; } = [];

    /// <summary>Convenience list of clickable cell coordinates.</summary>
    public List<CrystalSphereCellPositionDto> ClickableCells { get; set; } = [];

    /// <summary>Items that have been fully revealed.</summary>
    public List<CrystalSphereRevealedItemDto> RevealedItems { get; set; } = [];

    /// <summary>Current tool: "big" or "small".</summary>
    public string Tool { get; set; } = "big";

    /// <summary>Whether the big divination button is available.</summary>
    public bool CanUseBigTool { get; set; }

    /// <summary>Whether the small divination button is available.</summary>
    public bool CanUseSmallTool { get; set; }

    /// <summary>Remaining divination uses.</summary>
    public int DivinationsLeft { get; set; }

    /// <summary>Whether the proceed button is enabled (minigame finished).</summary>
    public bool CanProceed { get; set; }
}

public class CrystalSphereCellDto
{
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>True if cell is still hidden (fog).</summary>
    public bool IsHidden { get; set; }

    /// <summary>True if the cell can be clicked (IsHidden and Visible).</summary>
    public bool IsClickable { get; set; }

    /// <summary>Item type name if revealed, null otherwise.</summary>
    public string? ItemType { get; set; }

    /// <summary>Whether the item on this cell is "good" (non-curse). Null if no item or hidden.</summary>
    public bool? IsGood { get; set; }
}

public class CrystalSphereCellPositionDto
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class CrystalSphereRevealedItemDto
{
    /// <summary>Item class name (e.g., "CrystalSphereRelic", "CrystalSphereGold").</summary>
    public string ItemType { get; set; } = "";

    /// <summary>Whether this item is beneficial.</summary>
    public bool IsGood { get; set; }

    /// <summary>Item position on the grid (top-left corner).</summary>
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>Item dimensions.</summary>
    public int Width { get; set; }
    public int Height { get; set; }
}
```

**Add to `GameStateDto`**:

```csharp
/// <summary>
///     Crystal Sphere mini-game state if the Crystal Sphere overlay is open, null otherwise.
/// </summary>
public CrystalSphereStateDto? CrystalSphere { get; set; }
```

### 3. State Builder

**New file**: `STS2.Cli.Mod/State/Builders/CrystalSphereStateBuilder.cs`

```csharp
namespace STS2.Cli.Mod.State.Builders;

public static class CrystalSphereStateBuilder
{
    private static readonly ModLogger Logger = new("CrystalSphereStateBuilder");

    public static CrystalSphereStateDto? Build()
    {
        var screen = CommonUiUtils.FindScreenInOverlay<NCrystalSphereScreen>();
        return screen == null ? null : Build(screen);
    }

    public static CrystalSphereStateDto? Build(NCrystalSphereScreen screen)
    {
        try
        {
            var entity = screen.Entity; // CrystalSphereMinigame
            var cells = new List<CrystalSphereCellDto>();
            var clickable = new List<CrystalSphereCellPositionDto>();

            // Iterate all NCrystalSphereCell nodes
            var cellNodes = CommonUiUtils.FindAll<NCrystalSphereCell>(screen);
            foreach (var cellNode in cellNodes)
            {
                var cell = cellNode.Entity;
                var isClickable = cell.IsHidden && cellNode.Visible;

                var dto = new CrystalSphereCellDto
                {
                    X = cell.X,
                    Y = cell.Y,
                    IsHidden = cell.IsHidden,
                    IsClickable = isClickable
                };

                // Only expose item info for revealed cells
                if (!cell.IsHidden && cell.Item != null)
                {
                    dto.ItemType = cell.Item.GetType().Name;
                    dto.IsGood = cell.Item.IsGood;
                }

                cells.Add(dto);
                if (isClickable)
                    clickable.Add(new CrystalSphereCellPositionDto { X = cell.X, Y = cell.Y });
            }

            // Sort by Y then X for consistent ordering
            cells.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            clickable.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));

            // Extract revealed items (deduplicated via HashSet of positions)
            var revealed = new List<CrystalSphereRevealedItemDto>();
            var seen = new HashSet<(int, int)>();
            foreach (var item in entity.Items)
            {
                var pos = (item.Position.X, item.Position.Y);
                if (seen.Contains(pos)) continue;

                // Check if all cells for this item are cleared
                // (item fires Revealed event when this happens, but we check directly)
                bool allCleared = true;
                // ... check via item cells or use the Revealed state
                // Simplification: items in the Items list that have been revealed
                // We can check by looking at cell IsHidden status for all occupied cells
                seen.Add(pos);
                // Item reveal detection logic needed here
            }

            // Detect current tool via button outline visibility
            var bigButton = screen.GetNodeOrNull<Control>("%BigDivinationButton");
            var smallButton = screen.GetNodeOrNull<Control>("%SmallDivinationButton");
            var tool = DetectActiveTool(bigButton, smallButton);

            // Divinations left
            var divLabel = screen.GetNodeOrNull<Label>("%DivinationsLeft");
            var divLeft = 0;
            if (divLabel != null && int.TryParse(divLabel.Text, out var parsed))
                divLeft = parsed;

            // Proceed button
            var proceedButton = screen.GetNodeOrNull<NProceedButton>("%ProceedButton");
            var canProceed = proceedButton is { IsEnabled: true };

            return new CrystalSphereStateDto
            {
                GridWidth = 11, // entity.Width if accessible, else hardcode
                GridHeight = 11,
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

    private static string DetectActiveTool(Control? bigBtn, Control? smallBtn)
    {
        // Active tool has its %Outline child visible
        var bigOutline = bigBtn?.GetNodeOrNull<Control>("%Outline");
        var smallOutline = smallBtn?.GetNodeOrNull<Control>("%Outline");

        if (smallOutline?.Visible == true) return "small";
        if (bigOutline?.Visible == true) return "big";
        return "none";
    }
}
```

**Key implementation notes**:

- `NCrystalSphereScreen.Entity` gives access to `CrystalSphereMinigame` — need to verify this property exists via reflection or decompilation. If not directly accessible, use `CommonUiUtils.FindAll<NCrystalSphereCell>()` and read `cell.Entity` from each cell node.
- **Revealed item detection**: Iterate `entity.Items`, for each item check if ALL cells at its occupied positions have `IsHidden == false`. Alternatively, listen for `item.Revealed` events, but state extraction is point-in-time — checking cell hidden status is simpler.
- **Tool detection**: Read `%Outline` visibility on divination buttons (same approach as STS2MCP).
- **Divination count**: Read `%DivinationsLeft` label text and parse as integer. Alternatively read `entity.DivinationCount` directly if accessible.

### 4. Action Handlers

Three CLI commands, all async (cell clicks trigger animations, proceed triggers reward screen):

**New file**: `STS2.Cli.Mod/Actions/CrystalSphereHandler.cs`

#### `crystal_set_tool` — Switch divination tool

```
sts2 crystal_set_tool <tool>
```

- **Argument**: `tool` — `"big"` or `"small"`
- **Guard**: Screen is `NCrystalSphereScreen` (via `CommonUiUtils.FindScreenInOverlay`)
- **Guard**: Tool button is visible (buttons are hidden after minigame finishes)
- **Guard**: Requested tool is not already active
- **Action**: `ForceClick()` on `%BigDivinationButton` or `%SmallDivinationButton`
- **Wait**: `PostClickDelayMs` (200ms) for outline toggle animation
- **Response**: `{ ok: true, data: { action: "CRYSTAL_SET_TOOL", tool: "big"|"small", screen } }`
- **Error codes**: `NOT_IN_CRYSTAL_SPHERE`, `INVALID_TOOL`, `TOOL_NOT_AVAILABLE`, `TOOL_ALREADY_ACTIVE`

#### `crystal_click_cell` — Click a grid cell

```
sts2 crystal_click_cell <x> <y>
```

- **Arguments**: `x`, `y` — cell coordinates (0-based)
- **Guard**: Screen is `NCrystalSphereScreen`
- **Guard**: Minigame not finished (`DivinationsLeft > 0` or proceed button not enabled)
- **Guard**: Cell at (x, y) exists, is hidden, and is visible (clickable)
- **Action**: `cell.EmitSignal(NClickableControl.SignalName.Released, cell)` (same as AutoSlay — `ForceClick()` might also work but `EmitSignal` is the proven pattern)
- **Wait**: `PreviewAppearDelayMs` (300ms) for fog animation, then check if a child overlay appeared (reward screen from item reveal). If the minigame is now finished, poll for proceed button to become enabled.
- **Response**: `{ ok: true, data: { action: "CRYSTAL_CLICK_CELL", x, y, divinations_left, screen } }`
- **Error codes**: `NOT_IN_CRYSTAL_SPHERE`, `MINIGAME_FINISHED`, `CELL_NOT_FOUND`, `CELL_NOT_CLICKABLE`

**Important**: After clicking, a reward overlay (`NRewardsScreen` or `NCardRewardSelectionScreen`) may push on top of the Crystal Sphere screen if an item was revealed. The handler should detect this and report the resulting screen accurately (could be `REWARD`, `CARD_REWARD`, or still `CRYSTAL_SPHERE`).

#### `crystal_proceed` — Leave the minigame

```
sts2 crystal_proceed
```

- **Guard**: Screen is `NCrystalSphereScreen`
- **Guard**: Proceed button is enabled (`NProceedButton.IsEnabled`)
- **Action**: `proceedButton.ForceClick()`
- **Wait**: `PollUntilAsync` for overlay removal (same pattern as `BundleSelectHandler.ExecuteConfirmAsync`)
- **Response**: `{ ok: true, data: { action: "CRYSTAL_PROCEED", screen } }`
- **Error codes**: `NOT_IN_CRYSTAL_SPHERE`, `CANNOT_PROCEED`

### 5. Pipe Server Registration

**File**: `STS2.Cli.Mod/Server/PipeServer.cs`

Add three entries to the `ProcessRequestAsync` switch:

```csharp
"crystal_set_tool" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
    CrystalSphereHandler.HandleSetToolAsync(request)),

"crystal_click_cell" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
    CrystalSphereHandler.HandleClickCellAsync(request)),

"crystal_proceed" => await MainThreadExecutor.RunOnMainThreadAsync(() =>
    CrystalSphereHandler.HandleProceedAsync(request)),
```

### 6. CLI Commands

**New file**: `STS2.Cli.Cmd/Commands/CrystalSetToolCommand.cs`

```csharp
internal static class CrystalSetToolCommand
{
    public static Command Create()
    {
        var cmd = new Command("crystal_set_tool",
            "Set the divination tool (big or small) in the Crystal Sphere mini-game");
        var toolArg = new Argument<string>("tool") { Description = "Tool type: 'big' or 'small'" };
        cmd.Arguments.Add(toolArg);
        cmd.SetHandler(async (tool, pretty) =>
        {
            await CommandExecutor.ExecuteAsync("crystal_set_tool", pretty, tool);
        }, toolArg, CommandExecutor.PrettyOption);
        return cmd;
    }
}
```

**New file**: `STS2.Cli.Cmd/Commands/CrystalClickCellCommand.cs`

```csharp
internal static class CrystalClickCellCommand
{
    public static Command Create()
    {
        var cmd = new Command("crystal_click_cell",
            "Click a cell in the Crystal Sphere mini-game");
        var xArg = new Argument<int>("x") { Description = "Cell X coordinate (0-based)" };
        var yArg = new Argument<int>("y") { Description = "Cell Y coordinate (0-based)" };
        cmd.Arguments.Add(xArg);
        cmd.Arguments.Add(yArg);
        cmd.SetHandler(async (x, y, pretty) =>
        {
            await CommandExecutor.ExecuteAsync("crystal_click_cell", pretty, x.ToString(), y.ToString());
        }, xArg, yArg, CommandExecutor.PrettyOption);
        return cmd;
    }
}
```

`crystal_proceed` uses `SimpleCommand.Create()` (no arguments).

**Register in `Program.cs`**:

```csharp
// Crystal Sphere commands
rootCommand.Subcommands.Add(CrystalSetToolCommand.Create());
rootCommand.Subcommands.Add(CrystalClickCellCommand.Create());
rootCommand.Subcommands.Add(SimpleCommand.Create("crystal_proceed",
    "Leave the Crystal Sphere mini-game after divinations are complete"));
```

### 7. State Extraction in StateHandler

**File**: `STS2.Cli.Mod/State/StateHandler.cs`

```csharp
// Extract Crystal Sphere mini-game state if the overlay is open
if (state.Screen == "CRYSTAL_SPHERE") state.CrystalSphere = ExtractCrystalSphereState();
```

Plus the extraction method:

```csharp
private static CrystalSphereStateDto? ExtractCrystalSphereState()
{
    try
    {
        return CrystalSphereStateBuilder.Build();
    }
    catch (Exception ex)
    {
        Logger.Error($"Failed to extract Crystal Sphere state: {ex.Message}");
        return null;
    }
}
```

## State JSON Example

```json
{
  "screen": "CRYSTAL_SPHERE",
  "crystal_sphere": {
    "grid_width": 11,
    "grid_height": 11,
    "cells": [
      { "x": 0, "y": 0, "is_hidden": false, "is_clickable": false },
      { "x": 1, "y": 0, "is_hidden": true, "is_clickable": true },
      { "x": 2, "y": 0, "is_hidden": false, "is_clickable": false,
        "item_type": "CrystalSphereGold", "is_good": true }
    ],
    "clickable_cells": [
      { "x": 1, "y": 0 },
      { "x": 5, "y": 3 }
    ],
    "revealed_items": [
      { "item_type": "CrystalSphereGold", "is_good": true,
        "x": 2, "y": 0, "width": 1, "height": 1 }
    ],
    "tool": "big",
    "can_use_big_tool": true,
    "can_use_small_tool": true,
    "divinations_left": 2,
    "can_proceed": false
  },
  "timestamp": 1711900000000
}
```

## Implementation Risks and Open Questions

### Risk 1: Accessing `NCrystalSphereScreen.Entity`

The `Entity` property on `NCrystalSphereScreen` gives access to `CrystalSphereMinigame`. Need to verify it is a public property (decompilation shows it as a Godot-bound property). If not public, use reflection or fall back to reading state purely from UI nodes (`NCrystalSphereCell.Entity` on each cell).

### Risk 2: Revealed Item Detection

`CrystalSphereItem` has a `Revealed` event but no `IsRevealed` boolean property. Two approaches:
1. **Cell-based**: For each item, check if all cells at `(Position.X..Position.X+Size.X-1, Position.Y..Position.Y+Size.Y-1)` have `IsHidden == false`. This is reliable but requires accessing the grid from `CrystalSphereMinigame`.
2. **UI-based**: Check if `NCrystalSphereItem` nodes are visible. Simpler but may miss timing edge cases.

**Recommendation**: Use approach 1 (cell-based) for reliability.

### Risk 3: Reward Overlay During Minigame

When items are revealed, the game may push reward overlays (`NRewardsScreen`) on top of `NCrystalSphereScreen`. The screen detection must handle this correctly:
- If a reward overlay is on top, `DetectScreen()` will return `REWARD` or `CARD_REWARD` (which is correct — the player needs to handle the reward first)
- After claiming/skipping the reward, the Crystal Sphere screen becomes the top overlay again → `CRYSTAL_SPHERE`

This matches existing behavior — no special handling needed.

### Risk 4: Cell Coordinate System

The decompiled code shows cells are placed at 57-pixel intervals in `NCrystalSphereScreen`. The logical coordinates are `(X, Y)` from `CrystalSphereCell`. Need to verify:
- Coordinates start at (0, 0) — confirmed from decompilation
- Range is 0..10 for both X and Y (11x11 grid)

### ~~Risk 5: Command Name Length~~ Resolved

Commands use the `crystal_` prefix (without `sphere`): `crystal_set_tool`, `crystal_click_cell`, `crystal_proceed`. This keeps names concise while remaining unambiguous.

## File Checklist

| File | Action | Description |
|---|---|---|
| `STS2.Cli.Mod/Models/State/CrystalSphereStateDto.cs` | **Create** | DTO classes for Crystal Sphere state |
| `STS2.Cli.Mod/Models/State/GameStateDto.cs` | **Edit** | Add `CrystalSphere` property |
| `STS2.Cli.Mod/State/Builders/CrystalSphereStateBuilder.cs` | **Create** | State extraction from `NCrystalSphereScreen` |
| `STS2.Cli.Mod/State/StateHandler.cs` | **Edit** | Add `CRYSTAL_SPHERE` detection + extraction |
| `STS2.Cli.Mod/Actions/CrystalSphereHandler.cs` | **Create** | Three async handlers: set_tool, click_cell, proceed |
| `STS2.Cli.Mod/Server/PipeServer.cs` | **Edit** | Register three new commands |
| `STS2.Cli.Cmd/Commands/CrystalSetToolCommand.cs` | **Create** | CLI command for crystal_set_tool |
| `STS2.Cli.Cmd/Commands/CrystalClickCellCommand.cs` | **Create** | CLI command for crystal_click_cell |
| `STS2.Cli.Cmd/Program.cs` | **Edit** | Register three CLI commands (crystal_proceed uses SimpleCommand) |
| `docs/cli-reference.md` | **Edit** | Add Crystal Sphere command docs |
| `docs/missing-features.md` | **Edit** | Mark 2.1 as done |
| `AGENTS.md` | **Edit** | Add Crystal Sphere test commands |

