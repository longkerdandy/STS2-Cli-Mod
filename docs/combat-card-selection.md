# Combat Card Selection Analysis

Analysis of all cards, powers, relics, and potions that trigger in-combat card selection UI.

## Selection UI Types

There are **three distinct UI types** for card selection during combat, each requiring a different handler:

### Type A — Hand Select (`NPlayerHand.SelectCards`)

Player selects cards directly from their hand. The hand enters `SimpleSelect` or `UpgradeSelect` mode — background dims, ineligible cards are hidden.

Triggered via: `CardSelectCmd.FromHand()`, `CardSelectCmd.FromHandForDiscard()`, `CardSelectCmd.FromHandForUpgrade()`

### Type B — Grid Overlay (`NSimpleCardSelectScreen`)

A fullscreen overlay pops up showing cards from draw pile, discard pile, or generated pools in a grid layout. Handled by `grid_select_card` / `grid_select_skip`.

Triggered via: `CardSelectCmd.FromSimpleGrid()`

### Type C — Choose-A-Card (`NChooseACardSelectionScreen`)

Shows up to 3 generated cards to choose from, with optional skip. Similar to card reward selection. May share logic with existing `potion_select_card`.

Triggered via: `CardSelectCmd.FromChooseACardScreen()`

---

## Type A — Hand Select (24 cards)

### Discard from hand

| Card | Character | Count | Notes |
|------|-----------|-------|-------|
| Acrobatics | Silent | 1 | |
| DaggerThrow | Silent | 1 | |
| HiddenDaggers | Silent | 2 | Variable (`Cards`) |
| Prepare | Silent | 2 | Variable (`Cards`) |
| Survivor | Silent | 1 | |

### Exhaust from hand

| Card | Character | Count | Notes |
|------|-----------|-------|-------|
| Brand | Ironclad | 1 | |
| BurningPact | Ironclad | 1 | |
| TrueGrit | Ironclad | 1 | Upgraded only — base version exhausts randomly |
| Purity | Colorless | 0–3 (0–5 upgraded) | Optional selection |
| Scavenge | Defect | 1 | |

### Upgrade from hand

| Card | Character | Count | Notes |
|------|-----------|-------|-------|
| Armaments | Ironclad | 1 | Uses `FromHandForUpgrade` / `UpgradeSelect` mode |

### Put back on top of draw pile

| Card | Character | Count | Notes |
|------|-----------|-------|-------|
| Glimmer | Regent | 1 | Variable (`PutBack`) |
| PhotonCut | Regent | 1 | Variable (`PutBack`) |
| ThinkingAhead | Colorless | 1 | |

### Transform / morph

| Card | Character | Count | Notes |
|------|-----------|-------|-------|
| Begone | Regent | 1 | Transforms selected card into MinionStrike |
| Guards | Regent | 0–any | Transforms selected cards into MinionSacrifice |

### Copy to hand

| Card | Character | Count | Notes |
|------|-----------|-------|-------|
| DualWield | Event (Ironclad pool) | 1 | Filter: Attack or Power only |
| HeirloomHammer | Regent | 1 | Filter: Colorless cards only |

### Apply keyword / modifier

| Card | Character | Count | Notes |
|------|-----------|-------|-------|
| HandTrick | Silent | 1 | Gives Sly keyword; filter: Skill, not already Sly |
| SculptingStrike | Necrobinder | 1 | Gives Ethereal; filter: not already Ethereal |
| Snap | Necrobinder | 1 | Gives Retain; filter: not already Retain |
| Transfigure | Necrobinder | 1 | Cost +1, gives Replay |

### Special effects

| Card | Character | Count | Notes |
|------|-----------|-------|-------|
| DecisionsDecisions | Regent | 1 | Select a Skill to auto-play 3 times; filter: Skill, not Unplayable |
| Nightmare | Silent | 1 | Selected card used for NightmarePower effect |

---

## Type B — Grid Overlay (12 cards)

### From discard pile

| Card | Character | Effect | Count | Notes |
|------|-----------|--------|-------|-------|
| Headbutt | Ironclad | Put on top of draw pile | 1 | |
| Hologram | Defect | Move to hand | 1 | |
| CosmicIndifference | Regent | Put on top of draw pile | 1 | |
| Dredge | Necrobinder | Move to hand | variable | min(Cards, 10 - hand size) |
| Graveblast | Necrobinder | Move to hand | 1 | |

