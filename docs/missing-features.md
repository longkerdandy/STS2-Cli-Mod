# Missing Features

Features identified by comparing with [STS2MCP](https://github.com/Gennadiyev/STS2MCP) that are not yet implemented in STS2-Cli-Mod.

## Priority 1 — Critical (blocks gameplay)

### 1.1 ~~Combat Hand Select (`combat_select_card` / `combat_confirm_selection`)~~ ✅ DONE

Implemented as `hand_select_card` and `hand_confirm_selection` commands. Screen detection returns `HAND_SELECT` when `NPlayerHand.IsInCardSelection` is true (combat sub-state). State includes mode, prompt, selectable/selected cards, min/max constraints, and confirm button status. Selection done by emitting `Pressed` signal on hand card holders; confirmation by `ForceClick()` on `%SelectModeConfirmButton`.

### 1.2 Relic Select Screen (`relic_select` / `relic_skip`)

After defeating a boss, players choose one of several boss relics. Some events also present relic choices. This is a distinct UI from treasure room relic pickup.

**Current gap**: `pick_relic` handles treasure room relics only. Boss relic rewards and event relic choices are not supported.

**STS2MCP reference**: `relic_select(relic_index)` to pick, `relic_skip()` to skip. State includes available relics and whether skipping is allowed.

**Needed**:
- Detect relic selection overlay/screen
- State extraction: available relics with names/descriptions, skip availability
- Action: select relic by index, skip selection

### 1.3 Bundle Select (`bundle_select` / `bundle_confirm` / `bundle_cancel`)

Some game mechanics offer card bundle packs (groups of cards presented as bundles). The player can preview each bundle and choose one.

**Current gap**: Not implemented at all.

**STS2MCP reference**: `bundle_select(bundle_index)` to preview, `bundle_confirm_selection()` to confirm, `bundle_cancel_selection()` to cancel. State includes bundle list with cards in each bundle and preview status.

**Needed**:
- Detect bundle selection screen
- State extraction: bundle list, cards within each bundle, preview state
- Action: preview bundle, confirm, cancel

## Priority 2 — Important (affects specific scenarios or AI decision quality)

### 2.1 Crystal Sphere Mini-Game

The Crystal Sphere event features a minesweeper-style mini-game with a grid of hidden cells. The player uses divination tools to reveal cells and collect items.

**Current gap**: Not implemented at all.

**STS2MCP reference**: Three tools — `crystal_sphere_set_tool(tool)` to switch tool, `crystal_sphere_click_cell(x, y)` to click a cell, `crystal_sphere_proceed()` to finish. State includes grid layout, clickable cells, revealed items, current tool, and remaining actions.

**Needed**:
- Detect Crystal Sphere event screen
- State extraction: grid state, available tools, remaining clicks, revealed items
- Action: set tool, click cell, proceed when done

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

### 2.4 Relic Counter and Dynamic Description

Some relics track usage counts (e.g., "Used 3/5 times this combat") and have descriptions that update with current values.

**Current gap**: Only static `description` is returned.

**STS2MCP reference**: Returns `DisplayAmount` (counter value) and `DynamicDescription` (description with live values substituted).

**Needed**:
- Add `counter` (or `display_amount`) field to `RelicStateDto` — nullable int
- Add `dynamic_description` field — nullable string, only set when different from static description

## Priority 3 — Nice to Have

### 3.1 Markdown Format Output

STS2MCP can render the full game state as formatted Markdown, which is easier for LLMs to consume directly than raw JSON.

**Current gap**: Only JSON output supported.

**Consideration**: Could be implemented as a `--format markdown` option on the `state` command. The Markdown renderer would live in the CLI project (STS2.Cli.Cmd), not the mod.

### 3.2 Multiplayer Mode

STS2MCP has 31 multiplayer tools including voting, auctioning, and multi-player state tracking.

**Current gap**: Not supported at all.

**Consideration**: Low priority unless multiplayer AI control is a goal. Would require significant effort — voting system, multi-player state extraction, undo mechanics, etc.
