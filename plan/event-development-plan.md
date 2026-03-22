# Event System - Development Plan

This document outlines the development plan for map event interaction features. When the player enters an event room on the map, the game presents a multi-page dialogue with options. We need to expose this via the CLI so that an AI agent can read event state and choose options.

---

## Game Event System Summary

### Flow

```
Player enters event room on map
  → EventRoom.Enter()
    → EventSynchronizer.BeginEvent()
      → NEventRoom created (scene instantiation)
        → SetupLayout() → NEventLayout created with description + options
          → NEventOptionButton instances in %OptionsContainer
  → Player clicks option
    → NEventOptionButton.OnRelease()
      → NEventRoom.OptionButtonClicked(option, index)
        → If IsProceed: option.Chosen() → NEventRoom.Proceed() → opens map
        → If not IsProceed: EventSynchronizer.ChooseLocalOption(index)
          → Event logic runs → EventModel.StateChanged fires
            → RefreshEventState() → new description + new options
  → Loop until EventModel.IsFinished
    → Final "PROCEED" option shown → clicking opens map
```

### Event Layout Types (EventLayoutType Enum)

| Type | Description | Notes |
|------|-------------|-------|
| `Default` | Standard event with options | Most common |
| `Combat` | Event that can trigger mid-event combat | After combat, event may resume with new options |
| `Ancient` | Ancient event with dialogue phase first | Click `%DialogueHitbox` repeatedly before options appear |
| `Custom` | Special event with custom layout | e.g., `NFakeMerchant` |

### Key Game APIs

| Class | Access | Purpose |
|-------|--------|---------|
| `NEventRoom.Instance` | `NRun.Instance?.EventRoom` | Singleton for the current event room node |
| `NEventRoom.Layout` | Property (NEventLayout?) | The UI layout containing description and options |
| `NEventLayout._event` | Protected field (EventModel) | The event model with state and options |
| `NEventLayout.OptionButtons` | Property (IEnumerable\<NEventOptionButton\>) | All option buttons currently displayed |
| `NEventLayout._description` | Field (MegaRichTextLabel via `%EventDescription`) | Event description rich text |
| `EventModel.Id` | Property (ModelId) | Event identifier |
| `EventModel.Title` | Property (LocString) | Localized event title |
| `EventModel.Description` | Property (LocString?) | Localized description (can change per page) |
| `EventModel.CurrentOptions` | Property (IReadOnlyList\<EventOption\>) | Currently available options |
| `EventModel.IsFinished` | Property (bool) | Whether the event has concluded |
| `EventModel.LayoutType` | Property (EventLayoutType) | Layout type enum |
| `EventOption.Title` | Property (LocString) | Option display text |
| `EventOption.Description` | Property (LocString) | Option description/tooltip |
| `EventOption.TextKey` | Property (string) | Raw text key for the option |
| `EventOption.IsLocked` | Property (bool) | True when `OnChosen == null` — cannot be selected |
| `EventOption.IsProceed` | Property (bool) | True for the final "proceed" option |
| `EventOption.WasChosen` | Property (bool) | True if already selected |
| `EventOption.Relic` | Property (RelicModel?) | Relic associated with this option (if any) |
| `EventOption.WillKillPlayer` | Property (Func\<Player, bool\>?) | Guard against fatal options (multiplayer) |
| `NEventOptionButton` | Extends `NButton` (has ForceClick) | UI button; `OnRelease()` → `OptionButtonClicked()` |
| `NEventRoom.OptionButtonClicked()` | Method | Dispatches option choice (proceed vs normal) |

### Multi-Page Event Flow

Events are inherently multi-page. After choosing a non-proceed option:
1. `EventSynchronizer.ChooseLocalOption(index)` executes the event logic
2. `EventModel.StateChanged` fires → `NEventRoom.RefreshEventState()` runs
3. New description and new options are displayed
4. The loop continues until `EventModel.IsFinished`
5. When finished, a single "PROCEED" option (with `IsProceed = true`) replaces all options
6. Clicking proceed calls `NEventRoom.Proceed()` → opens the map

### AutoSlayer Reference Pattern (EventRoomHandler.cs)

```
1. Wait for NEventRoom at node path /root/Game/RootSceneContainer/Run/RoomContainer/EventRoom
2. Check for NAncientEventLayout → click %DialogueHitbox repeatedly (DEFERRED)
3. Check for NFakeMerchant custom event → click proceed button
4. Find unlocked NEventOptionButtons via UiHelper.FindAll<NEventOptionButton>(eventRoom)
5. Filter out options where WillKillPlayer returns true
6. Pick random option → UiHelper.Click(choice) (ForceClick + 100ms delay)
7. If IsProceed: wait until event room leaves tree or map opens
8. If not proceed: wait until new options appear, overlay opens, or map opens
9. Loop up to 50 iterations for multi-page events
```

