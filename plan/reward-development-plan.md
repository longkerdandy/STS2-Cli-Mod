# Combat Reward Settlement - Development Plan

This document outlines the development plan for post-combat reward settlement features. After combat ends, the game transitions to a reward screen where the player can claim gold, potions, relics, and choose cards. We need to expose this via the CLI so that an AI agent can interact with the full reward flow.

---

## Game Reward System Summary

### Flow

```
Combat Victory
  → RewardsCmd.OfferForRoomEnd(player, room)
    → RewardsSet.WithRewardsFromRoom(room)
      → GenerateRewardsFor() builds reward list per room type
    → RewardsSet.Offer()
      → Populate each reward (roll gold, cards, potions, relics)
      → Hook.ModifyRewards() / Hook.BeforeRewardsOffered()
      → NRewardsScreen.ShowScreen(isTerminal: true)
        → Player interacts: claim rewards, pick cards
        → OnProceedButtonPressed()
          → RunManager.Instance.ProceedFromTerminalRewardsScreen()
            → Open map screen
```

### Reward Types (RewardType Enum)

| Type | Class | Index | Generation | Claim Behavior |
|------|-------|-------|------------|----------------|
| Gold | `GoldReward` | 1 | Always (per room type gold range) | `PlayerCmd.GainGold()` — instant |
| Potion | `PotionReward` | 2 | Probabilistic (PotionRewardOdds roll) | `PotionCmd.TryToProcure()` — may fail if belt full |
| Relic | `RelicReward` | 3 | Elite only (pulled from relic queue) | `RelicCmd.Obtain()` — instant |
| SpecialCard | `SpecialCardReward` | 4 | Event/encounter specific | `CardPileCmd.Add()` — instant, direct to deck |
| Card | `CardReward` | 5 | Always (3 options per room type rarity odds) | Opens `NCardRewardSelectionScreen` — player picks 1 or skips |
| CardRemoval | `CardRemovalReward` | 7 | Event specific | Opens card removal UI |
| LinkedRewardSet | `LinkedRewardSet` | varies | Special (pick-one-of-N bundles) | Claims sub-reward, removes siblings |

### Rewards Per Room Type

| Room | Gold | Potion (roll) | Card (3 choices) | Relic |
|------|------|---------------|-------------------|-------|
| Monster | 10-20 | Yes | Yes (regular rarity odds) | No |
| Elite | 35-45 | Yes | Yes (elite rarity odds) | Yes |
| Boss | 100 | Yes | Yes (100% rare odds) | No* |

*Boss rewards omitted if final act boss.

### Key Game APIs

| Class | Access | Purpose |
|-------|--------|---------|
| `NOverlayStack.Instance` | `NRun.Instance.GlobalUi.Overlays` | Get current overlay screen |
| `NOverlayStack.Peek()` | Top of overlay stack | Identify which screen is shown |
| `NRewardsScreen` | Overlay screen | Reward list UI, proceed button |
| `NRewardButton.Reward` | Property on UI button | Access underlying `Reward` object |
| `Reward.OnSelectWrapper()` | Instance method | Claim a reward (calls `OnSelect()` + hooks) |
| `Reward.OnSkipped()` | Instance method | Mark reward as skipped |
| `CardReward.Cards` | `IEnumerable<CardModel>` | Get the 3 card choices |
| `NCardRewardSelectionScreen` | Overlay screen | Card pick sub-screen |
| `RunManager.Instance.ProceedFromTerminalRewardsScreen()` | Instance method | Leave reward screen, go to map |

---

## Implementation Plan

### Step 1: Screen Detection — Detect Reward Screen

**Goal**: Extend `GameStateExtractor.DetectScreen()` to recognize the reward screen.

**Changes**:
- `STS2.Cli.Mod/State/GameStateExtractor.cs` — Add reward screen detection logic

**Implementation**:
```csharp
// After the COMBAT check, before returning "UNKNOWN"
// Check NOverlayStack for reward-related screens
var overlay = NOverlayStack.Instance?.Peek();
if (overlay is NCardRewardSelectionScreen) return "CARD_REWARD";
if (overlay is NRewardsScreen) return "REWARD";
```

