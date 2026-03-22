# STS2 CLI Reference

Command-line interface for interacting with Slay the Spire 2 via the STS2-Cli-Mod.

## Commands

### ping

```
./sts2 ping
```

Test connection to the mod. Returns `{"ok": true}` on success.

### state

```
./sts2 state
```

Get current game state. Returns screen type, combat details, player info, hand, enemies, and rewards.

### play_card

```
./sts2 play_card <card_id> [--nth <n>] [--target <combat_id>]
```

Play a card from hand by **card ID**.

- **card_id**: Card identifier (e.g., `STRIKE_IRONCLAD`)
- **--nth**: N-th occurrence when multiple copies exist (0-based, optional, defaults to 0)
- **--target**: Required for cards with enemy-targeting target type. Omit for self-targeting or area cards.

Examples:
```bash
./sts2 play_card STRIKE_IRONCLAD                    # Play first Strike
./sts2 play_card STRIKE_IRONCLAD --nth 1            # Play second Strike if multiple exist
./sts2 play_card STRIKE_IRONCLAD --target 123       # Strike specific enemy
```

### use_potion

```
./sts2 use_potion <potion_id> [--nth <n>] [--target <combat_id>]
```

Use a potion by **potion ID**.

- **potion_id**: Potion identifier (e.g., `FIRE_POTION`)
- **--nth**: N-th occurrence when multiple copies exist (0-based, optional, defaults to 0)
- **--target**: Required for enemy-targeting potions

Examples:
```bash
./sts2 use_potion FIRE_POTION                       # Use first Fire Potion
./sts2 use_potion FIRE_POTION --nth 1               # Use second Fire Potion if multiple exist
./sts2 use_potion FIRE_POTION --target 456          # Use on specific enemy
```

### end_turn

```
./sts2 end_turn
```

End the current turn. The response contains all enemy action results (damage dealt, buffs applied, etc.).

### claim_reward

```
./sts2 claim_reward --type <type> [--id <id>] [--nth <n>]
```

Claim a non-card reward by **type and optional ID**.

- **--type**: Reward type - `gold`, `potion`, `relic`, `special_card`
- **--id**: Item ID (required for `potion`, `relic`, `special_card`; optional for `gold`)
- **--nth**: N-th occurrence when multiple rewards of same type exist (0-based, optional, defaults to 0)

Examples:
```bash
./sts2 claim_reward --type gold                                   # Claim gold reward
./sts2 claim_reward --type potion --id FIRE_POTION               # Claim Fire Potion
./sts2 claim_reward --type relic --id BURNING_BLOOD              # Claim specific relic
./sts2 claim_reward --type potion --id FIRE_POTION --nth 1       # Claim 2nd Fire Potion
```

For card rewards, use `choose_card` instead (returns `USE_CHOOSE_CARD` error if attempted on a card reward).

### choose_card

```
./sts2 choose_card --type card --card_id <card_id> [--nth <n>]
```

Pick a specific card from a card reward by **card ID**.

- **--type**: Must be `card`
- **--card_id**: Card identifier to select (e.g., `STRIKE_IRONCLAD`)
- **--nth**: N-th card reward when multiple exist (0-based, optional, defaults to 0)

Example:
```bash
./sts2 choose_card --type card --card_id STRIKE_IRONCLAD          # Select from 1st card reward
./sts2 choose_card --type card --card_id STRIKE_IRONCLAD --nth 1  # Select from 2nd card reward
```

### skip_card

```
./sts2 skip_card --type card [--nth <n>]
```

Skip a card reward — take nothing.

- **--type**: Must be `card`
- **--nth**: N-th card reward when multiple exist (0-based, optional, defaults to 0)

Example:
```bash
./sts2 skip_card --type card              # Skip 1st card reward
./sts2 skip_card --type card --nth 1      # Skip 2nd card reward
```

### choose_event

```
./sts2 choose_event <index>
```

Choose an option in an event room by **0-based index**.

- **index**: Option index in the event's options list (0-based)

Examples:
```bash
./sts2 choose_event 0                    # Choose first option
./sts2 choose_event 1                    # Choose second option
```

For Ancient events, use `advance_dialogue` first if `is_in_dialogue` is true.

### advance_dialogue

```
./sts2 advance_dialogue [--auto]
```

Advance dialogue in an **Ancient event**.

Ancient events have a dialogue phase before options appear. Use this command to click through the dialogue.

- **--auto**: Automatically advance all dialogue lines until options appear

Examples:
```bash
./sts2 advance_dialogue                   # Advance one dialogue line
./sts2 advance_dialogue --auto           # Auto-advance to options
```