---

## Implementation Plan

### Step 1: Screen Detection — Detect Event Room

**Goal**: Extend `GameStateExtractor.DetectScreen()` to recognize when the player is in an event room.

**Modified Files**:
- `STS2.Cli.Mod/State/GameStateExtractor.cs` — Add `"EVENT"` detection

**Implementation**:

The event check must go after COMBAT (combat events have `CombatManager.Instance.IsInProgress` true) and after MAP (proceeding from event opens map but `NEventRoom` may linger):

```csharp
// Current detection priority:
// 1. MENU — !RunManager.Instance.IsInProgress
// 2. COMBAT — CombatManager.Instance.IsInProgress
// 3. MAP — NMapScreen.Instance.IsOpen
// ... overlay checks (CARD_REWARD, REWARD) ...
// 8. UNKNOWN

// Add EVENT check after MAP but before overlay stack checks:
// 4. EVENT — NEventRoom.Instance is not null and inside tree
```

**Detection Logic**:
```csharp
// After MAP check, before overlay stack
var eventRoom = NRun.Instance?.EventRoom;
if (eventRoom is { } && eventRoom.IsInsideTree())
    return "EVENT";
```

**Why after MAP**: When proceeding from an event, the map opens but `NEventRoom.Instance` may still be valid momentarily. Checking MAP first avoids this stale reference (same pattern as `NRewardsScreen` stale overlay).

**Why before overlay stack**: Event rooms don't use the overlay stack — the event UI is part of the room node itself, not pushed as an overlay.

**Deliverables**:
- [x] `GameStateExtractor.DetectScreen()` returns `"EVENT"` when in event room
- [x] `sts2 state` shows `"screen": "EVENT"` when at an event

**Status**: ✅ COMPLETED - Event screen detection implemented in `GameStateExtractor.DetectScreen()`

---

### Step 2: Event State Extraction — Read Event State & Options

**Goal**: When screen is `EVENT`, extract the event model state and current options, return them in `sts2 state`.

**New Files**:
- `STS2.Cli.Mod/Models/Dto/EventStateDto.cs` — Root event state DTO
- `STS2.Cli.Mod/Models/Dto/EventOptionDto.cs` — Individual event option DTO
- `STS2.Cli.Mod/State/Builders/EventStateBuilder.cs` — Builds event DTOs from game objects

**Modified Files**:
- `STS2.Cli.Mod/Models/Dto/GameStateDto.cs` — Add `Event` property
- `STS2.Cli.Mod/State/GameStateExtractor.cs` — Extract event state when screen is `"EVENT"`

**DTO Design**:

```csharp
// EventStateDto - top-level event container
public class EventStateDto
{
    public required string EventId { get; set; }        // ModelId as string
    public required string Title { get; set; }          // Localized event title
    public string? Description { get; set; }            // Localized event description (current page)
    public required string LayoutType { get; set; }     // "Default", "Combat", "Ancient", "Custom"
    public bool IsFinished { get; set; }                // Whether event has concluded
    public List<EventOptionDto> Options { get; set; } = [];  // Current available options
}

// EventOptionDto - individual option
public class EventOptionDto
{
    public required int Index { get; set; }             // 0-based index for choose_event
    public required string Title { get; set; }          // Localized option text
    public string? Description { get; set; }            // Localized option description/tooltip
    public string? TextKey { get; set; }                // Raw text key
    public bool IsLocked { get; set; }                  // Cannot be selected (OnChosen == null)
    public bool IsProceed { get; set; }                 // Final "proceed to map" option
    public bool WasChosen { get; set; }                 // Already selected in a previous page

    // Relic info (some options show a relic)
    public string? RelicId { get; set; }
    public string? RelicName { get; set; }
}
```

**Builder Logic** (`EventStateBuilder.Build()`):
1. Get `NEventRoom.Instance` → access the event model
2. Access `EventModel` via `NEventRoom.Instance.Layout._event` (protected field — may need reflection or alternative path like `NRun.Instance.EventRoom` internal field `_event`)
3. Extract `Id`, `Title`, `Description`, `LayoutType`, `IsFinished`
4. Iterate `EventModel.CurrentOptions` to build `EventOptionDto` list
5. For each option: extract `Title`, `Description`, `TextKey`, `IsLocked`, `IsProceed`, `WasChosen`
6. If `option.Relic` is not null: extract relic `Id` and `Title`
7. Per-field try-catch wrapping (matching existing builder pattern)

