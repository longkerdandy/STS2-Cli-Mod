# Potion Card Selection - Development Plan

This document outlines the implementation plan for potion card selection screens. Certain potions open interactive card selection interfaces that require special handling.

---

## Overview

### Problem

Some potions pause execution and open card selection screens:

| Potion ID | Selection Type | Source | Count | Can Skip |
|-----------|---------------|--------|-------|----------|
| `ATTACK_POTION` | Single | 3 random attacks | 1 | No |
| `SKILL_POTION` | Single | 3 random skills | 1 | No |
| `POWER_POTION` | Single | 3 random powers | 1 | No |
| `COLORLESS_POTION` | Single | 3 random colorless | 1 | Yes |
| `LIQUID_MEMORIES` | Single | Discard pile | 1 | No |
| `DROPLET_OF_PRECOGNITION` | Single | Draw pile | 1 | No |
| `GAMBLERS_BREW` | Multi | Hand | 0-10 | Yes (select 0) |
| `ASHWATER` | Multi | Hand | 1-10 | No |
| `TOUCH_OF_INSANITY` | Single | Hand | 1 | No |

**Current Issue**: `UsePotionAction` calls `CardSelectCmd.FromChooseACardScreen()` which blocks waiting for player input. The CLI has no detection/handling for these screens.

### Solution

Two-phase approach:
1. `use_potion` → Enqueues potion, detects selection screen, returns `selection_required` status
2. `potion_select_card` → Selects cards by ID (with optional nth) or `--skip` flag

---

## Command Renaming (Step 1)

### Renaming Map

| Old Command | New Command | Description |
|-------------|-------------|-------------|
| `choose_card` | `reward_choose_card` | Select card from reward screen |
| `skip_card` | `reward_skip_card` | Skip card reward |
| `claim_reward` | `reward_claim` | Claim non-card reward |
| `proceed` | `reward_proceed` | Leave reward screen |

### Files to Modify

1. `STS2.Cli.Cmd/Program.cs` - Update command registrations
2. `STS2.Cli.Mod/Server/PipeServer.cs` - Update route mappings
3. `STS2.Cli.Cmd/Services/CommandRunner.cs` - Update error code mappings (if any)
4. `AGENTS.md` - Update documentation

---

## New Commands

### potion_select_card

**Syntax**:
```bash
# Single selection
sts2 potion_select_card <card_id> [--nth <n>]

# Multi-selection
sts2 potion_select_card <card_id1> [--nth1 <n>] <card_id2> [--nth2 <n>] ...

# With skip flag
sts2 potion_select_card --skip
```

**Examples**:
```bash
# Select Bash from selection screen
sts2 potion_select_card BASH

# Select Strike (2nd copy) and Defend (1st copy)
st2 potion_select_card STRIKE --nth 1 DEFEND --nth 0

# Skip selection (if allowed)
st2 potion_select_card --skip
```

**Implementation**: `PotionSelectCardHandler.cs`

---

## Implementation Steps

### Step 1: Rename Existing Reward Commands

**Modified Files**:
- `STS2.Cli.Cmd/Program.cs`
- `STS2.Cli.Mod/Server/PipeServer.cs`
- `AGENTS.md`

**Changes**:
- Rename `choose_card` → `reward_choose_card`
- Rename `skip_card` → `reward_skip_card`
- Rename `claim_reward` → `reward_claim`
- Rename `proceed` → `reward_proceed`

### Step 2: Extend use_potion for Selection Detection

**Modified File**: `STS2.Cli.Mod/Actions/UsePotionHandler.cs`

**Logic**:
1. Check if potion requires card selection via `PotionUtils.RequiresCardSelection()`
2. If yes, poll briefly (5s max) for selection screen appearance
3. If screen opens, extract card list and return `selection_required` status
4. If no screen appears within timeout, proceed with normal completion waiting

**Response for selection-required potions**:
```json
{
  "ok": true,
  "data": {
    "status": "selection_required",
    "selection_type": "choose_from_discard",
    "min_select": 1,
    "max_select": 1,
    "can_skip": false,
    "cards": [
      {"index": 0, "card_id": "STRIKE_IRONCLAD", "card_name": "Strike", "cost": 1}
    ]
  }
}
```

### Step 3: Create Potion Card Selection Handlers

**New File**: `STS2.Cli.Mod/Actions/PotionSelectCardHandler.cs`

**Features**:
- Validates current screen is `POTION_SELECTION`
- Supports multi-selection by card ID + nth
- Emits `NCardHolder.SignalName.Pressed` for each selection
- Validates selection count against constraints

**New File**: `STS2.Cli.Mod/Utils/PotionUtils.cs`

**Features**:
- `RequiresCardSelection(potionId)` - Check if potion opens selection
- `GetSelectionType(potionId)` - Get selection category
- `GetMinSelection(potionId)` - Get minimum cards to select
- `GetMaxSelection(potionId)` - Get maximum cards to select
- `CanSkip(potionId)` - Check if selection can be skipped

### Step 4: Update Screen Detection