**Detection priority** (order matters):
1. `MENU` — no run in progress
2. `COMBAT` — combat is in progress
3. `CARD_REWARD` — card reward selection sub-screen is on top
4. `REWARD` — reward screen is on top
5. `UNKNOWN` — everything else (map, shop, event, etc.)

`CARD_REWARD` must be checked before `REWARD` because `NCardRewardSelectionScreen` is pushed on top of `NRewardsScreen` in the overlay stack.

**Deliverables**:
- [ ] `GameStateExtractor.DetectScreen()` returns `"REWARD"` and `"CARD_REWARD"`
- [ ] `sts2 state` shows correct screen when on reward screen

---

### Step 2: Reward State Extraction — Read Reward List

**Goal**: When screen is `REWARD`, extract the list of available rewards and return them in `sts2 state`.

**New Files**:
- `STS2.Cli.Mod/Models/Dto/RewardStateDto.cs` — Root reward state DTO
- `STS2.Cli.Mod/Models/Dto/RewardItemDto.cs` — Individual reward DTO
- `STS2.Cli.Mod/State/Builders/RewardStateBuilder.cs` — Builds reward DTOs from game objects

**Modified Files**:
- `STS2.Cli.Mod/Models/Dto/GameStateDto.cs` — Add `Rewards` property
- `STS2.Cli.Mod/State/GameStateExtractor.cs` — Extract rewards when screen is `REWARD`

**DTO Design**:

```csharp
// RewardStateDto - top-level container
public class RewardStateDto
{
    public required List<RewardItemDto> Rewards { get; set; }
}

// RewardItemDto - individual reward
public class RewardItemDto
{
    public required int Index { get; set; }           // Position in reward list (for claim_reward)
    public required string Type { get; set; }         // "Gold", "Potion", "Relic", "Card", "SpecialCard", "CardRemoval"
    public required string Description { get; set; }  // Localized description

    // Gold-specific
    public int? GoldAmount { get; set; }

    // Potion-specific
    public string? PotionId { get; set; }
    public string? PotionName { get; set; }
    public string? PotionRarity { get; set; }

    // Relic-specific
    public string? RelicId { get; set; }
    public string? RelicName { get; set; }
    public string? RelicDescription { get; set; }
    public string? RelicRarity { get; set; }

    // Card-specific (for CardReward: the 3 card choices)
    public List<CardChoiceDto>? CardChoices { get; set; }

    // SpecialCard-specific (for SpecialCardReward: the single card)
    public string? CardId { get; set; }
    public string? CardName { get; set; }
}

// CardChoiceDto - card option within a CardReward
public class CardChoiceDto
{
    public required int Index { get; set; }           // 0-2, for choose_card
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Type { get; set; }
    public required string Rarity { get; set; }
    public required int Cost { get; set; }
    public bool IsUpgraded { get; set; }
}
```

**Builder Logic**:
1. Get `NRewardsScreen` from `NOverlayStack`
2. Find all `NRewardButton` children in the rewards container
3. For each button, read its `.Reward` property
4. Map `Reward` subclass to `RewardItemDto` using pattern matching:
   - `GoldReward` → extract `Amount`
   - `PotionReward` → extract `Potion` (Id, Title, Rarity)
   - `RelicReward` → use reflection or internal field to get `_relic` (Id, Title, Description, Rarity)
   - `CardReward` → extract `.Cards` as `CardChoiceDto[]`
   - `SpecialCardReward` → extract card info
   - `CardRemovalReward` → type only, no extra data