**Note on accessing EventModel**: Both `NEventRoom._event` and `NEventLayout._event` are non-public fields. We may need to:
- Check if there's a public accessor on `NEventRoom` or `NEventLayout`
- Use reflection as fallback
- Or access via `EventRoom` (the room logic class) through `RunManager`

This needs investigation during implementation. The `EventModel.CurrentOptions` property is public once we have the model reference.

**JSON Output Example** (`sts2 state` on event screen):
```json
{
  "ok": true,
  "data": {
    "screen": "EVENT",
    "timestamp": 1711123456789,
    "event": {
      "event_id": "BIG_FISH",
      "title": "Big Fish",
      "description": "You find a large fish flopping on the riverbank...",
      "layout_type": "Default",
      "is_finished": false,
      "options": [
        {
          "index": 0,
          "title": "[Eat] Heal 5 HP.",
          "description": "Heal 5 HP.",
          "text_key": "EAT",
          "is_locked": false,
          "is_proceed": false,
          "was_chosen": false
        },
        {
          "index": 1,
          "title": "[Feed] Gain Max HP.",
          "description": "Max HP +5.",
          "text_key": "FEED",
          "is_locked": false,
          "is_proceed": false,
          "was_chosen": false
        },
        {
          "index": 2,
          "title": "[Ignore] Nothing happens.",
          "description": null,
          "text_key": "IGNORE",
          "is_locked": false,
          "is_proceed": false,
          "was_chosen": false
        }
      ]
    }
  }
}
```

**Deliverables**:
- [x] `EventStateDto` and `EventOptionDto` DTO classes
- [x] `EventStateBuilder.Build()` extracts event state from `NEventRoom`
- [x] `GameStateDto.Event` property added
- [x] `GameStateExtractor` returns event state when screen is `"EVENT"`

**Status**: ✅ COMPLETED - Event state extraction implemented with full DTO support

---

### Step 3: Choose Event Option — ForceClick Handler

**Goal**: New CLI command to choose an event option by index, returning updated event state after the option resolves.

**New Files**:
- `STS2.Cli.Mod/Actions/ChooseEventHandler.cs` — Handler for choose_event

**Modified Files**:
- `STS2.Cli.Mod/Server/PipeServer.cs` — Route `choose_event` command
- `STS2.Cli.Cmd/Program.cs` — Add `choose_event` CLI subcommand

**CLI Syntax**:
```bash
sts2 choose_event <index>
# index: 0-based position in the options list
```

**Request**:
```json
{"cmd": "choose_event", "args": [0]}
```

**Handler Logic** (`ChooseEventHandler.ExecuteAsync(int optionIndex)`):

1. **Guard: screen is EVENT** — Check `NEventRoom.Instance` is valid and inside tree
2. **Guard: layout exists** — `NEventRoom.Instance.Layout` is not null
3. **Guard: option index valid** — Index within `CurrentOptions` range
4. **Guard: option not locked** — `!option.IsLocked`
5. **Find NEventOptionButton** — Get button nodes from `NEventRoom.Instance.Layout.OptionButtons`, match by index
6. **ForceClick** the button — This triggers `OnRelease()` → `OptionButtonClicked()` through the normal game UI flow
7. **Post-click polling** (different behavior based on option type):

**If `IsProceed` (final option)**:
- Poll until `NMapScreen.Instance.IsOpen` becomes true, or `NEventRoom.Instance` leaves tree
- Return success response:
  ```json
  {
    "ok": true,
    "data": {
      "option_index": 0,
      "is_proceed": true,
      "proceeded": true
    }
  }
  ```

**If NOT `IsProceed` (regular option — leads to new page)**:
- Poll until `EventModel.CurrentOptions` changes (new options appear), or `EventModel.IsFinished` becomes true, or an overlay opens (combat event triggers combat), or timeout
- Build and return the **updated event state**:
  ```json
  {
    "ok": true,
    "data": {
      "option_index": 0,
      "is_proceed": false,
      "event": {
        "event_id": "BIG_FISH",
        "title": "Big Fish",
        "description": "You feel refreshed after the meal.",
        "layout_type": "Default",
        "is_finished": true,
        "options": [
          {
            "index": 0,
            "title": "Proceed",
            "is_locked": false,
            "is_proceed": true,
            "was_chosen": false
          }
        ]
      }
    }
  }
  ```

