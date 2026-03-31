# Feature Gap Analysis — STS2-Cli-Mod

> Last updated: 2026-03-25

## Overview

This document catalogs all implemented features and identifies missing capabilities
required for full game automation by an AI agent.

**Current status**: 26 CLI commands, 13 screen types detected, covering the core game loop.

---

## Implemented Features

### CLI Commands (26 total)

| # | Command | Parameters | Description |
|---|---------|------------|-------------|
| 1 | `ping` | — | Test pipe connection |
| 2 | `state` | — | Get full game state as JSON |
| 3 | `play_card` | `<card_id> [--nth <n>] [--target <combat_id>]` | Play a card from hand |
| 4 | `end_turn` | — | End current combat turn |
| 5 | `use_potion` | `<potion_id> [--nth <n>] [--target <combat_id>]` | Use a potion |
| 6 | `reward_claim` | `--type <type> [--id <id>] [--nth <n>]` | Claim a reward (gold/potion/relic/special\_card) |
| 7 | `reward_choose_card` | `--type card --card_id <card_id> [--nth <n>]` | Pick a card from card reward |
| 8 | `reward_skip_card` | `--type card [--nth <n>]` | Skip a card reward |
| 9 | `proceed` | — | Leave current screen and proceed to map |
| 10 | `choose_event` | `<index>` | Choose an event option by index |
| 11 | `advance_dialogue` | `[--auto]` | Advance Ancient event dialogue |
| 12 | `potion_select_card` | `<card_id> [<card_id>...] [--nth <n>...]` | Select cards from potion-opened selection |
| 13 | `potion_select_skip` | — | Skip potion card selection |
| 14 | `deck_select_card` | `<card_id> [<card_id>...] [--nth <n>...]` | Select cards from deck grid selection |
| 15 | `deck_select_skip` | — | Skip/cancel deck card selection |
| 16 | `select_character` | `<character_id>` | Select character on character select screen |
| 17 | `set_ascension` | `<level>` | Set ascension level |
| 18 | `embark` | — | Start the run from character select |
| 19 | `choose_map_node` | `<col> <row>` | Travel to a map node |
| 20 | `choose_rest_option` | `<option_id>` | Choose rest site option (HEAL/SMITH/etc.) |
| 21 | `open_chest` | — | Open treasure room chest |
| 22 | `pick_relic` | `<index>` | Pick relic from treasure room |
| 23 | `shop_buy_card` | `<card_id> [--nth <n>]` | Buy a card from shop |
| 24 | `shop_buy_relic` | `<relic_id> [--nth <n>]` | Buy a relic from shop |
| 25 | `shop_buy_potion` | `<potion_id> [--nth <n>]` | Buy a potion from shop |
| 26 | `shop_remove_card` | — | Buy card removal service (opens deck select) |

### Screen Detection (13 types)

| # | Screen | State DTO | Supported Commands |
|---|--------|-----------|-------------------|
| 1 | `CHARACTER_SELECT` | `CharacterSelectStateDto` | `select_character`, `set_ascension`, `embark` |
| 2 | `MENU` | — | — |
| 3 | `COMBAT` | `CombatStateDto` (player, hand, enemies, intents, powers) | `play_card`, `end_turn`, `use_potion` |
| 4 | `MAP` | `MapStateDto` (nodes with type/state/edges, travelable coords) | `choose_map_node` |
| 5 | `CARD_REWARD` | *(none — card data in parent REWARD state)* | `reward_choose_card`, `reward_skip_card` |
| 6 | `REWARD` | `RewardStateDto` | `reward_claim`, `proceed` |
| 7 | `POTION_SELECTION` | `PotionSelectionStateDto` | `potion_select_card`, `potion_select_skip` |
| 8 | `DECK_CARD_SELECT` | `DeckCardSelectStateDto` | `deck_select_card`, `deck_select_skip` |
| 9 | `EVENT` | `EventStateDto` | `choose_event`, `advance_dialogue`, `proceed` |
| 10 | `REST_SITE` | `RestSiteStateDto` | `choose_rest_option`, `proceed` |
| 11 | `TREASURE` | `TreasureStateDto` | `open_chest`, `pick_relic`, `proceed` |
| 12 | `SHOP` | `ShopStateDto` | `shop_buy_card/relic/potion`, `shop_remove_card`, `proceed` |
| 13 | `UNKNOWN` | — | — |