**Workflow for Ancient events**:
1. Check `sts2 state` - if `layout_type` is "Ancient" and `is_in_dialogue` is true
2. Run `advance_dialogue --auto` to skip to options
3. Then use `choose_event <index>` to select an option

### proceed

```
./sts2 proceed
```

Leave the reward screen and proceed to the map. Any unclaimed rewards are automatically skipped.

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Connection error (game not running, mod not loaded) |
| 2 | Invalid game state (not in combat, not on reward screen, combat ending, not player turn) |
| 3 | Invalid parameter (bad ID, missing target, unknown command, ID mismatch) |
| 4 | Timeout (action did not complete in time) |
| 5 | State changed (concurrent modification) |

## Response Format

**Success:**

```json
{"ok": true, "data": { ... }}
```

**Error:**

```json
{"ok": false, "error": "ERROR_CODE", "message": "Human-readable description"}
```

## Game State Structure

Returned by `./sts2 state` in the `data` field.

```
data
├── screen              # "COMBAT", "REWARD", "CARD_REWARD", "MAP", "MENU", "UNKNOWN"
├── timestamp           # Unix timestamp (ms)
├── combat              # null when not in combat
│   ├── encounter       # encounter ID (e.g., "jaw_worm")
│   ├── turn_number     # 1-indexed
│   ├── is_player_turn
│   ├── is_player_actions_disabled
│   ├── is_combat_ending
│   ├── player
│   │   ├── character_id, character_name
│   │   ├── hp, max_hp, block, energy, max_energy
│   │   ├── gold, deck_count
│   │   ├── hand_count, draw_count, discard_count, exhaust_count
│   │   ├── relics[]
│   │   ├── potions[]
│   │   ├── powers[]
│   │   ├── pets[]?          # Necrobinder only
│   │   ├── orbs[]?          # Defect only
│   │   ├── orb_slots?       # Defect only
│   │   └── stars?           # Regent only
│   ├── hand[]
│   └── enemies[]
└── rewards             # null when not on reward screen
    └── rewards[]       # Array of reward items
```

### Player Fields

| Field | Type | Description |
|-------|------|-------------|
| `character_id` | string | e.g., `"ironclad"` |
| `hp`, `max_hp` | int | Current and maximum health |
| `block` | int | Current block (damage reduction) |
| `energy`, `max_energy` | int | Current and max energy for this turn |
| `gold` | int | Gold count |
| `deck_count` | int | Total cards in deck |
| `hand_count` | int | Cards currently in hand |
| `draw_count` | int | Cards in draw pile |
| `discard_count` | int | Cards in discard pile |
| `exhaust_count` | int | Cards exhausted this combat |

### Relic Object

```json
{"id": "string", "name": "string", "description": "string", "rarity": "string"}
```

### Potion Object

```json
{"slot": 0, "id": "string", "name": "string", "description": "string", "rarity": "string", "usage": "string", "target_type": "string"}
```

### Power Object

```json
{"id": "string", "name": "string", "amount": 0, "type": "string", "stack_type": "string", "description": "string"}
```

### Card Object (in hand)

| Field | Type | Description |
|-------|------|-------------|
| `index` | int | 0-based position in hand (may shift after plays) |
| `id` | string | Card identifier |
| `name` | string | Display name |
| `description` | string | Card effect text |
| `type` | string | `Attack`, `Skill`, `Power`, `Status`, `Curse` |
| `rarity` | string | `Basic`, `Common`, `Uncommon`, `Rare`, `Ancient`, `Event`, `Token`, `Status`, `Curse` |
| `target_type` | string | `None`, `Self`, `AnyEnemy`, `AllEnemies`, `RandomEnemy`, `AnyAlly`, etc. |
| `cost` | int | Energy cost (-1 for X-cost) |
| `star_cost` | int? | Star cost (Regent only, -1 for X-star) |
| `keywords` | string[] | Keywords like `Exhaust`, `Ethereal`, `Innate`, `Retain`, `Sly` |
| `tags` | string[] | Additional tags like `Strike`, `Defend` |
| `damage` | int? | Preview damage (after all modifiers) |
| `block` | int? | Preview block (after all modifiers) |
| `enchantment` | string? | Enchantment model ID |
| `affliction` | string? | Affliction model ID |
| `is_upgraded` | bool | Whether the card is upgraded |
| `can_play` | bool | Whether the card can be played right now |
| `unplayable_reason` | string? | Why the card can't be played |

### Enemy Object

