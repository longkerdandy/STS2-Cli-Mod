# Custom Events - Development Plan

This document outlines the development plan for custom layout event interaction. Custom events like `NFakeMerchant` have non-standard UIs that require special handling beyond the standard event option system.

---

## Overview

### Current Status

The existing event system supports three layout types:
- **Default** - Standard events with option buttons
- **Ancient** - Events with dialogue phase before options
- **Combat** - Events that can trigger mid-event combat

However, **Custom** layout events are currently not fully supported. The most notable example is `NFakeMerchant` (Fake Merchant), which features:
- A shop interface (`NMerchantInventory`) with cards, relics, and potions
- A separate Proceed button to leave the event
- Custom dialogue and animations

### Reference Implementation

From `EventRoomHandler.cs` (AutoSlayer):

```csharp
// Detection
NFakeMerchant nFakeMerchant = UiHelper.FindFirst<NFakeMerchant>(eventRoom);
if (nFakeMerchant != null)
{
    AutoSlayLog.Info("Detected custom event: FakeMerchant");
    await HandleFakeMerchantEvent(nFakeMerchant, ct);
    return true;
}

// Handling
private async Task HandleFakeMerchantEvent(NFakeMerchant fakeMerchant, CancellationToken ct)
{
    AutoSlayLog.Action("Handling FakeMerchant event");
    NProceedButton proceedButton = null;
    await WaitHelper.Until(delegate
    {
        proceedButton = UiHelper.FindFirst<NProceedButton>(fakeMerchant);
        return proceedButton != null && proceedButton.IsEnabled && proceedButton.Visible;
    }, ct, TimeSpan.FromSeconds(10L), "FakeMerchant proceed button not available");
    AutoSlayLog.Action("Clicking FakeMerchant proceed button");
    await UiHelper.Click(proceedButton);
}
```

---

## NFakeMerchant Structure

### Key Components

| Component | Type | Purpose |
|-----------|------|---------|
| `MerchantButton` | `NMerchantButton` | Opens the shop inventory when clicked |
| `_proceedButton` | `NProceedButton` | Leave the event, opens map |
| `_inventory` | `NMerchantInventory` | Shop interface with items for sale |
| `_characterContainer` | `Control` | Displays player characters |

### Flow

```
Player enters FakeMerchant event
  → NFakeMerchant._Ready() initializes components
    → MerchantButton created and connected
    → ProceedButton created (initially enabled, not pulsing)
    → NMerchantInventory initialized (closed initially)
  → Player clicks MerchantButton
    → OnMerchantOpened() → OpenInventory()
      → _inventory.Open() displays shop
      → ProceedButton disabled while shop is open
  → Player closes shop (back button)
    → InventoryClosed signal fires
    → ProceedButton enabled and pulsing
  → Player clicks ProceedButton
    → HideScreen() → NMapScreen.Instance.Open()
```

---

## Implementation Plan

### Step 1: Extend EventStateDto for Custom Events

**Goal**: Add fields to support custom event state extraction.

**Modified Files**:
- `STS2.Cli.Mod/Models/Dto/EventStateDto.cs`

**New Properties**:

```csharp
/// <summary>
///     Custom event sub-type (e.g., "FakeMerchant").
///     Only set when LayoutType is "Custom".
/// </summary>
public string? CustomEventType { get; set; }

/// <summary>
///     For FakeMerchant: whether the proceed button is available.
/// </summary>
public bool? CanProceed { get; set; }

/// <summary>
///     For FakeMerchant: whether the shop is available.
/// </summary>
public bool? HasShop { get; set; }

/// <summary>
///     For FakeMerchant: whether the shop inventory is currently open.
/// </summary>
public bool? IsShopOpen { get; set; }
```

**Deliverables**:
- [ ] `EventStateDto` includes custom event fields
- [ ] JSON output includes `custom_event_type`, `can_proceed`, `has_shop`, `is_shop_open`

---

### Step 2: Detect Custom Events in EventStateBuilder

**Goal**: Detect `NFakeMerchant` and extract its state.

**Modified Files**:
- `STS2.Cli.Mod/State/Builders/EventStateBuilder.cs`

**Implementation**:

```csharp
private static void ExtractCustomEventInfo(NEventRoom eventRoom, EventStateDto result)
{
    // Try to find NFakeMerchant node
    // Note: Access path may vary, need to investigate exact node structure
    var fakeMerchant = FindFakeMerchant(eventRoom);
    if (fakeMerchant != null)
    {
        result.CustomEventType = "FakeMerchant";
        
        // Check proceed button
        var proceedButton = fakeMerchant.GetNodeOrNull<NProceedButton>("%ProceedButton");
        if (proceedButton != null)
        {
            result.CanProceed = proceedButton.IsEnabled && proceedButton.Visible;
        }
        
        // Check shop inventory
        var inventory = fakeMerchant.GetNodeOrNull<NMerchantInventory>("%Inventory");
        if (inventory != null)
        {
            result.HasShop = true;
            result.IsShopOpen = inventory.IsOpen;
        }
        
        Logger.Info($"Detected FakeMerchant: can_proceed={result.CanProceed}, is_shop_open={result.IsShopOpen}");
    }
}

private static NFakeMerchant? FindFakeMerchant(NEventRoom eventRoom)
{
    // Try multiple approaches
    // Approach 1: Direct child
    var fakeMerchant = eventRoom.GetNodeOrNull<NFakeMerchant>("CustomEventNode");
    if (fakeMerchant != null) return fakeMerchant;
    
    // Approach 2: Find in children recursively
    // Use UiHelper pattern or manual traversal
    
    return null;
}
```