### State Builders (17 total)

| Builder | Extracts |
|---------|----------|
| `CardStateBuilder` | Card details (ID, name, type, rarity, cost, damage/block preview, playability) |
| `CombatStateBuilder` | Hand cards + enemy list from combat state |
| `PlayerStateBuilder` | Character info, relics, potions, gold, HP, energy, stars, orbs, deck/pile counts, pets |
| `EnemyStateBuilder` | Enemy details (combat ID, model ID, HP, block, alive, minion, move, intents, powers) |
| `IntentStateBuilder` | Intent type, damage, hits, description |
| `PowerStateBuilder` | Buff/debuff details (ID, name, amount, type, stack type) |
| `PotionStateBuilder` | Potion belt contents |
| `RelicStateBuilder` | Acquired relics with status and counter |
| `OrbStateBuilder` | Channeled orbs (Defect) |
| `PetStateBuilder` | Pet creatures (Necrobinder) |
| `MapStateBuilder` | Map grid, nodes with types/edges, travelable coordinates |
| `RewardStateBuilder` | Reward items with type-specific details |
| `EventStateBuilder` | Event ID, title, options, Ancient dialogue state |
| `DeckCardSelectStateBuilder` | Deck grid selection (type, prompt, constraints, cards) |
| `RestSiteStateBuilder` | Rest site options with enabled status |
| `TreasureStateBuilder` | Chest status, available relics |
| `ShopStateBuilder` | Shop inventory (cards/relics/potions with prices), card removal |

---

## Coverage by Game Phase

### Phase 1: Main Menu & Character Select

| Interaction | Status | Command |
|-------------|--------|---------|
| Select character | ✅ Implemented | `select_character` |
| Set ascension level | ✅ Implemented | `set_ascension` |
| Start run (embark) | ✅ Implemented | `embark` |
| Navigate main menu to character select | ❌ Missing (P2) | — |

### Phase 2: Map Navigation

| Interaction | Status | Command |
|-------------|--------|---------|
| Choose next map node | ✅ Implemented | `choose_map_node` |

Node types returned: `MONSTER`, `ELITE`, `BOSS`, `SHOP`, `REST_SITE`, `TREASURE`, `UNKNOWN`, `ANCIENT`, `UNASSIGNED`.

### Phase 3: Combat

| Interaction | Status | Command |
|-------------|--------|---------|
| Play card from hand | ✅ Implemented | `play_card` |
| Use potion | ✅ Implemented | `use_potion` |
| End turn | ✅ Implemented | `end_turn` |
| Potion-opened card selection | ✅ Implemented | `potion_select_card` / `potion_select_skip` |
| In-combat card selection (`NSimpleCardSelectScreen`) | ❌ Missing (P1) | — |

### Phase 4: Post-Combat Rewards

| Interaction | Status | Command |
|-------------|--------|---------|
| Claim gold/potion/relic/special\_card | ✅ Implemented | `reward_claim` |
| Choose card reward | ✅ Implemented | `reward_choose_card` |
| Skip card reward | ✅ Implemented | `reward_skip_card` |
| Leave reward screen | ✅ Implemented | `proceed` |

### Phase 5: Events

| Interaction | Status | Command |
|-------------|--------|---------|
| Choose event option | ✅ Implemented | `choose_event` |
| Advance Ancient dialogue | ✅ Implemented | `advance_dialogue` |
| Leave event | ✅ Implemented | `proceed` |
| Event-triggered card selection | ✅ Covered | `potion_select_card` / `deck_select_card` |
| Event-triggered relic selection (`NChooseARelicSelection`) | ❌ Missing (P1) | — |
| Crystal Sphere event (`NCrystalSphereScreen`) | ❌ Missing (P2) | — |
| Bundle selection (`NChooseABundleSelectionScreen`) | ❌ Missing (P2) | — |

### Phase 6: Rest Site

| Interaction | Status | Command |
|-------------|--------|---------|
| Choose rest option | ✅ Implemented | `choose_rest_option` |
| SMITH card upgrade selection | ✅ Implemented | `deck_select_card` |
| COOK card removal selection | ✅ Implemented | `deck_select_card` |
| Leave rest site | ✅ Implemented | `proceed` |

### Phase 7: Treasure Room

