# Development Plan: Top-Level Player State in `state` Response

## Background

Currently the `sts2 state` command only returns player data (HP, max_hp, gold, relics, potions)
inside `combat.player`, which is only populated on COMBAT / HAND_SELECT / GRID_CARD_SELECT / TRI_SELECT screens.
On all other in-run screens (MAP, EVENT, REST_SITE, REWARD, SHOP, TREASURE, etc.) these fields are absent.

However, the underlying game objects are **always available** during a run:

- `RunManager.Instance.DebugOnlyGetState().Players[0]` — the `Player` object, run-scoped
- `Player.Gold`, `Player.Creature.CurrentHp`, `Player.Creature.MaxHp` — always valid
- `Player.Relics`, `Player.PotionSlots`, `Player.Deck` — always valid

Proof:
- `ShopStateBuilder` already reads `inventory.Player.Gold` outside combat (`ShopStateBuilder.cs:40`)
- `RunManager.UpdatePlayerStatsInMapPointHistory()` reads HP/gold from `State.Players` at map transitions (`RunManager.cs:1308-1324`)

The `PlayerStateBuilder.Build()` already cleanly separates run-scoped vs combat-scoped fields —
combat-scoped fields (energy, hand/draw/discard/exhaust counts, orbs, stars, pets)
are guarded by `if (playerCombatState != null)` and will naturally be zero/null outside combat.

## Goal

Add a top-level `player` field to the `state` JSON response, populated on **all in-run screens**.
This gives consumers (AI Agent, etc.) real-time access to HP, gold, relics, and potions
regardless of screen type.

## Changes

### 1. Add `Player` property to `GameStateDto`

**File**: `STS2.Cli.Mod/Models/State/GameStateDto.cs`

Add a new property after the `Error` field (after line 25):

```csharp
/// <summary>
///     Player state available on all in-run screens.
///     Contains run-scoped data (HP, gold, relics, potions, deck count).
///     Combat-scoped fields (energy, hand/draw/discard counts, etc.) are
///     zero/null outside of combat.
///     Null when not in an active run (menus, character select).
/// </summary>
public PlayerStateDto? Player { get; set; }
```

### 2. Populate `Player` in `StateHandler.GetState()`

**File**: `STS2.Cli.Mod/State/StateHandler.cs`

Insert after the `GameStateDto` construction (after line 61, before `switch (state.Screen)`):

```csharp
// Populate top-level player for all in-run screens
if (RunManager.Instance.IsInProgress)
{
    var runState = RunManager.Instance.DebugOnlyGetState();
    if (runState?.Players.Count > 0)
        state.Player = PlayerStateBuilder.Build(runState.Players[0]);
}
```

Required import (already present via `using MegaCrit.Sts2.Core.Runs;` at line 11):
- `RunManager` — already imported

### 3. No changes to `PlayerStateBuilder`

`PlayerStateBuilder.Build()` (`STS2.Cli.Mod/State/Builders/PlayerStateBuilder.cs:19-79`)
already handles both cases correctly:

- **Run-scoped fields** (lines 26-39): Always populated from `Player` and `Creature`
  — these will work identically whether called from combat or non-combat context.
- **Combat-scoped fields** (lines 43-76): Guarded by `if (playerCombatState != null)`.
  Outside combat, `Player.PlayerCombatState` is null, so these fields remain at their
  default zero/null values.

No modification needed.

### 4. No changes to `CombatStateBuilder`

`CombatStateBuilder` continues to populate `combat.player` as before.
During combat, consumers will have both `data.player` (top-level, run-scoped access)
and `data.combat.player` (with combat-scoped additions). Both are built from the same
`PlayerStateBuilder.Build()`, so they are consistent.

No modification needed.

## JSON Output Example

### Before (MAP screen)

```json
{
  "screen": "MAP",
  "timestamp": 1234567890,
  "map": { ... }
}
```

### After (MAP screen)

```json
{
  "screen": "MAP",
  "timestamp": 1234567890,
  "player": {
    "characterId": "IRONCLAD",
    "characterName": "Ironclad",
    "relics": [...],
    "potions": [...],
    "gold": 120,
    "deckCount": 15,
    "maxEnergy": 3,
    "block": 0,
    "hp": 65,
    "maxHp": 80,
    "powers": [],
    "handCount": 0,
    "drawCount": 0,
    "discardCount": 0,
    "exhaustCount": 0,
    "energy": 0
  },
  "map": { ... }
}
```

### During COMBAT

Both `player` (top-level) and `combat.player` are populated.
`combat.player` additionally has non-zero combat-scoped fields (energy, hand count, etc.):

```json
{
  "screen": "COMBAT",
  "timestamp": 1234567890,
  "player": {
    "hp": 55,
    "maxHp": 80,
    "gold": 120,
    "relics": [...],
    "potions": [...],
    "energy": 3,
    "handCount": 5,
    "drawCount": 7,
    "discardCount": 3,
    ...
  },
  "combat": {
    "player": {
      "hp": 55,
      "maxHp": 80,
      "gold": 120,
      "relics": [...],
      "potions": [...],
      "energy": 3,
      "handCount": 5,
      "drawCount": 7,
      "discardCount": 3,
      ...
    },
    "hand": [...],
    "enemies": [...],
    ...
  }
}
```

Note: The top-level `player` and `combat.player` are built from the same `Player` game object
via the same `PlayerStateBuilder.Build()` call, so they contain identical data during combat.
`combat.player` is retained for backward compatibility.

## Scope

- **Files modified**: 2 (`GameStateDto.cs`, `StateHandler.cs`)
- **Lines added**: ~10
- **Risk**: Low — additive change, no existing fields modified
- **Backward compatibility**: Full — existing `combat.player` is unchanged,
  `player` is a new optional top-level field
