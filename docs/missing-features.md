# Missing Features

Features identified by comparing with [STS2MCP](https://github.com/Gennadiyev/STS2MCP) that are not yet implemented in STS2-Cli-Mod.

## Priority 1 — Critical (blocks gameplay)

### 1.1 ~~Combat Hand Select (`combat_select_card` / `combat_confirm_selection`)~~ ✅ DONE

Implemented as `hand_select_card` and `hand_confirm_selection` commands. Screen detection returns `HAND_SELECT` when `NPlayerHand.IsInCardSelection` is true (combat sub-state). State includes mode, prompt, selectable/selected cards, min/max constraints, and confirm button status. Selection done by emitting `Pressed` signal on hand card holders; confirmation by `ForceClick()` on `%SelectModeConfirmButton`.

### 1.2 ~~Relic Select Screen (`relic_select` / `relic_skip`)~~ ✅ DONE

Implemented as `relic_select` and `relic_skip` commands. Screen detection returns `RELIC_SELECT` when `NChooseARelicSelection` is found in the overlay stack. State includes available relics (id, name, description, rarity) and skip availability. Selection done by `ForceClick()` on `NRelicBasicHolder`; skip by `ForceClick()` on `NChoiceSelectionSkipButton`. Handler polls for overlay removal after action.

### 1.3 ~~Bundle Select (`bundle_select` / `bundle_confirm` / `bundle_cancel`)~~ ✅ DONE

Implemented as `bundle_select`, `bundle_confirm`, and `bundle_cancel` commands. Screen detection returns `BUNDLE_SELECT` when `NChooseABundleSelectionScreen` is found in the overlay stack (both combat and non-combat contexts). State includes bundle list with cards in each bundle, preview visibility, preview card details, and confirm/cancel button availability. Selection is a two-step flow: `bundle_select <index>` clicks the bundle hitbox to open preview, then `bundle_confirm` clicks the confirm button (or `bundle_cancel` to go back). Handler polls for overlay removal after confirm.

## Priority 2 — Important (affects specific scenarios or AI decision quality)

### 2.1 ~~Crystal Sphere Mini-Game~~ ✅ DONE

Implemented as `crystal_set_tool`, `crystal_click_cell`, and `crystal_proceed` commands. Screen detection returns `CRYSTAL_SPHERE` when `NCrystalSphereScreen` is found in the overlay stack. State includes 11x11 grid cells with hidden/clickable status, current tool, divinations remaining, revealed items (with anti-cheat: hidden cell items are never exposed), and proceed availability. Tool switching via `ForceClick()` on `NDivinationButton`; cell clicks via `EmitSignal(Released)` (proven AutoSlay pattern); proceed via `ForceClick()` on `NProceedButton`. The `_entity` field on `NCrystalSphereScreen` is accessed via cached reflection.

### 2.2 Draw/Discard/Exhaust Pile Contents

Currently the state only returns the **count** of cards in each pile. AI agents need to know the actual cards to make informed decisions (e.g., knowing what's left in the draw pile affects play order).

**Current gap**: `draw_pile_count`, `discard_pile_count`, `exhaust_pile_count` are integers only.

**STS2MCP reference**: Returns full card lists with name and description for each pile.

**Needed**:
- Extend `CombatStateDto` (or sub-DTOs) to include card lists for draw pile, discard pile, and exhaust pile
- Each card entry: id, name, cost, type, rarity, description (same format as hand cards)

### 2.3 Keyword Glossary

Game entities (cards, relics, potions) have keywords with hover-tip definitions. Currently we return keyword names as string arrays but not their definitions.

**Current gap**: Keywords like "Vulnerable", "Weak", "Ethereal" are returned as names only — no descriptions.

**STS2MCP reference**: Collects all HoverTips from entities across the game state and generates a unified glossary with `{name, description}` entries.

**Needed**:
- Extract keyword definitions from `HoverTip` data on cards, relics, potions
- Return as a glossary section in state response (deduplicated)
- Or attach keyword descriptions inline to each entity

### 2.4 ~~Relic Counter and Dynamic Description~~ ✅ DONE

Already implemented. `RelicStateDto.Counter` is populated from `relic.DisplayAmount` when `relic.ShowCounter` is true (null otherwise). `RelicStateDto.Description` already uses `relic.DynamicDescription.GetFormattedText()`, so it returns the live description with current values substituted — no separate `dynamic_description` field needed.