| Interaction | Status | Command |
|-------------|--------|---------|
| Open chest | ✅ Implemented | `open_chest` |
| Pick relic | ✅ Implemented | `pick_relic` |
| Leave treasure room | ✅ Implemented | `proceed` |

### Phase 8: Shop

| Interaction | Status | Command |
|-------------|--------|---------|
| Buy card | ✅ Implemented | `shop_buy_card` |
| Buy relic | ✅ Implemented | `shop_buy_relic` |
| Buy potion | ✅ Implemented | `shop_buy_potion` |
| Buy card removal service | ✅ Implemented | `shop_remove_card` |
| Card removal selection | ✅ Implemented | `deck_select_card` |
| Leave shop | ✅ Implemented | `proceed` |

### Phase 9: Act Transition & Game Over

| Interaction | Status | Command |
|-------------|--------|---------|
| Act transition (auto after boss) | ⚠️ No command needed | — |
| TheArchitect event (final boss) | ✅ Covered | `choose_event` |
| Game over screen (`NGameOverScreen`) | ❌ Missing (P0) | — |
| Abandon run | ❌ Missing (P2) | — |

---

## Missing Features Summary

### P0 — Blocking (Agent gets stuck)

| Feature | Game Class | Trigger | Impact |
|---------|-----------|---------|--------|
| **Game Over screen** | `NGameOverScreen` | Death or victory | Agent cannot return to menu or start a new run. **Complete deadlock.** |

### P1 — Functional gaps (some scenarios unhandled)

| Feature | Game Class | Trigger | Impact |
|---------|-----------|---------|--------|
| **In-combat card selection** | `NSimpleCardSelectScreen` | Card effects requiring discard/arrange | Cannot interact with these card effects |
| **Relic selection** | `NChooseARelicSelection` | Some events offer multi-relic choice | Cannot complete these events |
| **CARD\_REWARD state extraction** | — | `state` called on CARD\_REWARD screen | Returns `{ screen: "CARD_REWARD" }` with no card data |

### P2 — Edge cases

| Feature | Game Class | Trigger | Impact |
|---------|-----------|---------|--------|
| Crystal Sphere event | `NCrystalSphereScreen` | Specific event UI | Cannot operate when encountered |
| Bundle selection | `NChooseABundleSelectionScreen` | Select a group of cards | Cannot operate when encountered |
| Main menu navigation | — | Navigate to character select | Requires manual entry to character select |
| Abandon run | — | Voluntarily exit current run | Agent cannot self-reset |

---

## Full Game Loop (Agent Perspective)

```
MENU
  └─ (manual) → CHARACTER_SELECT
       └── select_character → set_ascension → embark
            └─ MAP ←──────────────────────────────┐
                └── choose_map_node ──┐            │
                                      ▼            │
          COMBAT ← Monster/Elite/Boss              │
           ├── play_card / use_potion / end_turn   │
           ├── [POTION_SELECTION] ✅               │
           ├── [SIMPLE_CARD_SELECT] ❌             │
           └── REWARD → proceed ───────────────────┘
                ├── reward_claim
                └── CARD_REWARD → reward_choose/skip
                                                   │
          EVENT ──────────────────── proceed ───────┘
           ├── choose_event / advance_dialogue
           ├── [DECK_CARD_SELECT] ✅
           ├── [RELIC_SELECTION] ❌
           └── [CRYSTAL_SPHERE] ❌
                                                   │
          REST_SITE ────────────── proceed ─────────┘
           └── choose_rest_option → [SMITH] ✅
                                                   │
          TREASURE ─────────────── proceed ─────────┘
           └── open_chest → pick_relic
                                                   │
          SHOP ─────────────────── proceed ─────────┘
           └── shop_buy_* / shop_remove_card

     Boss kill → REWARD → Act transition → next act MAP
     Final Boss → TheArchitect EVENT → Victory

     GAME_OVER ❌  ← Agent stuck after death/victory
```

---

## Recommended Implementation Order

1. **P0: Game Over screen** — Without this, the agent deadlocks after any run ends.
2. **P1: CARD\_REWARD state extraction** — Quick fix, improves agent reliability.
3. **P1: In-combat card selection** — Needed for cards with discard/selection effects.
4. **P1: Relic selection** — Needed for certain events.
5. **P2: Edge cases** — As encountered during testing.