**Modified Files**:
- `STS2.Cli.Mod/State/GameStateExtractor.cs` - Add `POTION_SELECTION` detection
- `STS2.Cli.Mod/Models/Dto/GameStateDto.cs` - Add `PotionSelection` property

**New File**: `STS2.Cli.Mod/Models/Dto/PotionSelectionStateDto.cs`

**Fields**:
- `PotionId` - The potion that opened selection
- `SelectionType` - Category of selection
- `MinSelect` / `MaxSelect` - Constraints
- `CanSkip` - Whether skip is allowed
- `Cards` - List of selectable cards with ID, name, cost, etc.

### Step 5: Register New CLI Commands

**Modified File**: `STS2.Cli.Cmd/Program.cs`

**Add**:
```csharp
// potion_select_card <card_id>... [--nth <n>...] [--skip]
rootCommand.AddCommand(CreatePotionSelectCardCommand(prettyOption));
```

**Features**:
- Variable argument count for multi-select
- Optional --nth for each card (defaults to 0)
- --skip flag to skip selection

### Step 6: Server Routing and Error Codes

**Modified File**: `STS2.Cli.Mod/Server/PipeServer.cs`

**Add Route**:
```csharp
"potion_select_card" => await HandlePotionSelectCardRequestAsync(
    request.CardIds, 
    request.NthValues, 
    request.Skip),
```

**Modified File**: `STS2.Cli.Cmd/Services/CommandRunner.cs`

**New Error Codes**:
- `NOT_IN_POTION_SELECTION` (Exit 2)
- `CANNOT_SKIP` (Exit 2)
- `INVALID_SELECTION_COUNT` (Exit 3)
- `CARD_NOT_FOUND` (Exit 3)

### Step 7: Update Documentation

**Modified File**: `AGENTS.md`

Update test command list with new names and add potion selection commands.

---

## Usage Flow Examples

### Example 1: Liquid Memories (Single Select)

```bash
# Check state
$ sts2 state
{ "screen": "COMBAT", ... }

# Use potion
$ sts2 use_potion LIQUID_MEMORIES
{
  "ok": true,
  "data": {
    "status": "selection_required",
    "selection_type": "choose_from_discard",
    "min_select": 1,
    "max_select": 1,
    "can_skip": false,
    "cards": [
      {"index": 0, "card_id": "STRIKE", "card_name": "Strike", "cost": 1},
      {"index": 1, "card_id": "BASH", "card_name": "Bash", "cost": 2}
    ]
  }
}

# Check state shows selection screen
$ sts2 state
{
  "screen": "POTION_SELECTION",
  "potion_selection": { ... }
}

# Select card
$ sts2 potion_select_card BASH
{
  "ok": true,
  "data": {
    "selected_count": 1,
    "selected_cards": ["BASH"]
  }
}

# Back to combat
$ sts2 state
{ "screen": "COMBAT", ... }
```

### Example 2: Gambler's Brew (Multi-Select)

```bash
$ sts2 use_potion GAMBLERS_BREW
# ... returns selection_required with can_skip: true ...

# Select multiple cards to discard
$ sts2 potion_select_card STRIKE --nth 0 DEFEND --nth 1
{
  "ok": true,
  "data": {
    "selected_count": 2,
    "selected_cards": ["STRIKE", "DEFEND"],
    "cards_discarded": 2,
    "cards_drawn": 2
  }
}
```

### Example 3: Colorless Potion (Skip)

```bash
$ sts2 use_potion COLORLESS_POTION
# ... returns selection_required with can_skip: true ...

# Skip the selection
$ sts2 potion_select_card --skip
{
  "ok": true,
  "data": { "skipped": true }
}
```

---

## Error Codes

| Error Code | Exit Code | Description |
|------------|-----------|-------------|
| `NOT_IN_POTION_SELECTION` | 2 | Not in potion card selection screen |
| `CANNOT_SKIP` | 2 | This selection cannot be skipped |
| `INVALID_SELECTION_COUNT` | 3 | Wrong number of cards selected |
| `CARD_NOT_FOUND` | 3 | Specified card not in selection |
| `SELECTION_CANCELLED` | 2 | Selection was cancelled by game |

---

## File Checklist

### Modified Files

1. `STS2.Cli.Cmd/Program.cs` - Command registrations
2. `STS2.Cli.Mod/Server/PipeServer.cs` - Route mappings
3. `STS2.Cli.Cmd/Services/CommandRunner.cs` - Error codes
4. `STS2.Cli.Mod/Actions/UsePotionHandler.cs` - Selection detection
5. `STS2.Cli.Mod/State/GameStateExtractor.cs` - Screen detection
6. `STS2.Cli.Mod/Models/Dto/GameStateDto.cs` - Add property
7. `AGENTS.md` - Documentation

### New Files

1. `STS2.Cli.Mod/Actions/PotionSelectCardHandler.cs`
2. `STS2.Cli.Mod/Utils/PotionUtils.cs`
3. `STS2.Cli.Mod/Models/Dto/PotionSelectionStateDto.cs`

---

*Created: 2026-03-23*
*Version: v0.6.0*
