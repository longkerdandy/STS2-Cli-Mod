# Feature Gap Analysis — STS2-Cli-Mod

> Last updated: 2026-04-01

## Overview

This document catalogs all implemented features and identifies remaining gaps for full game automation.

**Current status**: 36 CLI commands, 17 screen types detected, 22 state builders. Core game loop is fully covered.

---

## Implemented Features

### CLI Commands (36 total)

| # | Command | Parameters | Description |
|---|---------|------------|-------------|
| 1 | `ping` | — | Test pipe connection |
| 2 | `state` | `[--include-pile-details]` | Get full game state as JSON |
| 3 | `play_card` | `<card_id> [--nth <n>] [--target <combat_id>]` | Play a card from hand |
| 4 | `end_turn` | — | End current combat turn |
| 5 | `use_potion` | `<potion_id> [--nth <n>] [--target <combat_id>]` | Use a potion |
| 6 | `reward_claim` | `--type <type> [--id <id>] [--nth <n>]` | Claim a reward (gold/potion/relic/special_card/card_removal) |
| 7 | `reward_choose_card` | `--type card --card_id <card_id> [--nth <n>]` | Pick a card from card reward |
| 8 | `reward_skip_card` | `--type card [--nth <n>]` | Skip a card reward |
| 9 | `proceed` | — | Leave current screen and proceed to map |
| 10 | `choose_event` | `<index>` | Choose an event option by index |
| 11 | `advance_dialogue` | `[--auto]` | Advance Ancient event dialogue |
| 12 | `tri_select_card` | `<card_id> [<card_id>...] [--nth <n>...]` | Select cards from three-choose-one screen |
| 13 | `tri_select_skip` | — | Skip three-choose-one selection |
| 14 | `grid_select_card` | `<card_id> [<card_id>...] [--nth <n>...]` | Select cards from grid selection screen |
| 15 | `grid_select_skip` | — | Skip/cancel grid card selection |
| 16 | `hand_select_card` | `<card_id> [<card_id>...] [--nth <n>...]` | Select cards from hand during combat selection |
| 17 | `hand_confirm_selection` | — | Confirm hand card selection |
| 18 | `select_character` | `<character_id>` | Select character on character select screen |
| 19 | `set_ascension` | `<level>` | Set ascension level |
| 20 | `embark` | — | Start the run from character select |
| 21 | `choose_map_node` | `<col> <row>` | Travel to a map node |
| 22 | `choose_rest_option` | `<option_id>` | Choose rest site option (HEAL/SMITH/etc.) |
| 23 | `open_chest` | — | Open treasure room chest |
| 24 | `pick_relic` | `<index>` | Pick relic from treasure room |
| 25 | `relic_select` | `<index>` | Select relic from boss/event relic choice |
| 26 | `relic_skip` | — | Skip relic selection |
| 27 | `bundle_select` | `<index>` | Preview bundle on bundle selection screen |
| 28 | `bundle_confirm` | — | Confirm the previewed bundle |
| 29 | `bundle_cancel` | — | Cancel bundle preview |
| 30 | `shop_buy_card` | `<card_id> [--nth <n>]` | Buy a card from shop |
| 31 | `shop_buy_relic` | `<relic_id> [--nth <n>]` | Buy a relic from shop |
| 32 | `shop_buy_potion` | `<potion_id> [--nth <n>]` | Buy a potion from shop |
| 33 | `shop_remove_card` | — | Buy card removal service |
| 34 | `crystal_set_tool` | `<tool>` | Set Crystal Sphere divination tool (big/small) |
| 35 | `crystal_click_cell` | `<x> <y>` | Click cell in Crystal Sphere mini-game |
| 36 | `crystal_proceed` | — | Leave Crystal Sphere mini-game |

### Screen Detection (17 types)