**JSON Output Example** (`sts2 state` on reward screen):
```json
{
  "ok": true,
  "data": {
    "screen": "REWARD",
    "timestamp": 1711123456789,
    "rewards": {
      "rewards": [
        {"index": 0, "type": "Gold", "description": "25 Gold", "gold_amount": 25},
        {"index": 1, "type": "Potion", "description": "Fire Potion", "potion_id": "FIRE_POTION", "potion_name": "Fire Potion", "potion_rarity": "Common"},
        {"index": 2, "type": "Card", "description": "Add a card to your deck", "card_choices": [
          {"index": 0, "id": "INFLAME", "name": "Inflame", "description": "Gain 2 Strength.", "type": "Power", "rarity": "Uncommon", "cost": 1, "is_upgraded": false},
          {"index": 1, "id": "SHRUG_IT_OFF", "name": "Shrug It Off", "description": "Gain 8 Block. Draw 1 card.", "type": "Skill", "rarity": "Common", "cost": 1, "is_upgraded": false},
          {"index": 2, "id": "ANGER", "name": "Anger", "description": "Deal 6 damage. Add a copy to discard.", "type": "Attack", "rarity": "Common", "cost": 0, "is_upgraded": false}
        ]}
      ]
    }
  }
}
```

**Deliverables**:
- [ ] `RewardItemDto` and `CardChoiceDto` DTO classes
- [ ] `RewardStateBuilder.Build()` extracts reward list from `NRewardsScreen`
- [ ] `GameStateExtractor` returns rewards when screen is `"REWARD"`
- [ ] `GameStateDto.Rewards` property added

---

### Step 3: Claim Reward — Gold, Potion, Relic, SpecialCard

**Goal**: New CLI command to claim a non-card reward by index.

**New Files**:
- `STS2.Cli.Mod/Actions/ClaimRewardHandler.cs` — Handler for claim_reward
- `STS2.Cli.Cmd/Program.cs` — Add `claim_reward` command

**Modified Files**:
- `STS2.Cli.Mod/Server/PipeServer.cs` — Route `claim_reward` command
- `STS2.Cli.Cmd/Services/CommandRunner.cs` — Add new error codes to exit code mapping

**CLI Syntax**:
```bash
sts2 claim_reward <index>
# index: 0-based position in the reward list
```

**Request**:
```json
{"cmd": "claim_reward", "args": [0]}
```

**Handler Logic**:
1. Validate screen is `REWARD` (not `CARD_REWARD`, not `COMBAT`, etc.)
2. Get `NRewardsScreen` from overlay stack
3. Find reward buttons, validate index is in range
4. Get `Reward` object from button
5. If reward is `CardReward` → reject with error `USE_CHOOSE_CARD` (must use `choose_card` command)
6. If reward is `CardRemovalReward` → reject with error `NOT_SUPPORTED` for now
7. Call `reward.OnSelectWrapper()` — this internally:
   - `GoldReward.OnSelect()` → `PlayerCmd.GainGold()` → always succeeds
   - `PotionReward.OnSelect()` → `PotionCmd.TryToProcure()` → may fail if belt full
   - `RelicReward.OnSelect()` → `RelicCmd.Obtain()` → always succeeds
   - `SpecialCardReward.OnSelect()` → `CardPileCmd.Add()` → always succeeds
8. Return result

**Success Response**:
```json
{
  "ok": true,
  "data": {
    "reward_index": 0,
    "reward_type": "Gold",
    "claimed": true
  }
}
```

**Error Cases**:
| Error Code | Condition |
|------------|-----------|
| `NOT_ON_REWARD_SCREEN` | Current screen is not `REWARD` |
| `INVALID_REWARD_INDEX` | Index out of range |
| `USE_CHOOSE_CARD` | Reward is `CardReward` (use `choose_card` instead) |
| `NOT_SUPPORTED` | Reward type not yet supported (e.g., `CardRemovalReward`) |
| `POTION_BELT_FULL` | Potion reward but no empty slots |

**Threading Model**: `RunOnMainThreadAsync` — `OnSelectWrapper()` is async.

**Deliverables**:
- [ ] `ClaimRewardHandler.ExecuteAsync(int rewardIndex)` with guard clauses
- [ ] `PipeServer` routes `claim_reward` to handler
- [ ] CLI `sts2 claim_reward <index>` command
- [ ] Error codes added to `CommandRunner.MapErrorToExitCode()`

---

### Step 4: Choose Card Reward — Pick a Card or Skip

**Goal**: New CLI command to interact with card rewards.

This is the most complex step because `CardReward.OnSelect()` opens a sub-screen (`NCardRewardSelectionScreen`) and enters a `while(true)` loop awaiting UI input. We need to either:
- **Option A**: Bypass the UI entirely and call `CardPileCmd.Add()` directly, then clean up
- **Option B**: Trigger `OnSelect()` to open the sub-screen, then simulate card selection on it