**Notes**:
- Need to verify the exact node path for `NFakeMerchant`
- May need to add reference to `MegaCrit.Sts2.Core.Nodes.Events.Custom` namespace
- Use reflection if necessary to access internal fields

**Deliverables**:
- [ ] `NFakeMerchant` detection implemented
- [ ] Proceed button state extraction
- [ ] Shop state extraction
- [ ] Graceful fallback when custom event type unknown

---

### Step 3: Create EventProceedHandler

**Goal**: New handler for clicking custom event proceed buttons.

**New Files**:
- `STS2.Cli.Mod/Actions/EventProceedHandler.cs`

**CLI Syntax**:
```bash
sts2 event_proceed
# Clicks the proceed button on custom events like FakeMerchant
```

**Request**:
```json
{"cmd": "event_proceed"}
```

**Handler Logic**:

1. **Guard: in EVENT screen** - Verify `NEventRoom.Instance` is valid
2. **Guard: is custom event** - Check `LayoutType == "Custom"`
3. **Guard: is FakeMerchant** - Find `NFakeMerchant` node
4. **Guard: proceed available** - Verify proceed button exists and is enabled
5. **ForceClick proceed button** - Trigger `NProceedButton.OnRelease()`
6. **Post-click polling** - Wait until map opens or event room closes

```csharp
public static class EventProceedHandler
{
    private static readonly ModLogger Logger = new("EventProceedHandler");
    
    public static object? Execute()
    {
        return MainThreadExecutor.RunOnMainThread(() =>
        {
            var eventRoom = NEventRoom.Instance;
            if (eventRoom == null || !eventRoom.IsInsideTree())
            {
                return new { ok = false, error = "NOT_IN_EVENT", message = "Not currently in an event" };
            }
            
            var eventModel = GetEventModel(eventRoom);
            if (eventModel?.LayoutType != EventLayoutType.Custom)
            {
                return new { ok = false, error = "NOT_CUSTOM_EVENT", message = "Current event is not a custom layout event" };
            }
            
            var fakeMerchant = FindFakeMerchant(eventRoom);
            if (fakeMerchant == null)
            {
                return new { ok = false, error = "NOT_FAKE_MERCHANT", message = "Custom event is not FakeMerchant (unsupported)" };
            }
            
            var proceedButton = fakeMerchant.GetNodeOrNull<NProceedButton>("%ProceedButton");
            if (proceedButton == null)
            {
                return new { ok = false, error = "PROCEED_BUTTON_NOT_FOUND", message = "Proceed button not found" };
            }
            
            if (!proceedButton.IsEnabled || !proceedButton.Visible)
            {
                return new { ok = false, error = "PROCEED_NOT_AVAILABLE", message = "Proceed button is not available" };
            }
            
            // Click the button
            Logger.Info("Clicking FakeMerchant proceed button");
            proceedButton.ForceClick();
            
            // Polling loop for state change
            // TODO: Implement polling similar to ChooseEventHandler
            
            return new { ok = true, data = new { proceeded = true } };
        });
    }
}
```

**Error Codes**:

| Error Code | Exit Code | Description |
|------------|-----------|-------------|
| `NOT_IN_EVENT` | 2 | Not currently in an event room |
| `NOT_CUSTOM_EVENT` | 2 | Event is not a custom layout |
| `NOT_FAKE_MERCHANT` | 2 | Custom event is not FakeMerchant (unsupported) |
| `PROCEED_BUTTON_NOT_FOUND` | 2 | Cannot find proceed button node |
| `PROCEED_NOT_AVAILABLE` | 2 | Proceed button is disabled or invisible |

**Deliverables**:
- [ ] `EventProceedHandler.cs` implemented with guard clauses
- [ ] Polling logic for post-click state verification
- [ ] Proper error handling and response formatting

---

### Step 4: Add CLI Command and Server Route

**Modified Files**:
- `STS2.Cli.Cmd/Program.cs` - Add `event_proceed` subcommand
- `STS2.Cli.Mod/Server/PipeServer.cs` - Route `event_proceed` command
- `STS2.Cli.Cmd/Services/CommandRunner.cs` - Add error code mapping

**CLI Implementation**:

```csharp
// In Program.cs
var eventProceedCmd = new Command("event_proceed", "Click proceed button on custom events (e.g., FakeMerchant)");
eventProceedCmd.SetHandler(async () =>
{
    await CommandRunner.RunAsync("event_proceed", []);
});
```

**Server Route**:

```csharp
// In PipeServer.cs "event_proceed" route
"event_proceed" => EventProceedHandler.Execute(),
```

