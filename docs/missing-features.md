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

### 2.2 ~~Draw/Discard/Exhaust Pile Contents~~ ✅ DONE

Implemented with `draw_pile`, `discard_pile`, and `exhaust_pile` fields in combat state. Each card entry includes: id, name, type, rarity, cost, keywords, is_upgraded. The draw pile is shuffled (order randomized) to match in-game view without revealing draw order. Use `--include-pile-details` flag with `state` command to include full descriptions (default is off to reduce payload size).

### 2.3 ~~Keyword Glossary~~ ✅ DONE (Documented)

Only 7 card keywords exist (Exhaust, Ethereal, Innate, Unplayable, Retain, Sly, Eternal). Instead of adding code overhead, their definitions are documented in [cli-reference.md](cli-reference.md#card-keywords). No code changes needed.

### 2.4 ~~Relic Counter and Dynamic Description~~ ✅ DONE

Already implemented. `RelicStateDto.Counter` is populated from `relic.DisplayAmount` when `relic.ShowCounter` is true (null otherwise). `RelicStateDto.Description` already uses `relic.DynamicDescription.GetFormattedText()`, so it returns the live description with current values substituted — no separate `dynamic_description` field needed.