| Field | Type | Description |
|-------|------|-------------|
| `combat_id` | uint | **Stable** target ID for `--target` parameter (does not change when enemies die) |
| `id` | string | Enemy type identifier |
| `name` | string | Display name |
| `hp`, `max_hp` | int | Current and maximum health |
| `block` | int | Current block |
| `is_alive` | bool | Whether enemy is alive |
| `is_minion` | bool | Whether enemy is a summoned minion |
| `move_id` | string | Current move identifier |
| `intents` | array | Current turn intents |
| `powers` | array | Active powers/buffs/debuffs |

### Intent Object

| Field | Type | Description |
|-------|------|-------------|
| `type` | string | `Attack`, `Defend`, `Buff`, `Debuff`, `Unknown`, etc. |
| `damage` | int? | Damage **per hit** |
| `hits` | int? | Number of hits (total damage = damage * hits) |

## Reward State Structure

Returned by `./sts2 state` in the `data.rewards` field when screen is `REWARD`.

### Reward Item Object

| Field | Type | Description |
|-------|------|-------------|
| `index` | int | 0-based position in reward list (legacy, may shift after claims) |
| `type` | string | `Gold`, `Potion`, `Relic`, `Card`, `SpecialCard`, `CardRemoval` |
| `description` | string | Localized description |
| `gold_amount` | int? | Gold rewards only: amount of gold |
| `potion_id` | string? | Potion rewards only: potion identifier |
| `potion_name` | string? | Potion rewards only: display name |
| `potion_rarity` | string? | Potion rewards only: rarity |
| `relic_id` | string? | Relic rewards only: relic identifier |
| `relic_name` | string? | Relic rewards only: display name |
| `relic_description` | string? | Relic rewards only: effect description |
| `relic_rarity` | string? | Relic rewards only: rarity |
| `card_choices` | array? | Card rewards only: array of card choices (typically 3) |
| `card_id` | string? | SpecialCard rewards only: card identifier |
| `card_name` | string? | SpecialCard rewards only: display name |

### Card Choice Object (within card rewards)

| Field | Type | Description |
|-------|------|-------------|
| `index` | int | 0-based position in card choices (legacy) |
| `id` | string | Card identifier (use with `choose_card --card_id`) |
| `name` | string | Display name |
| `description` | string | Card effect text |
| `type` | string | `Attack`, `Skill`, `Power`, `Status`, `Curse` |
| `rarity` | string | `Common`, `Uncommon`, `Rare` |
| `cost` | int | Energy cost |
| `is_upgraded` | bool | Whether the card is upgraded |

### JSON Example (Reward Screen)

```json
{
  "ok": true,
  "data": {
    "screen": "REWARD",
    "timestamp": 1711123456789,
    "rewards": {
      "rewards": [
        {"index": 0, "type": "Gold", "description": "25 Gold", "gold_amount": 25},
        {"index": 1, "type": "Potion", "description": "Fire Potion",
         "potion_id": "FIRE_POTION", "potion_name": "Fire Potion", "potion_rarity": "Common"},
        {"index": 2, "type": "Card", "description": "Add a card to your deck",
         "card_choices": [
           {"index": 0, "id": "INFLAME", "name": "Inflame", "description": "Gain 2 Strength.",
            "type": "Power", "rarity": "Uncommon", "cost": 1, "is_upgraded": false},
           {"index": 1, "id": "SHRUG_IT_OFF", "name": "Shrug It Off",
            "description": "Gain 8 Block. Draw 1 card.",
            "type": "Skill", "rarity": "Common", "cost": 1, "is_upgraded": false},
           {"index": 2, "id": "ANGER", "name": "Anger",
            "description": "Deal 6 damage. Add a copy to discard.",
            "type": "Attack", "rarity": "Common", "cost": 0, "is_upgraded": false}
         ]}
      ]
    }
  }
}
```

### Event State Structure

Returned by `./sts2 state` in the `data.event` field when screen is `EVENT`.

| Field | Type | Description |
|-------|------|-------------|
| `event_id` | string | Event identifier (e.g., "BIG_FISH") |
| `title` | string | Localized event title |
| `description` | string | Localized event description (current page) |
| `layout_type` | string | "Default", "Combat", "Ancient", "Custom" |
| `is_finished` | bool | Whether the event has concluded |
| `is_in_dialogue` | bool | **Ancient events only**: True when dialogue is in progress |
| `current_dialogue_line` | int | **Ancient events only**: Current dialogue line index (0-based) |
| `total_dialogue_lines` | int | **Ancient events only**: Total number of dialogue lines |
| `options` | array | Current available options (empty during Ancient dialogue) |

### Event Option Object