**Exit Code Mapping**:

```csharp
"NOT_IN_EVENT" => 2,
"NOT_CUSTOM_EVENT" => 2,
"NOT_FAKE_MERCHANT" => 2,
"PROCEED_BUTTON_NOT_FOUND" => 2,
"PROCEED_NOT_AVAILABLE" => 2,
```

**Deliverables**:
- [ ] CLI `sts2 event_proceed` command available
- [ ] Server routes command to handler
- [ ] Exit codes properly mapped

---

### Step 5: Build and Test

**Build Commands**:
```bash
# Clean
rm -rf STS2.Cli.Mod/bin STS2.Cli.Mod/obj STS2.Cli.Cmd/bin STS2.Cli.Cmd/obj

# Build Mod (game must NOT be running)
dotnet build STS2.Cli.Mod/STS2.Cli.Mod.csproj -c Release

# Build CLI for Windows x64
dotnet build STS2.Cli.Cmd/STS2.Cli.Cmd.csproj -c Release -r win-x64
```

**Test Scenarios**:

```bash
# 1. Enter FakeMerchant event in-game, then:
sts2.exe state
# Expected: screen "EVENT", layout_type "Custom", custom_event_type "FakeMerchant"
#           can_proceed=true, has_shop=true, is_shop_open=false

# 2. Click proceed to leave:
sts2.exe event_proceed
# Expected: ok true, proceeded true, map opens

# 3. Error: wrong event type
sts2.exe event_proceed  # (when on regular event)
# Expected: ok false, error "NOT_CUSTOM_EVENT"

# 4. Error: not in event
sts2.exe event_proceed  # (when on map)
# Expected: ok false, error "NOT_IN_EVENT"
```

**Deliverables**:
- [ ] Both projects build without errors
- [ ] `sts2 state` returns FakeMerchant state correctly
- [ ] `sts2 event_proceed` works on FakeMerchant events
- [ ] Proper error messages for unsupported scenarios

---

### Step 6: Documentation Updates

**Modified Files**:
- `AGENTS.md` - Add `event_proceed` to testing commands
- `plan/event-development-plan.md` - Mark custom events as completed

**AGENTS.md Entry**:
```
11. `sts2.exe event_proceed` - confirms custom event proceed button (FakeMerchant)
```

**Deliverables**:
- [ ] `AGENTS.md` updated with new command
- [ ] Event development plan marked complete

---

## Future Extensions

### Other Custom Events

If additional custom events are added to the game:

1. **Detection**: Extend `ExtractCustomEventInfo()` to detect new types
2. **State**: Add event-specific fields to `EventStateDto` or create derived DTOs
3. **Actions**: Implement handlers for event-specific actions
4. **Fallback**: Always provide graceful degradation for unknown custom events

### Shop Interaction

Future enhancement could add shop interaction commands:
```bash
sts2 shop_buy --slot 0      # Buy item at slot index
sts2 shop_remove_card       # Remove a card (if service available)
sts2 shop_close             # Close shop (returns to event)
```

This is currently **out of scope** as the primary use case is leaving the event via proceed.

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| NFakeMerchant node path uncertainty | High | Test multiple path patterns, use FindFirst helper |
| Future game updates change structure | Medium | Use reflection as fallback, log warnings |
| Shop open detection timing | Low | State extraction may miss rapid transitions, polling covers actions |
| Other custom events unsupported | Low | Clear error message, extendable architecture |

---

## Error Codes Summary

| Error Code | Exit Code | Commands |
|------------|-----------|----------|
| `NOT_IN_EVENT` | 2 | event_proceed |
| `NOT_CUSTOM_EVENT` | 2 | event_proceed |
| `NOT_FAKE_MERCHANT` | 2 | event_proceed |
| `PROCEED_BUTTON_NOT_FOUND` | 2 | event_proceed |
| `PROCEED_NOT_AVAILABLE` | 2 | event_proceed |

---

## CLI Command Summary (After Completion)

| Command | Syntax | Description |
|---------|--------|-------------|
| `sts2 ping` | `sts2 ping` | Test connection |
| `sts2 state` | `sts2 state` | Get game state |
| `sts2 play_card` | `sts2 play_card <index> [--target <id>]` | Play a card |
| `sts2 end_turn` | `sts2 end_turn` | End combat turn |
| `sts2 use_potion` | `sts2 use_potion <slot> [--target <id>]` | Use a potion |
| `sts2 claim_reward` | `sts2 claim_reward <index>` | Claim reward |
| `sts2 choose_card` | `sts2 choose_card <reward_index> <card_index>` | Pick card reward |
| `sts2 skip_card` | `sts2 skip_card <reward_index>` | Skip card reward |
| `sts2 proceed` | `sts2 proceed` | Leave reward screen |
| `sts2 choose_event` | `sts2 choose_event <index>` | Choose event option |
| `sts2 advance_dialogue` | `sts2 advance_dialogue [--auto]` | Advance Ancient event dialogue |
| `sts2 event_proceed` | `sts2 event_proceed` | **NEW** - Click proceed on custom events |

---

*Created: 2026-03-23*
*Version: v0.5.0*
