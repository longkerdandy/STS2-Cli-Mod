# STS2 CLI Reference

CLI for controlling Slay the Spire 2 via named pipe. All responses are JSON to stdout: `{"ok": true, "data": {...}}` on success, `{"ok": false, "error": "CODE", "message": "..."}` on error.

Exit codes: 0=success, 1=connection, 2=invalid state, 3=invalid param, 4=timeout, 5=state changed.

## Global Options

| Option | Description |
|--------|-------------|
| `--version`, `-v` | Show CLI version |
| `--pretty`, `-p` | Pretty-print JSON output |

## Commands

All commands use stable IDs (not indices). Use `--nth N` (0-based, default 0) when multiple items share the same ID.

### Connection Test

#### ping

```
./sts2 ping
```

Test connection. Returns `{"ok": true}`.

### State Query

#### state

```
./sts2 state [--include-pile-details]
```

Get full game state. See [Game State Structure](#game-state-structure) below.

**Options:**
- `--include-pile-details` — Include full card descriptions in draw/discard/exhaust pile listings. Default is off to reduce payload size.

---

### Main Menu

#### continue_run

```
./sts2 continue_run
```

Continue a saved run from the main menu. Only available when `menu.has_run_save` is true. Clicks the Continue button and waits for the run to load (up to 15 seconds).

#### new_run

```
./sts2 new_run
```

Start a new game from the main menu. Only available when no saved run exists (`menu.has_run_save` is false). Clicks the Singleplayer button. If the player has completed at least one run, this opens the singleplayer submenu (screen becomes `SINGLEPLAYER_SUBMENU`) -- use `choose_game_mode` to select a mode. If it's the first ever game, goes directly to character select.

#### abandon_run

```
./sts2 abandon_run
```

Abandon the current saved run from the main menu. Only available when `menu.has_run_save` is true. Skips the confirmation popup. After abandoning, the menu refreshes and `new_run` becomes available.

#### choose_game_mode

```
./sts2 choose_game_mode <mode>
```

Select a game mode from the singleplayer submenu. Only available when `screen` is `SINGLEPLAYER_SUBMENU`. Valid modes: `standard`, `daily`, `custom`. After selecting, the screen transitions to character select (for standard) or the corresponding mode screen.

---

### Character Select

#### select_character

```
./sts2 select_character <character_id>
```

Select a character on the character select screen. ID matching is case-insensitive.

#### set_ascension

```
./sts2 set_ascension <level>
```

Set ascension level (0 to max) on the character select screen. Max level is read from the game (typically 20).

#### embark

```
./sts2 embark
```

Start a run from the character select screen. Requires a character to be selected first.

---

### Map Navigation

#### choose_map_node

```
./sts2 choose_map_node <col> <row>
```

Travel to a map node. Only nodes with state `TRAVELABLE` can be selected -- check `map.travelable_coords` in state.

---

### Combat

#### play_card

```
./sts2 play_card <card_id> [--nth <n>] [--target <combat_id>]
```

Play a card from hand. `--target` required for enemy-targeting cards only.

#### end_turn

```
./sts2 end_turn
```

End turn. Response contains all enemy action results -- always read it.

#### use_potion

```
./sts2 use_potion <potion_id> [--nth <n>] [--target <combat_id>]
```

Use a potion. `--target` required for enemy-targeting potions. Some potions open a card selection screen (screen becomes `TRI_SELECT`) -- use `tri_select_card` or `tri_select_skip` to complete. Potions with `usage: AnyTime` (e.g., Blood Potion, Fruit Juice, Entropic Brew, Foul Potion) can be used outside combat -- `--target` is not allowed outside combat.

---

### Combat Sub-states (Hand/Grid Selection)

#### hand_select_card

```
./sts2 hand_select_card <card_id> [<card_id>...] [--nth <n>...]
```

Select cards from hand during combat selection mode (discard, exhaust, upgrade prompts). When `MinSelect == MaxSelect`, selection auto-completes after selecting the required number of cards. Otherwise, use `hand_confirm_selection` to finalize.

#### hand_confirm_selection

```
./sts2 hand_confirm_selection
```

Confirm the current hand card selection. Only needed when `hand_select.require_manual_confirmation` is true (i.e., `MinSelect != MaxSelect`).

#### grid_select_card / grid_select_skip

```
./sts2 grid_select_card <card_id> [<card_id>...] [--nth <n>...]
./sts2 grid_select_skip
```

Select or skip cards from a grid-style card selection screen (card removal, upgrade, transform, enchant, combat grid overlays). Check `grid_card_select.min_select` / `max_select` in state for how many to pick. Skip only works when `grid_card_select.cancelable` is true.

---

### Event Rooms

#### choose_event

```
./sts2 choose_event <index>
```

Choose event option by 0-based index. For Ancient events, advance dialogue first.

#### advance_dialogue

```
./sts2 advance_dialogue [--auto]
```

Advance Ancient event dialogue. Use `--auto` to skip all dialogue lines until options appear. Then use `choose_event`.

---

### Rest Site

#### choose_rest_option

```
./sts2 choose_rest_option <option_id>
```

Choose a rest site (campfire) option by ID. Common options: `HEAL` (restore HP), `SMITH` (upgrade a card). SMITH opens a card selection overlay (screen becomes `GRID_CARD_SELECT`) -- use `grid_select_card` to complete. After choosing, use `proceed` to leave the rest site if `rest_site.can_proceed` is true.

---

### Treasure Room

#### open_chest

```
./sts2 open_chest
```

Open the treasure chest in a treasure room. After opening, relics are revealed -- use `pick_relic` to pick one, or `proceed` to skip.

#### pick_relic

```
./sts2 pick_relic <index>
```

Pick a relic from an opened treasure chest by 0-based index. After picking, use `proceed` to leave the treasure room.

---

### Shop

#### shop_buy_card

```
./sts2 shop_buy_card <card_id> [--nth <n>]
```

Buy a card from the shop by card ID. Use `--nth` to disambiguate when multiple copies of the same card exist.

#### shop_buy_relic

```
./sts2 shop_buy_relic <relic_id> [--nth <n>]
```

Buy a relic from the shop by relic ID.

#### shop_buy_potion

```
./sts2 shop_buy_potion <potion_id> [--nth <n>]
```

Buy a potion from the shop by potion ID. Fails if the potion belt is full.

#### shop_remove_card

```
./sts2 shop_remove_card
```

Buy the card removal service from the shop. Opens a grid card selection screen (screen becomes `GRID_CARD_SELECT`) -- use `grid_select_card` to pick a card to remove, or `grid_select_skip` to cancel. After the shop, use `proceed` to leave.

---

### Post-combat Rewards

#### reward_claim

```
./sts2 reward_claim --type <type> [--id <id>] [--nth <n>]
```

Claim a non-card reward. Types: `gold`, `potion`, `relic`, `special_card`. `--id` required for all except `gold`.

#### reward_choose_card / reward_skip_card

```
./sts2 reward_choose_card --type card --card_id <card_id> [--nth <n>]
./sts2 reward_skip_card --type card [--nth <n>]
```

Pick or skip a card reward. `--nth` selects which card reward when multiple exist.

---

### Overlay Screens (Shared Across Contexts)

#### tri_select_card

```
./sts2 tri_select_card <card_id> [<card_id>...] [--nth <n>...]
```

Select cards from a three-choose-one card selection screen (triggered by potions, cards like Discovery/Quasar/Splash, relics like Toolbox, and monsters). Check `tri_select.min_select` / `max_select` in state for how many to pick.

#### tri_select_skip

```
./sts2 tri_select_skip
```

Skip three-choose-one card selection (only if `tri_select.can_skip` is true).

#### relic_select / relic_skip

```
./sts2 relic_select <index>
./sts2 relic_skip
```

Select or skip a relic from the boss/event relic choice screen. Index is 0-based.

#### bundle_select

```
./sts2 bundle_select <index>
```

Preview a bundle on the bundle selection screen (triggered by the Scroll Boxes relic). Index is 0-based. Opens a preview showing the cards in the selected bundle.

#### bundle_confirm

```
./sts2 bundle_confirm
```

Confirm the currently previewed bundle. The bundle's cards are added to the deck and the selection screen closes.

#### bundle_cancel

```
./sts2 bundle_cancel
```

Cancel the current bundle preview and return to bundle selection. Allows previewing a different bundle.

#### crystal_set_tool

```
./sts2 crystal_set_tool <tool>
```

Set the divination tool in the Crystal Sphere mini-game. Tool is `big` (3x3 area) or `small` (1 cell).

#### crystal_click_cell

```
./sts2 crystal_click_cell <x> <y>
```

Click a cell in the Crystal Sphere mini-game to clear fog. Coordinates are 0-based (range 0..10). After clicking, a reward overlay may appear if an item was revealed -- handle the reward first, then continue clicking.

#### crystal_proceed

```
./sts2 crystal_proceed
```

Leave the Crystal Sphere mini-game after all divinations are exhausted. The proceed button must be enabled (`can_proceed` is true in state).

---

### Navigation (Shared Across Contexts)

#### proceed

```
./sts2 proceed
```

Leave reward screen, FakeMerchant event, rest site (after choosing an option), treasure room (after picking/skipping relic), or merchant room and proceed to map.

---

### Game Over

#### return_to_menu

```
./sts2 return_to_menu
```

Return to the main menu from the game over screen. Only available when `screen` is `GAME_OVER` and `game_over.can_return_to_menu` is true. After returning to menu, the screen becomes `MENU` -- use `continue_run`, `new_run`, or `abandon_run` to proceed.

---

### Local-only Commands

#### report_bug

```
./sts2 report_bug --title <title> --description <desc> [--last-command <cmd>] [--last-response <json>] [--severity <level>] [--labels <labels>]
```

Save a structured bug report to a local JSON file. This is a local-only command -- no pipe connection required. Optionally captures a game state snapshot if the mod is reachable (2s timeout).

**Required options:**
- `--title` — Short summary of the bug
- `--description` — Detailed description: what happened, what was expected

**Optional options:**
- `--last-command` — The CLI command that triggered the bug (e.g., `"play_card STRIKE --nth 0"`)
- `--last-response` — The JSON response from the last command (parsed as JSON if valid, stored as raw string otherwise)
- `--severity` — Bug severity: `low`, `medium` (default), `high`, `critical`
- `--labels` — Comma-separated labels for categorization (e.g., `"combat,play_card"`)

**Output:** `{"ok": true, "data": {"bug_id": "BUG-...", "file": "sts2-cli-bugs/BUG-....json", "has_game_state": true/false}}`

Reports are saved as pretty-printed JSON in the `sts2-cli-bugs/` directory alongside the CLI executable. Each report includes a generated bug ID (`BUG-yyyyMMdd-HHmmss-fff`), CLI version, and optional game state snapshot.

## Game State Structure

Returned by `state` in the `data` field. Only the relevant screen's data is populated; others are null/omitted.

```
data
├── screen              # MENU | SINGLEPLAYER_SUBMENU | CHARACTER_SELECT
│                       # MAP | COMBAT | HAND_SELECT | GRID_CARD_SELECT
│                       # EVENT | REST_SITE | TREASURE | SHOP
│                       # REWARD | CARD_REWARD | TRI_SELECT | RELIC_SELECT
│                       # BUNDLE_SELECT | CRYSTAL_SPHERE
│                       # GAME_OVER | UNKNOWN
├── timestamp           # Unix ms
│
├── menu                      # Only when screen is MENU
│   └── has_run_save        # bool, true if a saved run exists (continue_run available)
├── singleplayer_submenu      # Only when screen is SINGLEPLAYER_SUBMENU
│   ├── standard_available  # bool, always true
│   ├── daily_available     # bool, true if daily run epoch is unlocked
│   └── custom_available    # bool, true if custom/seeds epoch is unlocked
├── character_select
│   ├── available_characters[]  # {character_id, character_name, is_locked, is_selected}
│   ├── selected_character      # string?, null if none selected
│   ├── current_ascension, max_ascension
│   └── can_embark
│
├── map
│   ├── act_index, act_floor, total_floor
│   ├── columns, rows
│   ├── current_coord       # {col, row}, null at start
│   ├── nodes[]
│   └── travelable_coords[] # [{col, row}]
│
├── combat
│   ├── encounter, turn_number, is_player_turn
│   ├── is_player_actions_disabled, is_combat_ending
│   ├── player
│   │   ├── character_id, character_name
│   │   ├── hp, max_hp, block, energy, max_energy
│   │   ├── gold, deck_count
│   │   ├── hand_count, draw_count, discard_count, exhaust_count
│   │   ├── relics[], potions[], powers[]
│   │   ├── pets[]?          # Necrobinder only
│   │   ├── orbs[]?, orb_slots?  # Defect only
│   │   └── stars?           # Regent only
│   ├── hand[]
│   ├── draw_pile[]       # Shuffled to hide draw order. Use --include-pile-details for descriptions
│   ├── discard_pile[]    # Use --include-pile-details for descriptions
│   ├── exhaust_pile[]    # Use --include-pile-details for descriptions
│   └── enemies[]
├── hand_select                 # Only when screen is HAND_SELECT (combat sub-state)
│   ├── mode                    # SimpleSelect or UpgradeSelect
│   ├── prompt                  # e.g., "Choose 1 card to discard."
│   ├── min_select, max_select
│   ├── cancelable, require_manual_confirmation
│   ├── can_confirm, selected_count
│   ├── selectable_cards[]      # Cards available to select from hand
│   └── selected_cards[]        # Cards already selected
├── grid_card_select
│   ├── selection_type          # remove, upgrade, transform, enchant, generic, unknown
│   ├── prompt, min_select, max_select, cancelable
│   └── cards[]
│
├── event
│   ├── event_id, title, description, layout_type
│   ├── is_finished, is_in_dialogue
│   ├── current_dialogue_line, total_dialogue_lines  # Ancient only
│   └── options[]
├── rest_site
│   ├── options[]           # [{index, option_id, name, description, is_enabled}]
│   └── can_proceed         # bool, true after an option has been chosen
├── treasure
│   ├── is_chest_opened     # bool, whether the chest has been opened
│   ├── relics[]            # [{index, id, name, description, rarity}] -- available after opening
│   ├── can_proceed         # bool, true after picking/skipping relic
│   └── can_skip            # bool, true if relic can be skipped via proceed
├── shop
│   ├── cards[]             # [{index, card_id, card_name, description, card_type, rarity, cost, is_on_sale, is_stocked}]
│   ├── relics[]            # [{index, relic_id, relic_name, description, rarity, cost, is_stocked}]
│   ├── potions[]           # [{index, potion_id, potion_name, description, rarity, cost, is_stocked}]
│   ├── card_removal        # {cost, is_used} or null
│   ├── player_gold         # int
│   └── can_proceed         # bool, true when proceed button is enabled
│
├── rewards
│   ├── can_skip            # bool, false when rewards are mandatory (e.g., NeowsBones relic)
│   └── rewards[]
├── tri_select
│   ├── selection_type, min_select, max_select, can_skip
│   └── cards[]
├── relic_select
│   ├── relics[]            # [{index, id, name, description, rarity}]
│   └── can_skip            # bool, true if selection can be skipped
├── bundle_select
│   ├── bundles[]           # [{index, card_count, cards[]}] -- cards are selectable cards
│   ├── preview_showing     # bool, true when a bundle preview is open
│   ├── preview_cards[]     # [{index, card_id, card_name, description, card_type, cost}] -- shown during preview
│   ├── can_confirm         # bool, true when confirm button is enabled
│   └── can_cancel          # bool, true when cancel button is enabled
├── crystal_sphere
│   ├── grid_width, grid_height  # int (always 11)
│   ├── cells[]             # [{x, y, is_hidden, is_clickable, item_type?, is_good?}]
│   ├── clickable_cells[]   # [{x, y}] -- convenience list of clickable coordinates
│   ├── revealed_items[]    # [{item_type, is_good, x, y, width, height}]
│   ├── tool                # "big" | "small" | "none"
│   ├── can_use_big_tool, can_use_small_tool  # bool
│   ├── divinations_left    # int, remaining divination uses
│   └── can_proceed         # bool, true when proceed button is enabled
│
└── game_over                 # Only when screen is GAME_OVER
    ├── is_victory          # bool, true if defeated final boss
    ├── floor               # int, floor where run ended
    ├── character_id        # string, character used
    ├── score               # int, final score
    ├── epochs_discovered   # int, number of epochs discovered
    ├── can_return_to_menu  # bool, true when main menu button is available
    └── can_continue        # bool, true when continue/summary button is available
```

### Card Object (in hand)

| Field | Type | Description |
|-------|------|-------------|
| `index` | int | Position in hand (0-based) |
| `id` | string | Card identifier (use with `play_card`) |
| `name` | string | Display name |
| `description` | string | Card effect text |
| `type` | string | `Attack`, `Skill`, `Power`, `Status`, `Curse` |
| `rarity` | string | `Basic`, `Common`, `Uncommon`, `Rare`, `Ancient`, `Event`, `Token`, `Status`, `Curse` |
| `target_type` | string | `None`, `Self`, `AnyEnemy`, `AllEnemies`, `RandomEnemy`, `AnyAlly`, etc. |
| `cost` | int | Energy cost (-1 = X-cost) |
| `star_cost` | int? | Regent only (-1 = X-star) |
| `keywords` | string[] | `Exhaust`, `Ethereal`, `Innate`, `Retain`, `Sly`, etc. |
| `tags` | string[] | `Strike`, `Defend`, etc. |
| `damage` | int? | Preview damage (after all modifiers) |
| `block` | int? | Preview block (after all modifiers) |
| `enchantment` | string? | Enchantment model ID |
| `affliction` | string? | Affliction model ID |
| `is_upgraded` | bool | Whether upgraded |
| `can_play` | bool | Whether playable right now |
| `unplayable_reason` | string? | Why not playable |

### Pile Card Object (draw/discard/exhaust)

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Card identifier |
| `name` | string | Display name |
| `type` | string | `Attack`, `Skill`, `Power`, `Status`, `Curse` |
| `rarity` | string | `Basic`, `Common`, `Uncommon`, `Rare`, `Ancient`, `Event`, `Token`, `Status`, `Curse` |
| `cost` | int | Energy cost |
| `keywords` | string[] | Active keywords |
| `is_upgraded` | bool | Whether upgraded |
| `description` | string? | Full description (only with `--include-pile-details`) |

### Enemy Object

| Field | Type | Description |
|-------|------|-------------|
| `combat_id` | uint | **Stable** target ID for `--target` (survives other enemies dying) |
| `id` | string | Enemy type identifier |
| `name` | string | Display name |
| `hp`, `max_hp` | int | Health |
| `block` | int | Current block |
| `is_alive` | bool | Alive status |
| `is_minion` | bool | Summoned minion |
| `move_id` | string | Current move |
| `intents[]` | array | `{type, damage?, hits?}` -- damage is **per hit** |
| `powers[]` | array | `{id, name, amount, type, stack_type, description}` |

### Sub-objects

- **Relic**: `{id, name, description, rarity}`
- **Potion**: `{slot, id, name, description, rarity, usage, target_type}`
- **Power**: `{id, name, amount, type, stack_type, description}`
- **Reward Item**: `{index, type, description, gold_amount?, potion_id?, potion_name?, potion_rarity?, relic_id?, relic_name?, relic_description?, relic_rarity?, card_choices[]?, card_id?, card_name?}` -- type is `Gold`, `Potion`, `Relic`, `Card`, `SpecialCard`, `CardRemoval`
- **Card Choice** (in reward): `{index, id, name, description, type, rarity, cost, is_upgraded}`
- **Event Option**: `{index, title, description, is_locked, is_proceed, relic_id?}`
- **Selectable Card** (tri select / grid select): `{index, card_id, card_name, description, card_type, cost}`
- **Hand Select Card**: `{index, card_id, card_name, card_type, cost, description}` -- card in hand selection mode
- **Character Option**: `{character_id, character_name, is_locked, is_selected}`
- **Map Node**: `{col, row, type, state, children[], parents[]}` -- type: `MONSTER|ELITE|BOSS|SHOP|REST_SITE|TREASURE|ANCIENT|UNKNOWN`; state: `TRAVELABLE|TRAVELED|UNTRAVELABLE|NONE`; children/parents are `[{col, row}]`
- **Rest Site Option**: `{index, option_id, name, description, is_enabled}` -- option_id: `HEAL`, `SMITH`, `MEND`, `LIFT`, `DIG`, `HATCH`, `COOK`, `CLONE`
- **Treasure Relic**: `{index, id, name, description, rarity}` -- relic available from an opened treasure chest
- **Shop Card**: `{index, card_id, card_name, description, card_type, rarity, cost, is_on_sale, is_stocked}` -- card for sale in shop
- **Shop Relic**: `{index, relic_id, relic_name, description, rarity, cost, is_stocked}` -- relic for sale in shop
- **Shop Potion**: `{index, potion_id, potion_name, description, rarity, cost, is_stocked}` -- potion for sale in shop
- **Shop Card Removal**: `{cost, is_used}` -- card removal service info
- **Selectable Relic** (relic select): `{index, id, name, description, rarity}` -- relic available on boss/event relic choice screen
- **Bundle**: `{index, card_count, cards[]}` -- a bundle of cards in the bundle selection screen; `cards[]` uses the same selectable card format
- **Crystal Sphere Cell**: `{x, y, is_hidden, is_clickable, item_type?, is_good?}` -- item_type and is_good only populated for revealed (non-hidden) cells with items
- **Crystal Sphere Revealed Item**: `{item_type, is_good, x, y, width, height}` -- fully revealed items (all occupied cells cleared); item_type is class name (e.g., "CrystalSphereRelic", "CrystalSphereGold")

### Action Results

`play_card`, `end_turn`, `use_potion` responses include `data.results[]`:

| Type | Fields |
|------|--------|
| `damage` | `target_id`, `target_name`, `damage`, `blocked`, `hp_loss`, `killed` |
| `block` | `target_id`, `target_name`, `amount` |
| `power` | `target_id`, `target_name`, `power_id`, `amount` |
| `potion_used` | `target_id`, `target_name`, `potion_id` |

## Card Keywords Reference

Cards can have the following keywords. These are returned as strings in the `keywords` array.

| Keyword | Description |
|---------|-------------|
| `Exhaust` | Card is removed from combat when played (sent to exhaust pile) |
| `Ethereal` | Card is exhausted if not played by end of turn |
| `Innate` | Card is added to the starting hand at the beginning of combat |
| `Retain` | Card is not discarded at end of turn |
| `Sly` | Card's cost is reduced by 1 when drawn (cost returns to normal at end of turn) |
| `Eternal` | Card cannot be removed from the deck (card removal services) |
| `Unplayable` | Card cannot be played from hand |

## Key Notes

- `damage`/`block` on cards are **preview values** with all modifiers applied.
- `intents[].damage` is **per hit**; total = `damage * hits`.
- Null/empty fields are **omitted** from JSON.
- After `end_turn`, always read the response for enemy action results.
- On any error, run `state` to refresh before retrying.

## Error Codes

**General** (may apply to any command):

| Error | Cause |
|-------|-------|
| `CONNECTION_ERROR` | Game not running or mod not loaded |
| `INVALID_REQUEST` | Failed to parse request |
| `UNKNOWN_COMMAND` | Command not recognized |
| `MISSING_ARGUMENT` | Required argument not provided |
| `INTERNAL_ERROR` | Unexpected internal error |
| `UI_NOT_FOUND` | Required UI element not found |
| `TIMEOUT` | Action did not complete within time limit |

**Main Menu** (`continue_run`, `new_run`, `abandon_run`, `choose_game_mode`):

| Error | Cause |
|-------|-------|
| `NOT_ON_MENU` | Not on main menu screen |
| `NO_SAVED_RUN` | No saved run exists (continue/abandon) |
| `RUN_SAVE_EXISTS` | Saved run exists, must abandon first (new_run) |
| `NOT_ON_SINGLEPLAYER_SUBMENU` | Not on singleplayer submenu (choose_game_mode) |
| `INVALID_GAME_MODE` | Mode not one of standard, daily, custom |
| `MODE_NOT_UNLOCKED` | Game mode not yet unlocked |
| `BUTTON_NOT_FOUND` | UI button not found |
| `BUTTON_DISABLED` | UI button is disabled |
| `ACTION_FAILED` | Failed to execute menu action |

**Character Select** (`select_character`, `set_ascension`, `embark`):

| Error | Cause |
|-------|-------|
| `NOT_IN_CHARACTER_SELECT` | Not on character select screen |
| `CHARACTER_NOT_FOUND` | Character ID not found |
| `CHARACTER_LOCKED` | Character not unlocked |
| `INVALID_ASCENSION_LEVEL` | Level out of range (0 to max) |
| `NO_CHARACTER_SELECTED` | Embark without selecting character |
| `EMBARK_BUTTON_NOT_FOUND` | Embark button not found |
| `EMBARK_NOT_AVAILABLE` | Embark button disabled |

**Map** (`choose_map_node`):

| Error | Cause |
|-------|-------|
| `NOT_ON_MAP` | Map not open |
| `NO_RUN_STATE` | No active run |
| `NODE_NOT_FOUND` | Invalid coordinates |
| `NOT_TRAVELABLE` | Node not reachable |
| `TRAVEL_DISABLED` | Animation in progress |

**Combat** (`play_card`, `use_potion`, `end_turn`):

| Error | Cause |
|-------|-------|
| `NOT_IN_COMBAT` | Combat not in progress |
| `COMBAT_ENDING` | Combat is over or ending |
| `NOT_PLAYER_TURN` | Not in play phase |
| `ACTIONS_DISABLED` | Player actions temporarily disabled |
| `NO_PLAYER` | Player not found or not in combat |
| `PLAYER_DEAD` | Player is dead |
| `CARD_NOT_FOUND` | Card ID not in hand |
| `INVALID_CARD_INDEX` | nth out of range for duplicate cards |
| `CANNOT_PLAY_CARD` | Card not playable (energy, blocked, etc.) |
| `TARGET_REQUIRED` | Card/potion requires a target but none provided |
| `TARGET_NOT_FOUND` | Enemy died or wrong combat_id |
| `TARGET_NOT_ALLOWED` | Item does not accept a target (self/AoE/non-combat) |
| `POTION_NOT_FOUND` | Potion ID not in belt |
| `INVALID_POTION_SLOT` | nth out of range for duplicate potions |
| `POTION_ALREADY_QUEUED` | Potion already queued for use |
| `POTION_NOT_USABLE` | Potion fails custom usability check |
| `NOT_IN_RUN` | No active run (AnyTime potion outside combat) |
| `AUTOMATIC_POTION` | Potion is automatic, cannot be used manually |
| `ACTION_CANCELLED` | Action was cancelled by the game |

**Hand Select** (`hand_select_card`, `hand_confirm_selection`):

| Error | Cause |
|-------|-------|
| `NOT_IN_HAND_SELECT` | Not in hand card selection mode |
| `INVALID_SELECTION_COUNT` | Would exceed max selection count |
| `CARD_NOT_FOUND` | Card ID not in selectable hand |
| `CANNOT_CONFIRM` | Not enough cards selected to confirm |

**Grid Card Select** (`grid_select_card`, `grid_select_skip`):

| Error | Cause |
|-------|-------|
| `NOT_IN_GRID_CARD_SELECT` | Not on grid selection screen |
| `INVALID_SELECTION_COUNT` | Wrong number of cards selected |
| `CARD_NOT_FOUND` | Card ID not in selection |
| `CANNOT_SKIP` | Selection is not cancelable |

**Event** (`choose_event`, `advance_dialogue`):

| Error | Cause |
|-------|-------|
| `NOT_IN_EVENT` | Not currently in an event |
| `NO_EVENT_LAYOUT` | Event layout not found |
| `INVALID_OPTION_INDEX` | Option index out of range |
| `OPTION_LOCKED` | Option is locked and cannot be selected |
| `OPTION_BUTTON_NOT_FOUND` | Option button not found in UI |
| `EVENT_TIMEOUT` | Event state did not change within timeout |
| `NOT_ANCIENT_EVENT` | `advance_dialogue` on non-Ancient event |
| `NOT_IN_DIALOGUE` | Dialogue already finished |
| `DIALOGUE_HITBOX_NOT_FOUND` | Dialogue hitbox not found |

**Rest Site** (`choose_rest_option`):

| Error | Cause |
|-------|-------|
| `NOT_AT_REST_SITE` | Not at a rest site |
| `OPTION_NOT_FOUND` | Option ID not available |
| `OPTION_DISABLED` | Option is disabled (e.g., SMITH with no upgradable cards) |

**Treasure Room** (`open_chest`, `pick_relic`):

| Error | Cause |
|-------|-------|
| `NOT_IN_TREASURE_ROOM` | Not in a treasure room |
| `CHEST_ALREADY_OPENED` | Chest already opened |
| `NO_RELICS_AVAILABLE` | No relics to pick (not opened or already picked) |
| `INVALID_RELIC_INDEX` | Relic index out of range |

**Shop** (`shop_buy_card`, `shop_buy_relic`, `shop_buy_potion`, `shop_remove_card`):

| Error | Cause |
|-------|-------|
| `NOT_IN_SHOP` | Not in a merchant room |
| `ITEM_NOT_FOUND` | Item ID not found in shop |
| `ITEM_SOLD_OUT` | Item already purchased |
| `NOT_ENOUGH_GOLD` | Insufficient gold |
| `POTION_BELT_FULL` | No empty potion slots |
| `CARD_REMOVAL_USED` | Card removal already used this visit |
| `PURCHASE_FAILED` | Unknown purchase failure |

**Reward** (`reward_claim`, `reward_choose_card`, `reward_skip_card`):

| Error | Cause |
|-------|-------|
| `NOT_ON_REWARD_SCREEN` | Not on reward screen |
| `REWARD_NOT_FOUND` | No matching reward |
| `AMBIGUOUS_REWARD` | nth!=0 but only one item |
| `INVALID_REWARD_INDEX` | nth out of range |
| `INVALID_REWARD_TYPE` | choose_card/skip_card only supports card type |
| `USE_CHOOSE_CARD` | Used `reward_claim` on card reward |
| `CARD_NOT_FOUND` | Card ID not found in card reward |
| `POTION_BELT_FULL` | No empty potion slots |
| `NOT_SUPPORTED` | Unsupported reward type |
| `CLAIM_FAILED` | Unknown failure |

**Proceed** (`proceed`):

| Error | Cause |
|-------|-------|
| `NO_PROCEED_AVAILABLE` | Not on a screen that supports proceed |
| `PROCEED_BUTTON_NOT_FOUND` | Proceed button not found |
| `PROCEED_NOT_VISIBLE` | Proceed button is not visible |
| `PROCEED_NOT_ENABLED` | Proceed button is not enabled |
| `EVENT_NOT_FINISHED` | Event not finished, use choose_event first |

**Tri Select** (`tri_select_card`, `tri_select_skip`):

| Error | Cause |
|-------|-------|
| `NOT_IN_TRI_SELECT` | Not on selection screen |
| `CANNOT_SKIP` | Selection cannot be skipped |
| `SKIP_BUTTON_NOT_FOUND` | Skip button unavailable |
| `INVALID_SELECTION_COUNT` | Wrong number of cards |
| `DUPLICATE_SELECTION` | Same card selected twice |

**Relic Select** (`relic_select`, `relic_skip`):

| Error | Cause |
|-------|-------|
| `NOT_IN_RELIC_SELECT` | Not on relic selection screen |
| `NO_RELICS_AVAILABLE` | No relics in selection |
| `INVALID_RELIC_INDEX` | Relic index out of range |
| `SKIP_BUTTON_NOT_FOUND` | Skip button unavailable |

**Bundle Select** (`bundle_select`, `bundle_confirm`, `bundle_cancel`):

| Error | Cause |
|-------|-------|
| `NOT_IN_BUNDLE_SELECT` | Not on bundle selection screen |
| `PREVIEW_ALREADY_OPEN` | Bundle preview already showing (confirm or cancel first) |
| `NO_BUNDLES_AVAILABLE` | No bundles in selection |
| `INVALID_BUNDLE_INDEX` | Bundle index out of range |
| `CANNOT_CONFIRM` | Confirm button disabled (no bundle previewed) |
| `CANNOT_CANCEL` | Cancel button disabled (no preview open) |

**Crystal Sphere** (`crystal_set_tool`, `crystal_click_cell`, `crystal_proceed`):

| Error | Cause |
|-------|-------|
| `NOT_IN_CRYSTAL_SPHERE` | Not in Crystal Sphere mini-game |
| `INVALID_TOOL` | Tool must be "big" or "small" |
| `TOOL_NOT_AVAILABLE` | Tool button not visible (minigame finished) |
| `TOOL_ALREADY_ACTIVE` | Requested tool is already selected |
| `MINIGAME_FINISHED` | All divinations used (use `crystal_proceed`) |
| `CELL_NOT_FOUND` | Invalid cell coordinates |
| `CELL_NOT_CLICKABLE` | Cell is already cleared or not visible |
| `CANNOT_PROCEED` | Proceed button not enabled (divinations remaining) |

**Game Over** (`return_to_menu`):

| Error | Cause |
|-------|-------|
| `NOT_ON_GAME_OVER_SCREEN` | Not on game over screen |
| `BUTTON_NOT_FOUND` | Main menu button not found |
| `BUTTON_DISABLED` | Main menu button is disabled |
| `ACTION_FAILED` | Failed to initiate return to menu |

**Bug Report** (`report_bug`):

| Error | Cause |
|-------|-------|
| `WRITE_FAILED` | Cannot create bug report directory or write file |