| Field | Type | Description |
|-------|------|-------------|
| `index` | int | 0-based index for `choose_event` |
| `title` | string | Localized option text |
| `description` | string | Localized option description/tooltip |
| `is_locked` | bool | Cannot be selected (OnChosen == null) |
| `is_proceed` | bool | Final "proceed to map" option |
| `relic_id` | string | Relic ID if option shows a relic |

### JSON Example (Event Screen)

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
      "is_in_dialogue": false,
      "options": [
        {
          "index": 0,
          "title": "[Eat] Heal 5 HP.",
          "description": "Heal 5 HP.",
          "is_locked": false,
          "is_proceed": false
        }
      ]
    }
  }
}
```

### JSON Example (Ancient Event in Dialogue)

```json
{
  "ok": true,
  "data": {
    "screen": "EVENT",
    "event": {
      "event_id": "ANCIENT_EXAMPLE",
      "title": "Ancient One",
      "layout_type": "Ancient",
      "is_in_dialogue": true,
      "current_dialogue_line": 0,
      "total_dialogue_lines": 3,
      "options": []
    }
  }
}
```

## Action Results

After `play_card`, `end_turn`, and `use_potion`, the response `data` includes a `results` array:

| Type | Fields |
|------|--------|
| `damage` | `target_id`, `target_name`, `damage`, `blocked`, `hp_loss`, `killed` |
| `block` | `target_id`, `target_name`, `amount` |
| `power` | `target_id`, `target_name`, `power_id`, `amount` |
| `potion_used` | `target_id`, `target_name`, `potion_id` |

## Key Notes

- **Use IDs, not indices**: All commands now use stable IDs (`card_id`, `potion_id`, `relic_id`) instead of shifting indices. This prevents errors when the game state changes between command issuance and execution.
- **--nth parameter**: Only specify `--nth` when multiple items with the same ID exist. If only one item exists but `--nth` is specified with a non-zero value, an `AMBIGUOUS_REWARD` error is returned.
- **combat_id** on enemies is **stable** across the entire combat -- use it for `--target`. It does not change when other enemies die.
- Null/empty fields are **omitted** from JSON (not serialized as `null`).
- `damage` and `block` on cards are **preview values** after all modifiers (strength, vulnerable, etc.).
- `intents[].damage` is **per-hit**; multiply by `intents[].hits` for total incoming damage.
- After `end_turn`, the response contains all enemy actions -- always read it.
- **Ancient events**: Use `is_in_dialogue` field to detect dialogue phase. Run `advance_dialogue --auto` before `choose_event`.
- **Event workflow**: For Ancient events: check `is_in_dialogue` → `advance_dialogue --auto` → wait → `choose_event`.

## Error Handling

| Error | Cause | Recovery |
|-------|-------|----------|
| `TARGET_NOT_FOUND` | Enemy died or wrong combat_id | Run `./sts2 state` to get alive enemies |
| `CARD_NOT_FOUND` | Card ID not in hand | Run `./sts2 state` to get current hand |
| `CANNOT_PLAY_CARD` | Not enough energy or blocked by effect | Skip this card |
| `NOT_IN_COMBAT` | Combat ended | Stop the combat loop |
| `COMBAT_ENDING` | Combat is resolving | Stop playing, wait for resolution |
| `NOT_ON_REWARD_SCREEN` | Not on the reward screen | Run `./sts2 state` to check current screen |
| `REWARD_NOT_FOUND` | No reward matching type/ID found | Run `./sts2 state` to get available rewards |
| `AMBIGUOUS_REWARD` | Only one item but nth≠0 specified | Use nth=0 or omit --nth |
| `INVALID_REWARD_INDEX` | nth out of range for matching rewards | Check available count in error message |
| `ID_MISMATCH` | Item at position doesn't match expected ID | Run `./sts2 state` to verify current state |
| `NOT_CARD_REWARD` | Used `choose_card`/`skip_card` on non-card reward | Use `claim_reward` instead |
| `USE_CHOOSE_CARD` | Used `claim_reward` on a card reward | Use `choose_card` or `skip_card` instead |
| `POTION_BELT_FULL` | Potion reward but belt has no empty slots | Skip this reward or use a potion first |
| `NOT_SUPPORTED` | Reward type not yet supported (e.g., CardRemoval) | Skip this reward |
| `CLAIM_FAILED` | Reward claim failed for unknown reason | Run `./sts2 state` to refresh and retry |
| `NOT_ANCIENT_EVENT` | Tried `advance_dialogue` on non-Ancient event | Check `layout_type` in event state |
| `NOT_IN_DIALOGUE` | Ancient event dialogue already finished | Use `choose_event` directly |
| `CONNECTION_ERROR` | Game disconnected | Report to user and stop |

On any error, run `./sts2 state` to refresh state before continuing.