### From draw pile

| Card | Character | Effect | Count | Notes |
|------|-----------|--------|-------|-------|
| Charge | Regent | Transform into MinionDiveBomb | 2 | Variable (`Cards`) |
| Cleanse | Necrobinder | Exhaust | 1 | |
| Seance | Necrobinder | Transform into Soul | 1 | Variable (`Cards`) |
| SecretTechnique | Colorless | Move to hand | 1 | Filter: Skill only |
| SecretWeapon | Colorless | Move to hand | 1 | Filter: Attack only |
| SeekerStrike | Colorless | Move to hand | 1 | From random top N of draw pile |
| Wish | Event | Move to hand | 1 | |

---

## Type C — Choose-A-Card (3 cards)

Generated cards displayed in a reward-style selection screen. All are skippable.

| Card | Character | Pool Source | Notes |
|------|-----------|-------------|-------|
| Discovery | Colorless | 3 random from character pool | Selected card added to hand for free |
| Quasar | Regent | 3 random from Colorless pool | Selected card added to hand |
| Splash | Colorless | 3 random Attacks from other character pools | Selected card added to hand for free |

---

## Non-Card Triggers

### Powers (6 total)

| Power | Trigger | UI Type | Effect | Count |
|-------|---------|---------|--------|-------|
| EntropyPower | Turn start | A | Select hand cards to transform into random cards | `Amount` |
| ToolsOfTheTradePower | Turn start | A | Discard from hand | `Amount` |
| TyrannyPower | Turn start | A | Exhaust from hand | `Amount` |
| WellLaidPlansPower | Before end-of-turn discard | A | Give temporary Retain | 0–`Amount` |
| ForegoneConclusionPower | Before hand draw | B | Select from draw pile to add to hand | `Amount` |
| StratagemPower | After shuffle | B | Select from draw pile to add to hand | `Amount` |

### Potions (9 total)

| Potion | UI Type | Effect | Count |
|--------|---------|--------|-------|
| GamblersBrew | A | Discard any number, redraw equal amount | 0–any |
| Ashwater | A | Exhaust any number from hand | 0–any |
| TouchOfInsanity | A | Select 1 card to make permanently free; filter: has energy/star cost | 1 |
| DropletOfPrecognition | B | Select from draw pile to add to hand | 1 |
| LiquidMemories | B | Select from discard pile, cost becomes 0, add to hand | 1 |
| AttackPotion | C | 3 random Attacks from character pool, pick 1 | 1 (skippable) |
| SkillPotion | C | 3 random Skills from character pool, pick 1 | 1 (skippable) |
| PowerPotion | C | 3 random Powers from character pool, pick 1 | 1 (skippable) |
| ColorlessPotion | C | 3 random from Colorless pool, pick 1 | 1 (skippable) |

### Relics (1 total)

| Relic | Trigger | UI Type | Effect | Count |
|-------|---------|---------|--------|-------|
| GamblingChip | First turn start | A | Discard any number, redraw equal amount | 0–any |

---

## Summary

| UI Type | Cards | Powers | Potions | Relics | Total |
|---------|-------|--------|---------|--------|-------|
| A — Hand Select | 24 | 4 | 3 | 1 | **32** |
| B — Grid Overlay | 12 | 2 | 2 | 0 | **16** |
| C — Choose-A-Card | 3 | 0 | 4 | 0 | **7** |
| **Total** | **39** | **6** | **9** | **1** | **55** |

## Implementation Notes

- **Type A** needs a new handler. Detect `NPlayerHand` entering `SimpleSelect` / `UpgradeSelect` mode. State must include: selection mode, prompt text, selectable cards, already-selected cards, min/max constraints, confirm button availability.
- **Type B** is handled by `grid_select_card` / `grid_select_skip`. `NSimpleCardSelectScreen` shares the base class `NCardGridSelectionScreen` with deck selection screens, so the same handler works for both combat and non-combat grid overlays.
- **Type C** may reuse existing `potion_select_card` / `potion_select_skip` if `NChooseACardSelectionScreen` is already handled by those commands. Verify coverage for card-triggered instances (Discovery, Quasar, Splash).
- Powers and relics trigger these UIs automatically during combat flow — the agent must detect and respond to them even when no explicit action was taken.