We choose **Option A** because it avoids complex UI synchronization and matches the AutoSlay pattern for simplicity.

**New Files**:
- `STS2.Cli.Mod/Actions/ChooseCardHandler.cs` — Handler for choose_card

**Modified Files**:
- `STS2.Cli.Mod/Server/PipeServer.cs` — Route `choose_card` command
- `STS2.Cli.Cmd/Program.cs` — Add `choose_card` command

**CLI Syntax**:
```bash
# Pick a card from the card reward
sts2 choose_card <reward_index> <card_index>

# Skip the card reward (take nothing)
sts2 skip_card <reward_index>
```

**Requests**:
```json
// Pick card
{"cmd": "choose_card", "args": [2, 1]}   // reward_index=2, card_index=1

// Skip card reward
{"cmd": "skip_card", "args": [2]}        // reward_index=2
```

**Handler Logic (choose_card)**:
1. Validate screen is `REWARD`
2. Get reward at `reward_index`, validate it is a `CardReward`
3. Get `CardReward.Cards`, validate `card_index` in range
4. Get the selected `CardModel`
5. Call `CardPileCmd.Add(card, PileType.Deck)` to add card to deck
6. Record card choice in history (both picked and skipped cards)
7. Sync via `RewardSynchronizer`
8. Remove the reward button from the reward screen UI
9. Return result

**Handler Logic (skip_card)**:
1. Validate screen is `REWARD`
2. Get reward at `reward_index`, validate it is a `CardReward`
3. Call `reward.OnSkipped()` — records all cards as not picked in history
4. Remove the reward button from the reward screen UI
5. Return result

**Success Response (choose_card)**:
```json
{
  "ok": true,
  "data": {
    "reward_index": 2,
    "card_index": 1,
    "card_id": "SHRUG_IT_OFF",
    "card_name": "Shrug It Off"
  }
}
```

**Error Cases**:
| Error Code | Condition |
|------------|-----------|
| `NOT_ON_REWARD_SCREEN` | Current screen is not `REWARD` |
| `INVALID_REWARD_INDEX` | Reward index out of range |
| `NOT_CARD_REWARD` | Reward at index is not a `CardReward` |
| `INVALID_CARD_INDEX` | Card index out of range (0-2 typically) |

**Complexity Notes**:
- Directly calling `CardPileCmd.Add()` bypasses the `CardReward.OnSelect()` UI flow. We need to manually handle: card choice history recording, reward synchronizer sync, and removing the reward from the screen UI.
- Need to investigate whether `NRewardButton` has a public method to trigger removal, or if we need to call `NRewardsScreen.RewardCollectedFrom()`.
- This is the highest-risk step and may need iteration based on testing.

**Deliverables**:
- [ ] `ChooseCardHandler.ExecuteAsync(int rewardIndex, int cardIndex)`
- [ ] `SkipCardHandler.Execute(int rewardIndex)` (or combined into ChooseCardHandler)
- [ ] `PipeServer` routes `choose_card` and `skip_card`
- [ ] CLI `sts2 choose_card <reward_index> <card_index>` and `sts2 skip_card <reward_index>`

---

### Step 5: Proceed — Leave Reward Screen

**Goal**: New CLI command to leave the reward screen and proceed to the map.

**New Files**:
- `STS2.Cli.Mod/Actions/ProceedHandler.cs` — Handler for proceed

**Modified Files**:
- `STS2.Cli.Mod/Server/PipeServer.cs` — Route `proceed` command
- `STS2.Cli.Cmd/Program.cs` — Add `proceed` command

**CLI Syntax**:
```bash
sts2 proceed
```

**Request**:
```json
{"cmd": "proceed"}
```

**Handler Logic**:
1. Validate screen is `REWARD` (the reward screen must be showing)
2. Get `NRewardsScreen` from overlay stack
3. Call all remaining rewards' `OnSkipped()` (same as what `AfterOverlayClosed` does)
4. Call `RunManager.Instance.ProceedFromTerminalRewardsScreen()`
5. Wait for the reward screen to close (check `NOverlayStack` no longer has it, or map opens)
6. Return result