**Polling Strategy**:
- Capture a snapshot of `CurrentOptions` before ForceClick (count + first option title)
- Poll every 100ms up to 5 seconds
- Detect change when: options count differs, or first option title differs, or `IsFinished` changes, or `NMapScreen.Instance.IsOpen`, or a new overlay appears on `NOverlayStack`
- If combat event triggers mid-event combat: the screen will change to COMBAT. Poll should detect this and return early.

**Threading Model**: `RunOnMainThreadAsync` — ForceClick must run on main thread, polling needs multiple frames.

**Error Cases**:

| Error Code | Exit Code | Condition |
|------------|-----------|-----------|
| `NOT_IN_EVENT` | 2 (invalid state) | Screen is not EVENT |
| `NO_EVENT_LAYOUT` | 2 (invalid state) | Event layout not found |
| `INVALID_OPTION_INDEX` | 3 (invalid param) | Index out of range |
| `OPTION_LOCKED` | 2 (invalid state) | Option is locked (cannot be chosen) |
| `OPTION_BUTTON_NOT_FOUND` | 2 (invalid state) | Button node not found at index |
| `EVENT_TIMEOUT` | 4 (timeout) | Post-click polling timed out |

**Deliverables**:
- [x] `ChooseEventHandler.ExecuteAsync(int optionIndex)` with guard clauses + ForceClick + polling
- [x] `PipeServer` routes `choose_event` to handler
- [x] CLI `sts2 choose_event <index>` command (indexed pattern like `claim_reward`)
- [x] Error codes added to `CommandRunner.MapErrorToExitCode()`

**Status**: ✅ COMPLETED - Event option selection implemented with polling and updated state return

---

### Step 4: Build & Test

**Goal**: Verify the event system works end-to-end.

**Build**:
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
# 1. Navigate to an event room in-game, then:
sts2.exe state
# Expected: screen "EVENT", event_id, title, description, options list

# 2. Choose the first option:
sts2.exe choose_event 0
# Expected: ok true, updated event state with new description + options (or is_finished)

# 3. If event shows "Proceed" option (is_finished):
sts2.exe choose_event 0
# Expected: ok true, is_proceed true, proceeded true, map opens

# 4. Error: wrong screen
sts2.exe choose_event 0  # (when NOT on event screen)
# Expected: ok false, error "NOT_IN_EVENT"

# 5. Error: invalid index
sts2.exe choose_event 99  # (when on event screen)
# Expected: ok false, error "INVALID_OPTION_INDEX"

# 6. Error: locked option
sts2.exe choose_event 1  # (option at index 1 is locked)
# Expected: ok false, error "OPTION_LOCKED"
```

**Deliverables**:
- [ ] Both projects build without errors
- [ ] `sts2 state` returns event state when on event screen
- [ ] `sts2 choose_event <index>` selects options and returns updated state
- [ ] Proceed option transitions to map correctly

---

### Step 5: Documentation — Update AGENTS.md

**Goal**: Add `choose_event` to the testing commands in `AGENTS.md`.

**Modified Files**:
- `AGENTS.md` — Add `choose_event` command to the testing section

**New entry**:
```
10. `sts2.exe choose_event <index>` - confirms event option selection with updated event state
```

**Deliverables**:
- [ ] `AGENTS.md` updated with `choose_event` command

---

## Implementation Order & Dependencies

```
Step 1 (Screen Detection) ──→ Step 2 (State Extraction) ──→ Step 3 (Choose Event Handler)
                                                                 │
                                                                 ↓
                                                           Step 4 (Build & Test)
                                                                 │
                                                                 ↓
                                                           Step 5 (Documentation)
