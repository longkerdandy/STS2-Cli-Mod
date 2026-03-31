# STS2 CLI Reference

CLI for controlling Slay the Spire 2 via named pipe. All responses are JSON: `{"ok": true, "data": {...}}` on success, `{"ok": false, "error": "CODE", "message": "..."}` on error.

Exit codes: 0=success, 1=connection, 2=invalid state, 3=invalid param, 4=timeout, 5=state changed.

## Commands

All commands use stable IDs (not indices). Use `--nth N` (0-based, default 0) when multiple items share the same ID.

### ping

```
./sts2 ping
```

Test connection. Returns `{"ok": true}`.

### state

```
./sts2 state
```

Get full game state. See [Game State Structure](#game-state-structure) below.

---

### play_card

```
./sts2 play_card <card_id> [--nth <n>] [--target <combat_id>]
```

Play a card from hand. `--target` required for enemy-targeting cards only.

### use_potion

```
./sts2 use_potion <potion_id> [--nth <n>] [--target <combat_id>]
```

Use a potion. `--target` required for enemy-targeting potions. Some potions open a card selection screen (screen becomes `TRI_SELECT`) -- use `tri_select_card` or `tri_select_skip` to complete.

### end_turn

```
./sts2 end_turn
```

End turn. Response contains all enemy action results -- always read it.

---

### reward_claim

```
./sts2 reward_claim --type <type> [--id <id>] [--nth <n>]
```

Claim a non-card reward. Types: `gold`, `potion`, `relic`, `special_card`. `--id` required for all except `gold`.

### reward_choose_card / reward_skip_card

```
./sts2 reward_choose_card --type card --card_id <card_id> [--nth <n>]
./sts2 reward_skip_card --type card [--nth <n>]
```

Pick or skip a card reward. `--nth` selects which card reward when multiple exist.

### proceed

```
./sts2 proceed
```

Leave reward screen, FakeMerchant event, rest site (after choosing an option), treasure room (after picking/skipping relic), or merchant room and proceed to map.

---

### choose_event

```
./sts2 choose_event <index>
```

Choose event option by 0-based index. For Ancient events, advance dialogue first.

### advance_dialogue

```
./sts2 advance_dialogue [--auto]
```

Advance Ancient event dialogue. Use `--auto` to skip all dialogue lines until options appear. Then use `choose_event`.

---

### tri_select_card

```
./sts2 tri_select_card <card_id> [<card_id>...] [--nth <n>...]
```

Select cards from a three-choose-one card selection screen (triggered by potions, cards like Discovery/Quasar/Splash, relics like Toolbox, and monsters). Check `tri_select.min_select` / `max_select` in state for how many to pick.

### tri_select_skip

```
./sts2 tri_select_skip
```

Skip three-choose-one card selection (only if `tri_select.can_skip` is true).

---

### choose_map_node

```
./sts2 choose_map_node <col> <row>
```

Travel to a map node. Only nodes with state `TRAVELABLE` can be selected -- check `map.travelable_coords` in state.

---

### choose_rest_option

```
./sts2 choose_rest_option <option_id>
```

Choose a rest site (campfire) option by ID. Common options: `HEAL` (restore HP), `SMITH` (upgrade a card). SMITH opens a card selection overlay (screen becomes `GRID_CARD_SELECT`) -- use `grid_select_card` to complete. After choosing, use `proceed` to leave the rest site if `rest_site.can_proceed` is true.

---

### open_chest

```
./sts2 open_chest
```

Open the treasure chest in a treasure room. After opening, relics are revealed -- use `pick_relic` to pick one, or `proceed` to skip.

### pick_relic

```
./sts2 pick_relic <index>
```

Pick a relic from an opened treasure chest by 0-based index. After picking, use `proceed` to leave the treasure room.

---

### relic_select / relic_skip

```
./sts2 relic_select <index>
./sts2 relic_skip
```

Select or skip a relic from the boss/event relic choice screen. Index is 0-based.

---

### bundle_select

```
./sts2 bundle_select <index>
```

Preview a bundle on the bundle selection screen (triggered by the Scroll Boxes relic). Index is 0-based. Opens a preview showing the cards in the selected bundle.

### bundle_confirm

```
./sts2 bundle_confirm
```

Confirm the currently previewed bundle. The bundle's cards are added to the deck and the selection screen closes.

### bundle_cancel

```
./sts2 bundle_cancel
```

Cancel the current bundle preview and return to bundle selection. Allows previewing a different bundle.

---

### shop_buy_card

```
./sts2 shop_buy_card <card_id> [--nth <n>]
```

Buy a card from the shop by card ID. Use `--nth` to disambiguate when multiple copies of the same card exist.

### shop_buy_relic

```
./sts2 shop_buy_relic <relic_id> [--nth <n>]
```

Buy a relic from the shop by relic ID.

### shop_buy_potion

```
./sts2 shop_buy_potion <potion_id> [--nth <n>]
```

Buy a potion from the shop by potion ID. Fails if the potion belt is full.

### shop_remove_card

```
./sts2 shop_remove_card
```

Buy the card removal service from the shop. Opens a grid card selection screen (screen becomes `GRID_CARD_SELECT`) -- use `grid_select_card` to pick a card to remove, or `grid_select_skip` to cancel. After the shop, use `proceed` to leave.

---

### select_character

```
./sts2 select_character <character_id>
```

Select a character on the character select screen. ID matching is case-insensitive.

### set_ascension

```
./sts2 set_ascension <level>
```

Set ascension level (0 to max) on the character select screen. Max level is read from the game (typically 20).

### embark

```
./sts2 embark
```

Start a run from the character select screen. Requires a character to be selected first.

---

### hand_select_card

```
./sts2 hand_select_card <card_id> [<card_id>...] [--nth <n>...]
```

Select cards from hand during combat selection mode (discard, exhaust, upgrade prompts). When `MinSelect == MaxSelect`, selection auto-completes after selecting the required number of cards. Otherwise, use `hand_confirm_selection` to finalize.

### hand_confirm_selection

```
./sts2 hand_confirm_selection
```

Confirm the current hand card selection. Only needed when `hand_select.require_manual_confirmation` is true (i.e., `MinSelect != MaxSelect`).

---

### grid_select_card / grid_select_skip

```
./sts2 grid_select_card <card_id> [<card_id>...] [--nth <n>...]
./sts2 grid_select_skip
```

Select or skip cards from a grid-style card selection screen (card removal, upgrade, transform, enchant, combat grid overlays). Check `grid_card_select.min_select` / `max_select` in state for how many to pick. Skip only works when `grid_card_select.cancelable` is true.

## Game State Structure

Returned by `state` in the `data` field. Only the relevant screen's data is populated; others are null/omitted.

```
data
├── screen              # COMBAT | HAND_SELECT | REWARD | CARD_REWARD | EVENT | TRI_SELECT
│                       # MAP | CHARACTER_SELECT | GRID_CARD_SELECT | REST_SITE | TREASURE | SHOP
│                       # RELIC_SELECT | BUNDLE_SELECT | MENU | UNKNOWN
├── timestamp           # Unix ms
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
│   └── enemies[]
├── rewards
│   └── rewards[]
├── event
│   ├── event_id, title, description, layout_type
│   ├── is_finished, is_in_dialogue
│   ├── current_dialogue_line, total_dialogue_lines  # Ancient only
│   └── options[]
├── tri_select
│   ├── selection_type, min_select, max_select, can_skip
│   └── cards[]
├── character_select
│   ├── available_characters[]  # {character_id, character_name, is_locked, is_selected}
│   ├── selected_character      # string?, null if none selected
│   ├── current_ascension, max_ascension
│   └── can_embark
├── grid_card_select
│   ├── selection_type          # remove, upgrade, transform, enchant, generic, unknown
│   ├── prompt, min_select, max_select, cancelable
│   └── cards[]
├── hand_select                 # Only when screen is HAND_SELECT (combat sub-state)
│   ├── mode                    # SimpleSelect or UpgradeSelect
│   ├── prompt                  # e.g., "Choose 1 card to discard."
│   ├── min_select, max_select
│   ├── cancelable, require_manual_confirmation
│   ├── can_confirm, selected_count
│   ├── selectable_cards[]      # Cards available to select from hand
│   └── selected_cards[]        # Cards already selected
└── map
    ├── act_index, act_floor, total_floor
    ├── columns, rows
    ├── current_coord       # {col, row}, null at start
    ├── nodes[]
    └── travelable_coords[] # [{col, row}]
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
├── relic_select
│   ├── relics[]            # [{index, id, name, description, rarity}]
│   └── can_skip            # bool, true if selection can be skipped
├── bundle_select
│   ├── bundles[]           # [{index, card_count, cards[]}] -- cards are selectable cards
│   ├── preview_showing     # bool, true when a bundle preview is open
│   ├── preview_cards[]     # [{index, card_id, card_name, description, card_type, cost}] -- shown during preview
│   ├── can_confirm         # bool, true when confirm button is enabled
│   └── can_cancel          # bool, true when cancel button is enabled
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
- **Selectable Card** (tri select): `{index, card_id, card_name, description, card_type, cost}`
- **Selectable Card** (grid selection): `{index, card_id, card_name, description, card_type, cost}` -- same structure as tri select
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

### Action Results

`play_card`, `end_turn`, `use_potion` responses include `data.results[]`:

| Type | Fields |
|------|--------|
| `damage` | `target_id`, `target_name`, `damage`, `blocked`, `hp_loss`, `killed` |
| `block` | `target_id`, `target_name`, `amount` |
| `power` | `target_id`, `target_name`, `power_id`, `amount` |
| `potion_used` | `target_id`, `target_name`, `potion_id` |

## Key Notes

- `damage`/`block` on cards are **preview values** with all modifiers applied.
- `intents[].damage` is **per hit**; total = `damage * hits`.
- Null/empty fields are **omitted** from JSON.
- After `end_turn`, always read the response for enemy action results.
- On any error, run `state` to refresh before retrying.

## Error Codes

**Combat** (`play_card`, `use_potion`, `end_turn`):

| Error | Cause |
|-------|-------|
| `NOT_IN_COMBAT` | Combat ended |
| `COMBAT_ENDING` | Combat resolving |
| `CARD_NOT_FOUND` | Card ID not in hand |
| `CANNOT_PLAY_CARD` | Not enough energy or blocked |
| `TARGET_NOT_FOUND` | Enemy died or wrong combat_id |

**Reward** (`reward_claim`, `reward_choose_card`, `reward_skip_card`):

| Error | Cause |
|-------|-------|
| `NOT_ON_REWARD_SCREEN` | Not on reward screen |
| `REWARD_NOT_FOUND` | No matching reward |
| `AMBIGUOUS_REWARD` | nth!=0 but only one item |
| `INVALID_REWARD_INDEX` | nth out of range |
| `ID_MISMATCH` | Item doesn't match expected ID |
| `NOT_CARD_REWARD` | Used card command on non-card reward |
| `USE_CHOOSE_CARD` | Used `reward_claim` on card reward |
| `POTION_BELT_FULL` | No empty potion slots |
| `NOT_SUPPORTED` | Unsupported reward type |
| `CLAIM_FAILED` | Unknown failure |

**Event** (`choose_event`, `advance_dialogue`):

| Error | Cause |
|-------|-------|
| `NOT_ANCIENT_EVENT` | `advance_dialogue` on non-Ancient event |
| `NOT_IN_DIALOGUE` | Dialogue already finished |

**Tri Select** (`tri_select_card`, `tri_select_skip`):

| Error | Cause |
|-------|-------|
| `NOT_IN_TRI_SELECT` | Not on selection screen |
| `CANNOT_SKIP` | Selection cannot be skipped |
| `SKIP_BUTTON_NOT_FOUND` | Skip button unavailable |
| `INVALID_SELECTION_COUNT` | Wrong number of cards |
| `DUPLICATE_SELECTION` | Same card selected twice |
| `NO_CARDS_AVAILABLE` | Empty selection screen |

**Map** (`choose_map_node`):

| Error | Cause |
|-------|-------|
| `NOT_ON_MAP` | Map not open |
| `NO_RUN_STATE` | No active run |
| `NODE_NOT_FOUND` | Invalid coordinates |
| `NOT_TRAVELABLE` | Node not reachable |
| `TRAVEL_DISABLED` | Animation in progress |

**Rest Site** (`choose_rest_option`):

| Error | Cause |
|-------|-------|
| `NOT_AT_REST_SITE` | Not at a rest site |
| `OPTION_NOT_FOUND` | Option ID not available |
| `OPTION_DISABLED` | Option is disabled (e.g., SMITH with no upgradable cards) |
| `OPTION_CANCELLED` | Option was cancelled (e.g., SMITH card selection skipped) |

**Treasure Room** (`open_chest`, `pick_relic`):

| Error | Cause |
|-------|-------|
| `NOT_IN_TREASURE_ROOM` | Not in a treasure room |
| `CHEST_ALREADY_OPENED` | Chest already opened |
| `NO_RELICS_AVAILABLE` | No relics to pick (not opened or already picked) |
| `INVALID_RELIC_INDEX` | Relic index out of range |

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

**Character Select** (`select_character`, `set_ascension`, `embark`):

| Error | Cause |
|-------|-------|
| `NOT_IN_CHARACTER_SELECT` | Not on character select screen |
| `CHARACTER_NOT_FOUND` | Character ID not found |
| `CHARACTER_LOCKED` | Character not unlocked |
| `INVALID_ASCENSION_LEVEL` | Level out of range (0 to max) |
| `NO_CHARACTER_SELECTED` | Embark without selecting character |
| `EMBARK_NOT_AVAILABLE` | Embark button disabled |

**Grid Card Select** (`grid_select_card`, `grid_select_skip`):

| Error | Cause |
|-------|-------|
| `NOT_IN_GRID_CARD_SELECT` | Not on grid selection screen |
| `INVALID_SELECTION_COUNT` | Wrong number of cards selected |
| `CARD_NOT_FOUND` | Card ID not in selection |
| `CANNOT_SKIP` | Selection is not cancelable |

**Hand Select** (`hand_select_card`, `hand_confirm_selection`):

| Error | Cause |
|-------|-------|
| `NOT_IN_HAND_SELECT` | Not in hand card selection mode |
| `INVALID_SELECTION_COUNT` | Would exceed max selection count |
| `CARD_NOT_FOUND` | Card ID not in selectable hand |
| `CANNOT_CONFIRM` | Not enough cards selected to confirm |
| `UI_NOT_FOUND` | Confirm button not found |

**General**: `CONNECTION_ERROR` -- game disconnected.