| # | Screen | State DTO | Supported Commands |
|---|--------|-----------|-------------------|
| 1 | `CHARACTER_SELECT` | `CharacterSelectStateDto` | `select_character`, `set_ascension`, `embark` |
| 2 | `MENU` | — | — |
| 3 | `COMBAT` | `CombatStateDto` | `play_card`, `end_turn`, `use_potion` |
| 4 | `HAND_SELECT` | `HandSelectStateDto` | `hand_select_card`, `hand_confirm_selection` |
| 5 | `MAP` | `MapStateDto` | `choose_map_node` |
| 6 | `CARD_REWARD` | *(card data in parent REWARD)* | `reward_choose_card`, `reward_skip_card` |
| 7 | `REWARD` | `RewardStateDto` | `reward_claim`, `proceed` |
| 8 | `TRI_SELECT` | `TriSelectStateDto` | `tri_select_card`, `tri_select_skip` |
| 9 | `GRID_CARD_SELECT` | `GridCardSelectStateDto` | `grid_select_card`, `grid_select_skip` |
| 10 | `EVENT` | `EventStateDto` | `choose_event`, `advance_dialogue`, `proceed` |
| 11 | `REST_SITE` | `RestSiteStateDto` | `choose_rest_option`, `proceed` |
| 12 | `TREASURE` | `TreasureStateDto` | `open_chest`, `pick_relic`, `proceed` |
| 13 | `SHOP` | `ShopStateDto` | `shop_buy_*`, `shop_remove_card`, `proceed` |
| 14 | `RELIC_SELECT` | `RelicSelectStateDto` | `relic_select`, `relic_skip` |
| 15 | `BUNDLE_SELECT` | `BundleSelectStateDto` | `bundle_select`, `bundle_confirm`, `bundle_cancel` |
| 16 | `CRYSTAL_SPHERE` | `CrystalSphereStateDto` | `crystal_set_tool`, `crystal_click_cell`, `crystal_proceed` |
| 17 | `UNKNOWN` | — | — |

### State Builders (22 total)

| Builder | Extracts |
|---------|----------|
| `CardStateBuilder` | Card details (ID, name, type, rarity, cost, damage/block preview, playability) |
| `CombatStateBuilder` | Hand cards, draw/discard/exhaust piles, enemies |
| `PlayerStateBuilder` | Character info, relics, potions, gold, HP, energy, stars, orbs, pets |
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
| `TriSelectStateBuilder` | Three-choose-one selection cards |
| `GridCardSelectStateBuilder` | Grid-based card selection |
| `HandSelectStateBuilder` | Hand card selection during combat |
| `RestSiteStateBuilder` | Rest site options with enabled status |
| `TreasureStateBuilder` | Chest status, available relics |
| `ShopStateBuilder` | Shop inventory (cards/relics/potions with prices), card removal |
| `RelicSelectStateBuilder` | Boss/event relic choice |
| `BundleSelectStateBuilder` | Scroll Boxes bundle selection |
| `CrystalSphereStateBuilder` | Crystal Sphere mini-game grid and state |

---

## Coverage by Game Phase

### Phase 1: Main Menu & Character Select

| Interaction | Status | Command |
|-------------|--------|---------|
| Select character | ✅ | `select_character` |
| Set ascension level | ✅ | `set_ascension` |
| Start run (embark) | ✅ | `embark` |

### Phase 2: Map Navigation

| Interaction | Status | Command |
|-------------|--------|---------|
| Choose next map node | ✅ | `choose_map_node` |

Node types: `MONSTER`, `ELITE`, `BOSS`, `SHOP`, `REST_SITE`, `TREASURE`, `ANCIENT`, `UNKNOWN`.

### Phase 3: Combat

| Interaction | Status | Command |
|-------------|--------|---------|
| Play card from hand | ✅ | `play_card` |
| Use potion | ✅ | `use_potion` |
| End turn | ✅ | `end_turn` |
| Hand card selection (discard/exhaust/upgrade) | ✅ | `hand_select_card`, `hand_confirm_selection` |
| Three-choose-one card selection | ✅ | `tri_select_card`, `tri_select_skip` |
| Grid card selection (from draw/discard piles) | ✅ | `grid_select_card`, `grid_select_skip` |

### Phase 4: Post-Combat Rewards

| Interaction | Status | Command |
|-------------|--------|---------|
| Claim gold/potion/relic/special_card/card_removal | ✅ | `reward_claim` |
| Choose card reward | ✅ | `reward_choose_card` |
| Skip card reward | ✅ | `reward_skip_card` |
| Leave reward screen | ✅ | `proceed` |

### Phase 5: Events

| Interaction | Status | Command |
|-------------|--------|---------|
| Choose event option | ✅ | `choose_event` |
| Advance Ancient dialogue | ✅ | `advance_dialogue` |
| Leave event | ✅ | `proceed` |
| Event-triggered card selection | ✅ | `tri_select_card` |
| Event-triggered relic selection | ✅ | `relic_select`, `relic_skip` |
| Crystal Sphere mini-game | ✅ | `crystal_set_tool`, `crystal_click_cell`, `crystal_proceed` |
| Bundle selection (Scroll Boxes) | ✅ | `bundle_select`, `bundle_confirm`, `bundle_cancel` |

