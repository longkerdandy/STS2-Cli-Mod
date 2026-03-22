# Ancient Event Support - Development Plan

This document outlines the development plan for supporting Ancient events, which have a dialogue phase before options appear. The player must click through dialogue lines before being able to select event options.

---

## Ancient Event System Summary

### What Makes Ancient Events Different

Unlike regular events that show options immediately, Ancient events have:

1. **Dialogue Phase**: A series of dialogue lines that must be clicked through
2. **Sequential Progression**: Each click advances to the next dialogue line
3. **Final Options**: After the last dialogue line, event options become available
4. **Special UI**: Uses `NAncientEventLayout` instead of standard `NEventLayout`

### Flow

```
Player enters Ancient event room
  → NAncientEventLayout created
    → SetDialogue() initializes dialogue lines
    → OnSetupComplete() starts animation
      → SetDialogueLineAndAnimate(0) shows first line
        → _dialogueHitbox visible and enabled (clickable)
        → Options NOT visible yet (IsDialogueOnLastLine = false)
  → Player clicks _dialogueHitbox
    → OnDialogueHitboxClicked() → SetDialogueLineAndAnimate(_currentDialogueLine + 1)
      → Dialogue advances to next line
      → Repeat until IsDialogueOnLastLine = true
        → _dialogueHitbox hidden and disabled
        → Options become visible and clickable
        → choose_event can now be used
```

### Key Game APIs

| Class/Property | Type | Purpose |
|----------------|------|---------|
| `NAncientEventLayout` | Class | Extends NEventLayout, handles dialogue |
| `_dialogueHitbox` | NAncientDialogueHitbox | Clickable area to advance dialogue (extends NButton) |
| `IsDialogueOnLastLine` | Property | True when dialogue finished, options available |
| `DefaultFocusedControl` | Property | Returns null during dialogue, first option when finished |
| `SetDialogueLineAndAnimate(int)` | Method | Advances to specific dialogue line |
| `OnDialogueHitboxClicked()` | Method | Handler for hitbox click |

### Dialogue Detection

We can detect if we're in dialogue phase by checking:
1. `layout is NAncientEventLayout` - confirms it's an Ancient event
2. `!layout.IsDialogueOnLastLine` - dialogue still in progress
3. `layout.DefaultFocusedControl == null` - no option focused yet

---

## Implementation Plan

### Option A: Add advance_dialogue Command (Recommended)

**Goal**: Add a new CLI command to manually advance dialogue.

**New CLI Command**:
```bash
sts2 advance_dialogue [--auto]
# --auto: automatically advance all dialogue lines until options appear
```

**Pros**:
- Explicit control over dialogue progression
- Can support manual step-through or auto-advance
- Easy to implement and understand

**Cons**:
- Requires two commands: `advance_dialogue` → wait → `choose_event`

### Option B: Auto-detect in choose_event (Alternative)

**Goal**: Make `choose_event` automatically handle dialogue if needed.

**Behavior**:
```bash
sts2 choose_event 0
# If in dialogue phase: advance all lines first, then select option 0
# If in options phase: directly select option 0
```

**Pros**:
- Single command interface
- AI doesn't need to know about dialogue phase

**Cons**:
- More complex implementation
- Less explicit state control
- Debugging harder if something goes wrong

---

## Recommended Implementation (Option A)

### Step 1: Extend Event State to Include Dialogue Info

**Goal**: Let AI know if dialogue needs to be advanced.

**Modified Files**:
- `STS2.Cli.Mod/Models/Dto/EventStateDto.cs` - Add `is_in_dialogue` flag
- `STS2.Cli.Mod/State/Builders/EventStateBuilder.cs` - Detect Ancient layout and dialogue phase

**Changes**:
```csharp
// EventStateDto
public bool IsInDialogue { get; set; }  // True for Ancient events in dialogue phase
public int? CurrentDialogueLine { get; set; }  // Current line index (optional)
public int? TotalDialogueLines { get; set; }  // Total lines (optional)
```

**Logic**:
1. Check if `NEventRoom.Instance.Layout is NAncientEventLayout`
2. If yes, cast and check `IsDialogueOnLastLine`
3. If not on last line, set `IsInDialogue = true`
4. Optionally extract `_currentDialogueLine` and `_dialogue.Count`

**Status**: ✅ COMPLETED

### Step 2: Create AdvanceDialogueHandler

**Goal**: Handle dialogue advancement via ForceClick on dialogue hitbox.

**New Files**:
- `STS2.Cli.Mod/Actions/AdvanceDialogueHandler.cs`

**Handler Logic** (`ExecuteAsync(bool auto = false)`):

1. **Guard: Check event room** - Same as ChooseEventHandler
2. **Guard: Check layout is Ancient** - `layout is NAncientEventLayout`
3. **Guard: Check in dialogue phase** - `!ancientLayout.IsDialogueOnLastLine`
4. **Find dialogue hitbox** - Access via reflection or property
5. **ForceClick hitbox** - `hitbox.ForceClick()`
6. **If auto mode**: Repeat steps 3-5 until `IsDialogueOnLastLine` or max iterations (e.g., 50)
7. **Return updated event state**