```

- **Step 1** is prerequisite for all — must detect EVENT screen before extracting state or acting.
- **Step 2** is prerequisite for Step 3 — handler reuses `EventStateBuilder` for returning updated state.
- **Steps 4–5** are sequential after implementation is done.

**Recommended order**: 1 → 2 → 3 → 4 → 5

---

## Files Summary

### New Files (4)

| File | Purpose |
|------|---------|
| `STS2.Cli.Mod/Models/Dto/EventStateDto.cs` | Event state DTO |
| `STS2.Cli.Mod/Models/Dto/EventOptionDto.cs` | Event option DTO |
| `STS2.Cli.Mod/State/Builders/EventStateBuilder.cs` | Builds EventStateDto from game state |
| `STS2.Cli.Mod/Actions/ChooseEventHandler.cs` | ForceClick handler + returns updated state |

### Modified Files (5)

| File | Change |
|------|--------|
| `STS2.Cli.Mod/State/GameStateExtractor.cs` | Add `"EVENT"` to DetectScreen(); add event state extraction call |
| `STS2.Cli.Mod/Models/Dto/GameStateDto.cs` | Add `Event` property |
| `STS2.Cli.Mod/Server/PipeServer.cs` | Add `"choose_event"` route |
| `STS2.Cli.Cmd/Program.cs` | Add `choose_event` CLI subcommand |
| `AGENTS.md` | Add `choose_event` to testing section |

---

## Deferred Work

### Ancient Event Dialogue (Follow-Up)

Ancient events have a dialogue phase before options appear. The player must click a `%DialogueHitbox` node repeatedly to advance through dialogue pages until options are shown.

**What's needed**:
- Detect `NAncientEventLayout` (subclass of `NEventLayout`)
- Find `%DialogueHitbox` node (an `NAncientDialogueHitbox` or similar clickable control)
- ForceClick repeatedly until dialogue ends and options appear
- May need a separate `advance_dialogue` command or auto-detection logic in `choose_event`

**Files to investigate when implementing**:
- `~/STS2-Reverse-Engineering/decompiled/sts2/MegaCrit.Sts2.Core.Nodes.Events/NAncientEventLayout.cs`
- `~/STS2-Reverse-Engineering/decompiled/sts2/MegaCrit.Sts2.Core.Nodes.Events/NAncientDialogueHitbox.cs`

### Combat Events (Follow-Up)

Some events trigger mid-event combat. After combat ends, the event may resume with new options. The current implementation will handle this partially — when combat triggers, the screen changes to `"COMBAT"`, and after combat ends, the screen should return to `"EVENT"` with new options. However, the transition may need specific handling.

### Custom Events (Follow-Up)

Custom layout events like `NFakeMerchant` have non-standard UIs. The AutoSlayer handles these with special-case code. We may need similar special-casing.

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| `EventModel` not accessible (private/protected fields) | High | Try reflection. Alternative: access via `EventRoom` from `RunManager`. Check for public accessors during implementation. |
| `NEventOptionButton` list doesn't match `CurrentOptions` indices | Medium | Verify button `Index` property matches option index. AutoSlayer uses `FindAll<NEventOptionButton>` which preserves order. |
| Event state changes between snapshot and ForceClick | Low | Polling will detect stale state. Guard clauses re-validate before click. |
| `ForceClick()` on option button doesn't trigger expected flow | Medium | Fallback: call `NEventRoom.OptionButtonClicked()` directly. AutoSlayer confirms ForceClick works. |
| Multi-page events with > 50 steps | Low | The handler returns after each option choice. The AI caller loops externally. No single-call limit. |
| Combat event transitions break state detection | Medium | Screen detection handles COMBAT priority. After combat, EVENT should be re-detected if event resumes. |

---

## New Error Codes Summary

| Error Code | Exit Code | Commands |
|------------|-----------|----------|
| `NOT_IN_EVENT` | 2 (invalid state) | choose_event |
| `NO_EVENT_LAYOUT` | 2 (invalid state) | choose_event |
| `INVALID_OPTION_INDEX` | 3 (invalid param) | choose_event |
| `OPTION_LOCKED` | 2 (invalid state) | choose_event |
| `OPTION_BUTTON_NOT_FOUND` | 2 (invalid state) | choose_event |
| `EVENT_TIMEOUT` | 4 (timeout) | choose_event |

---

## CLI Command Summary (After Completion)

| Command | Syntax | Description |
|---------|--------|-------------|
| `sts2 ping` | `sts2 ping` | Test connection |
| `sts2 state` | `sts2 state` | Get game state (combat, rewards, or event) |
| `sts2 play_card` | `sts2 play_card <index> [--target <id>]` | Play a card in combat |
| `sts2 end_turn` | `sts2 end_turn` | End combat turn |
| `sts2 use_potion` | `sts2 use_potion <slot> [--target <id>]` | Use a potion in combat |
| `sts2 claim_reward` | `sts2 claim_reward <index>` | Claim gold/potion/relic reward |
| `sts2 choose_card` | `sts2 choose_card <reward_index> <card_index>` | Pick a card from card reward |
| `sts2 skip_card` | `sts2 skip_card <reward_index>` | Skip a card reward |
| `sts2 proceed` | `sts2 proceed` | Leave reward screen |
| `sts2 choose_event` | `sts2 choose_event <index>` | **NEW** — Choose an event option |

---

*Last Updated: 2026-03-22*
*Version: v0.4.0*