**Success Response**:
```json
{
  "ok": true,
  "data": {
    "action": "PROCEED"
  }
}
```

**Error Cases**:
| Error Code | Condition |
|------------|-----------|
| `NOT_ON_REWARD_SCREEN` | Current screen is not `REWARD` |

**Threading Model**: `RunOnMainThreadAsync` — `ProceedFromTerminalRewardsScreen()` is async and involves scene transitions.

**Deliverables**:
- [ ] `ProceedHandler.ExecuteAsync()`
- [ ] `PipeServer` routes `proceed`
- [ ] CLI `sts2 proceed` command

---

## Implementation Order & Dependencies

```
Step 1 ──→ Step 2 ──→ Step 3 ──→ Step 5
                  └──→ Step 4 ──┘
```

- **Step 1** (Screen Detection) is prerequisite for all others — we must know we're on the reward screen.
- **Step 2** (State Extraction) is prerequisite for Steps 3-5 — the AI needs to see rewards before acting.
- **Steps 3 and 4** are independent of each other but both need Step 2.
- **Step 5** can be done after Step 3 (doesn't need Step 4).

**Recommended order**: 1 → 2 → 3 → 4 → 5

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| `NRewardButton.Reward` not accessible | High | It's a public property in decompiled source. Fallback: traverse `NRewardsScreen` child nodes. |
| `CardReward.Cards` not populated when we read it | Medium | Rewards are populated during `RewardsSet.Offer()` before screen shows. Verify `IsPopulated` flag. |
| Direct `CardPileCmd.Add()` misses side effects | High | Study `CardReward.OnSelect()` carefully. Must replicate: history recording, synchronizer sync, UI cleanup. If too complex, fall back to Option B (simulate UI). |
| `Reward.OnSelectWrapper()` triggers unexpected UI | Medium | Gold/Potion/Relic `OnSelect()` don't open sub-screens. Only `CardReward` does. Guard against it. |
| `ProceedFromTerminalRewardsScreen()` race condition | Medium | Add timeout. Check for map screen or overlay removal as completion signal. |
| Accessing private fields (e.g., `RelicReward._relic`) | Medium | Use reflection as fallback. Check if there's a public accessor first (`ClaimedRelic` is set after claim, not before). |

---

## New Error Codes Summary

| Error Code | Exit Code | Commands |
|------------|-----------|----------|
| `NOT_ON_REWARD_SCREEN` | 2 (invalid state) | claim_reward, choose_card, skip_card, proceed |
| `INVALID_REWARD_INDEX` | 3 (invalid param) | claim_reward, choose_card, skip_card |
| `NOT_CARD_REWARD` | 3 (invalid param) | choose_card, skip_card |
| `INVALID_CARD_INDEX` | 3 (invalid param) | choose_card |
| `USE_CHOOSE_CARD` | 3 (invalid param) | claim_reward |
| `POTION_BELT_FULL` | 2 (invalid state) | claim_reward |
| `NOT_SUPPORTED` | 2 (invalid state) | claim_reward |

---

## CLI Command Summary (After Completion)

| Command | Syntax | Description |
|---------|--------|-------------|
| `sts2 ping` | `sts2 ping` | Test connection |
| `sts2 state` | `sts2 state` | Get game state (combat OR rewards) |
| `sts2 play_card` | `sts2 play_card <index> [--target <id>]` | Play a card in combat |
| `sts2 end_turn` | `sts2 end_turn` | End combat turn |
| `sts2 use_potion` | `sts2 use_potion <slot> [--target <id>]` | Use a potion in combat |
| `sts2 claim_reward` | `sts2 claim_reward <index>` | Claim gold/potion/relic reward |
| `sts2 choose_card` | `sts2 choose_card <reward_index> <card_index>` | Pick a card from card reward |
| `sts2 skip_card` | `sts2 skip_card <reward_index>` | Skip a card reward |
| `sts2 proceed` | `sts2 proceed` | Leave reward screen |

---

*Last Updated: 2026-03-22*
*Version: v0.3.0*