**Status**: ✅ COMPLETED

**Response**:
```json
// Single advance
{
  "ok": true,
  "data": {
    "advanced": true,
    "is_dialogue_finished": false,
    "current_line": 1,
    "total_lines": 3
  }
}

// Auto advance (finished)
{
  "ok": true,
  "data": {
    "advanced": true,
    "is_dialogue_finished": true,
    "lines_advanced": 3,
    "event_state": { ... }
  }
}
```

### Step 3: Add CLI Commands

**New CLI Commands**:

```bash
# Advance one dialogue line
sts2 advance_dialogue

# Auto-advance all dialogue lines
sts2 advance_dialogue --auto
```

**Modified Files**:
- `STS2.Cli.Cmd/Program.cs` - Add `advance_dialogue` subcommand
- `STS2.Cli.Cmd/Services/CommandRunner.cs` - Add error codes
- `STS2.Cli.Mod/Server/PipeServer.cs` - Route command

**Error Codes**:
| Error Code | Condition |
|------------|-----------|
| `NOT_ANCIENT_EVENT` | Event is not an Ancient event |
| `NOT_IN_DIALOGUE` | Already in options phase (dialogue finished) |
| `DIALOGUE_HITBOX_NOT_FOUND` | Cannot find dialogue hitbox node |

**Status**: ✅ COMPLETED

**Deliverables**:
- [x] `AGENTS.md` updated with testing instructions for `advance_dialogue`
- [x] `docs/cli-reference.md` updated with event state structure and command documentation

### Step 4: Update Documentation (COMPLETED)

**Modified Files**:
- `AGENTS.md` - Add testing instructions
- `docs/cli-reference.md` - Document new command
- `plan/event-development-plan.md` - Mark Ancient support as complete

---

## Implementation Order

```
Step 1 ──→ Step 2 ──→ Step 3 ──→ Step 4
(Extend   (Create    (Add CLI   (Update
 State)   Handler)    Command)   Docs)
```

**Recommended order**: 1 → 2 → 3 → 4

---

## Files Summary

### New Files (1)

| File | Purpose |
|------|---------|
| `STS2.Cli.Mod/Actions/AdvanceDialogueHandler.cs` | ForceClick dialogue hitbox + polling |

### Modified Files (5)

| File | Changes |
|------|---------|
| `STS2.Cli.Mod/Models/Dto/EventStateDto.cs` | Add IsInDialogue, CurrentDialogueLine, TotalDialogueLines |
| `STS2.Cli.Mod/State/Builders/EventStateBuilder.cs` | Detect Ancient layout and dialogue phase |
| `STS2.Cli.Mod/Server/PipeServer.cs` | Add advance_dialogue route |
| `STS2.Cli.Cmd/Program.cs` | Add advance_dialogue CLI command |
| `STS2.Cli.Cmd/Services/CommandRunner.cs` | Add NOT_ANCIENT_EVENT, NOT_IN_DIALOGUE, DIALOGUE_HITBOX_NOT_FOUND |
| `AGENTS.md` | Add testing instructions |
| `docs/cli-reference.md` | Document new command |

---

## New Error Codes Summary

| Error Code | Exit Code | Commands |
|------------|-----------|----------|
| `NOT_ANCIENT_EVENT` | 2 (invalid state) | advance_dialogue |
| `NOT_IN_DIALOGUE` | 2 (invalid state) | advance_dialogue |
| `DIALOGUE_HITBOX_NOT_FOUND` | 2 (invalid state) | advance_dialogue |

---

## CLI Command Summary (After Completion)

| Command | Syntax | Description |
|---------|--------|-------------|
| `sts2 advance_dialogue` | `sts2 advance_dialogue [--auto]` | Advance Ancient event dialogue |
| `sts2 choose_event` | `sts2 choose_event <index>` | Choose option (after dialogue) |

---

## AI Usage Pattern

```python
# Example AI logic for Ancient events
state = sts2_state()
if state["screen"] == "EVENT":
    if state["event"]["layout_type"] == "Ancient":
        if state["event"]["is_in_dialogue"]:
            # Need to advance dialogue first
            sts2_advance_dialogue(auto=True)
            # Now check state again
            state = sts2_state()
    
    # Now options should be available
    choose_event(0)  # Select first option
```

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| NAncientEventLayout private fields | Medium | Use reflection to access _dialogueHitbox, or find public accessor |
| Dialogue animation timing | Low | Use polling after ForceClick, similar to ChooseEventHandler |
| Mixed Ancient/Normal events | Low | Check layout type before attempting dialogue advancement |
| Auto-advance loop forever | Low | Add max iteration limit (e.g., 50 lines) |

---

*Last Updated: 2026-03-22*
*Version: v0.5.0*