### Phase 6: Rest Site

| Interaction | Status | Command |
|-------------|--------|---------|
| Choose rest option | ✅ | `choose_rest_option` |
| SMITH card upgrade selection | ✅ | `grid_select_card` |
| COOK card removal selection | ✅ | `grid_select_card` |
| Leave rest site | ✅ | `proceed` |

### Phase 7: Treasure Room

| Interaction | Status | Command |
|-------------|--------|---------|
| Open chest | ✅ | `open_chest` |
| Pick relic | ✅ | `pick_relic` |
| Leave treasure room | ✅ | `proceed` |

### Phase 8: Shop

| Interaction | Status | Command |
|-------------|--------|---------|
| Buy card/relic/potion | ✅ | `shop_buy_*` |
| Buy card removal service | ✅ | `shop_remove_card` |
| Card removal selection | ✅ | `grid_select_card` |
| Leave shop | ✅ | `proceed` |

### Phase 9: Act Transition & Game Over

| Interaction | Status | Notes |
|-------------|--------|-------|
| Act transition | ✅ Auto | No command needed (auto after boss) |
| TheArchitect event | ✅ | `choose_event` |
| **Game over screen** | ❌ **Missing** | **P0: Agent deadlocks after death/victory** |
| Return to menu | ❌ **Missing** | **P0: Needed after game over** |
| Start new run | ❌ **Missing** | **P0: Without this agent cannot continue** |

---

## Missing Features Summary

### P0 — Blocking (Complete Deadlock)

| Feature | Game Class | Trigger | Impact |
|---------|-----------|---------|--------|
| **Game Over screen** | `NGameOverScreen` | Death or victory | Agent cannot return to menu or start a new run. Complete deadlock. |
| **Return to menu** | — | After game over | Required to escape game over screen |
| **New run from menu** | — | After returning to menu | Required to start next iteration |

**Status**: These are the ONLY remaining blocking issues. Once implemented, the agent can run indefinitely.

### P1-P3 — None

All previously identified P1, P2, and P3 features have been implemented:
- ✅ CARD_REWARD state extraction
- ✅ In-combat card selection (HAND_SELECT)
- ✅ Relic selection (RELIC_SELECT)
- ✅ Crystal Sphere event (CRYSTAL_SPHERE)
- ✅ Bundle selection (BUNDLE_SELECT)

---

## Full Game Loop (Agent Perspective)

```
MENU
  └─ (manual or new command) → CHARACTER_SELECT
       └── select_character → set_ascension → embark
            └─ MAP ←───────────────────────────────────┐
                └── choose_map_node ──┐               │
                                      ▼               │
          COMBAT ← Monster/Elite/Boss                │
           ├── play_card / use_potion / end_turn     │
           ├── HAND_SELECT → hand_select_card        │
           ├── TRI_SELECT → tri_select_card          │
           ├── GRID_CARD_SELECT → grid_select_card   │
           └── REWARD → proceed ─────────────────────┘
                ├── reward_claim
                └── CARD_REWARD → reward_choose/skip
                                                     │
          EVENT ─────────────────── proceed ─────────┘
           ├── choose_event / advance_dialogue
           ├── TRI_SELECT / GRID_CARD_SELECT
           ├── RELIC_SELECT → relic_select/skip      │
           ├── CRYSTAL_SPHERE → crystal_*            │
           └── BUNDLE_SELECT → bundle_*              │
                                                     │
          REST_SITE ───────────── proceed ───────────┘
           └── choose_rest_option → GRID_CARD_SELECT │
                                                     │
          TREASURE ────────────── proceed ───────────┘
           └── open_chest → pick_relic               │
                                                     │
          SHOP ────────────────── proceed ───────────┘
           └── shop_buy_* / shop_remove_card

     Boss kill → REWARD → Act transition → next act MAP
     Final Boss → TheArchitect EVENT → Victory

     GAME_OVER ❌  ← Agent STUCK (needs implementation)
```

---

## Summary

| Category | Count | Status |
|----------|-------|--------|
| CLI Commands | 36 | ✅ Complete |
| Screen Types | 17 | ✅ Complete (except GAME_OVER) |
| State Builders | 22 | ✅ Complete |
| **Blocking Issues** | **3** | **❌ Need GAME_OVER handling** |

The only remaining work is implementing Game Over screen detection and commands to return to menu/start a new run. All other features from the original gap analysis have been implemented.