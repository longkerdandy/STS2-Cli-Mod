# STS2-Cli-Mod

A CLI control mod for Slay the Spire 2. Allows AI Agents to control the game via command line.

**Core Principle**: CLI First, Unix Philosophy. Do one thing well, interact through stdin/stdout.

## Architecture

```
Agent (Cursor/Kimi CLI) → sts2 CLI → Named Pipe/Socket → C# Mod (In-Process) → Game
```

Three-layer structure:
1. **C# Mod** (in-process): Hooks into the game using HarmonyX, exposes Named Pipe interface
2. **sts2 CLI** (standalone): .NET command line tool, protocol translation
3. **Agent**: Calls CLI for decision-making (not included in this project)

## Tech Stack

### Game Engine (Confirmed)
- **Engine**: Godot 4.5.1 (MegaDot v4.5.1.m.8.mono.custom_build)
- **Runtime**: .NET 8/9 (C# / Mono)
- **Game DLL**: `sts2.dll` contains all game logic (~3,300 classes)

### Mod Development
- **Language**: C# 12.0
- **Framework**: .NET 9 (to match game runtime)
- **Hook Framework**: HarmonyX 2.4.2 (runtime method patching)
- **References**: 
  - `sts2.dll` (game logic)
  - `0Harmony.dll` (patching)
  - `GodotSharp.dll` (Godot API)

### CLI
- **Framework**: .NET 9 Console App (C#) - matching game runtime
- **Command parsing**: System.CommandLine
- **Serialization**: System.Text.Json
- **Networking**: System.IO.Pipes (Windows) / Unix Domain Socket

### Communication
- **Protocol**: Named Pipe `sts2-cli-mod` (Windows) / Unix Socket `/tmp/sts2-cli-mod.sock`
- **Data Format**: JSON

## Research Findings

### How Mods Work in STS2

Slay the Spire 2 has **native mod support**:
1. Create `mods/` folder in game directory
2. Place `.dll` + `.pck` files (and `.json` for 0.99+ beta)
3. Launch game → click "Yes" when prompted to load mods
4. Game shows "Running Modded" at bottom right

### Key Modding Technique

Unlike traditional Godot games, STS2 uses **C# for all game logic**. Mods use:
- **HarmonyX** to patch game methods at runtime
- **Direct class access** via referencing `sts2.dll`
- **No GDScript** needed for game logic hooks

### Reference Projects

| Project | Description | Relevance |
|---------|-------------|-----------|
| [luojiesi/SLS2Mods](https://github.com/luojiesi/SLS2Mods) | Undo/Redo, QuickRestart mods | Shows how to capture/restore full combat state using HarmonyX |
| [Gennadiyev/STS2MCP](https://github.com/Gennadiyev/STS2MCP) | HTTP REST API for AI agents | **Same goal as us!** Uses HTTP instead of CLI. Excellent reference for state extraction and action execution |
| [Alchyr/BaseLib-StS2](https://github.com/Alchyr/BaseLib-StS2) | Content mod framework | For content creators (new cards/characters). Less relevant for tool mods |
| [ptrlrd/spire-codex](https://github.com/ptrlrd/spire-codex) | Complete data API | Shows full game data structure via decompilation. Useful for understanding class hierarchy |

### Decompilation

To understand game internals:
```bash
# Install ILSpy CLI
dotnet tool install ilspycmd -g

# Decompile sts2.dll (run from game directory)
ilspycmd -p -o decompiled "data_sts2_windows_x86_64/sts2.dll"
```

Key classes to look for:
- `CombatManager` / `BattleManager` - Combat state
- `Player` / `AbstractPlayer` - Player state, hand, energy
- `Card` / `AbstractCard` - Card information
- `Monster` / `AbstractMonster` - Enemy state and intent

## Current Phase: v0.1.0 - Full Singleplayer Coverage

**Goal**: Support complete singleplayer game loop (all screens)

**Phase 1: Core Infrastructure** ✅
- [x] C# Mod with `[ModInitializer]` entry point
- [x] Named Pipe server (`sts2-cli-mod`)
- [x] Mod loads successfully in-game

**Phase 2: CLI Client** (In Progress)
- [ ] `sts2 ping` - Connection test
- [ ] `sts2 state` - Get current game state
- [ ] All scene-specific commands (see CLI Specification)

**Phase 3: State Extraction**
- [ ] Combat states (monster/elite/boss)
- [ ] Combat card selection (exhaust/discard/upgrade)
- [ ] Post-combat rewards
- [ ] Map navigation (full DAG with lookahead)
- [ ] Rest site, Shop, Events/Ancients
- [ ] Card selection screens (transform/upgrade/remove)
- [ ] Relic selection, Treasure room

**Phase 4: Action Execution**
- [ ] Combat actions (play_card, end_turn, use_potion)
- [ ] Combat selection (select_card, confirm)
- [ ] Reward claims and card picks
- [ ] Map node selection
- [ ] Shop purchases
- [ ] Event options and dialogue
- [ ] Rest options
- [ ] Card/Relic selection screens

**Not in v0.1.0**:
- Multiplayer support (see STS2MCP for reference)
- Streaming output (`sts2 watch`)
- Conditional waiting (`sts2 wait`)
- Non-JSON output formats

## Project Structure

```
STS2-Cli-Mod/
├── mod/                              # C# Mod (.NET 9)
│   ├── STS2.Cli.Mod.csproj            # Project file
│   ├── ModInitializer.cs             # Entry point with [ModInitializer]
│   ├── Server/                       
│   │   └── PipeServer.cs             # Named Pipe server
│   ├── State/                        
│   │   ├── GameStateExtractor.cs     # Extract state from game
│   │   ├── CombatStateExtractor.cs   # Combat-specific extraction
│   │   ├── MapStateExtractor.cs      # Map navigation extraction
│   │   ├── ShopStateExtractor.cs     # Shop inventory extraction
│   │   └── EventStateExtractor.cs    # Event/Ancient extraction
│   ├── Actions/                      
│   │   ├── ActionExecutor.cs         # Base executor
│   │   ├── CombatActions.cs          # play_card, end_turn, use_potion
│   │   ├── CombatSelectionActions.cs # select_card, confirm_selection
│   │   ├── RewardActions.cs          # claim_reward, pick_card, skip_card
│   │   ├── MapActions.cs             # choose_map_node
│   │   ├── ShopActions.cs            # shop_purchase
│   │   ├── EventActions.cs           # choose_event_option, advance_dialogue
│   │   ├── RestActions.cs            # choose_rest_option
│   │   └── SelectActions.cs          # select_card, confirm, cancel, relics
│   └── Patches/                      
│       └── GameStatePatch.cs         # Hook into game state updates
│
├── STS2.Cli.Cmd/                     # .NET CLI tool
│   ├── STS2.Cli.Cmd.csproj
│   ├── Program.cs                    # Entry point
│   ├── Commands/                     
│   │   ├── PingCommand.cs            # sts2 ping
│   │   ├── StateCommand.cs           # sts2 state
│   │   ├── PlayCardCommand.cs        # sts2 play_card
│   │   ├── EndTurnCommand.cs         # sts2 end_turn
│   │   ├── UsePotionCommand.cs       # sts2 use_potion
│   │   ├── SelectCardCommand.cs      # sts2 select_card
│   │   ├── ConfirmSelectionCommand.cs # sts2 confirm_selection
│   │   ├── ClaimRewardCommand.cs     # sts2 claim_reward
│   │   ├── PickCardCommand.cs        # sts2 pick_card
│   │   ├── SkipCardCommand.cs        # sts2 skip_card
│   │   ├── ProceedCommand.cs         # sts2 proceed
│   │   ├── MapCommand.cs             # sts2 map
│   │   ├── RestCommand.cs            # sts2 rest
│   │   ├── EventCommand.cs           # sts2 event
│   │   ├── AdvanceCommand.cs         # sts2 advance
│   │   ├── BuyCommand.cs             # sts2 buy
│   │   ├── SelectCommand.cs          # sts2 select
│   │   ├── ConfirmCommand.cs         # sts2 confirm
│   │   ├── CancelCommand.cs          # sts2 cancel
│   │   ├── PickRelicCommand.cs       # sts2 pick_relic
│   │   ├── SkipRelicCommand.cs       # sts2 skip_relic
│   │   └── TreasureCommand.cs        # sts2 treasure
│   ├── Services/
│   │   ├── PipeClient.cs             # Named Pipe client
│   │   ├── ResponseWriter.cs         # Output formatting
│   │   └── ErrorHandler.cs           # Exit code handling
│   └── Models/
│       ├── GameState.cs              # State models
│       ├── ActionRequest.cs          # Action request models
│       └── ActionResponse.cs         # Response models
│
└── docs/
    └── protocol.md                   # Protocol documentation
```

## Development Environment Setup

### Prerequisites
- **.NET 9.0 SDK** (for both Mod and CLI, matching game runtime)
- **Godot 4.5.1** (optional, for PCK packing)
- **ILSpy** (optional, for decompilation: `dotnet tool install ilspycmd -g`)

### Game Directory Structure
```
C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2/
├── SlayTheSpire2.exe
├── SlayTheSpire2.pck
├── data_sts2_windows_x86_64/
│   ├── sts2.dll              # Game logic (reference this)
│   ├── 0Harmony.dll          # HarmonyX is already included!
│   ├── GodotSharp.dll        # Godot C# API
│   └── ... (other .NET runtime files)
└── mods/                     # Create this folder
    ├── QuickRestart.dll      # Example mod assembly
    ├── QuickRestart.pck      # Resource pack (stable branch)
    ├── QuickRestart.json     # External manifest (0.99+ beta)
    └── ...
```

### Log File Locations

Godot engine logs and Mod logs are written to:

| Platform | Log Location |
|----------|--------------|
| **Windows** | `%APPDATA%\SlayTheSpire2\logs\godot.log` |
| **macOS** | `~/Library/Application Support/SlayTheSpire2/logs/godot.log` |
| **Linux** | `~/.local/share/SlayTheSpire2/logs/godot.log` |

**Quick access:**
```bash
# Windows
%APPDATA%\SlayTheSpire2\logs\godot.log

# Or via Run dialog (Win+R)
%APPDATA%\SlayTheSpire2\logs
```

**Note:** Mod logs are prefixed with `[STS2.Cli.Mod]` in the godot.log file.

### Mod File Formats

For compatibility with both stable (v0.98.3) and beta (0.99+) branches, each mod requires three files:

| File | Purpose | Required By |
|---|---|---|
| `ModName.dll` | Compiled mod assembly | All versions |
| `ModName.pck` | Godot resource pack containing internal `mod_manifest.json` | Stable branch |
| `ModName.json` | External manifest declaring `has_pck` and `has_dll` | Beta branch (0.99+) |

**Internal manifest** (`mod_manifest.json` inside .pck):
```json
{
  "pck_name": "STS2.Cli.Mod.pck",
  "name": "STS2 CLI Mod",
  "author": "...",
  "description": "...",
  "version": "0.1.0"
}
```

**External manifest** (`STS2.Cli.Mod.json`):
```json
{
  "name": "STS2 CLI Mod",
  "author": "...",
  "version": "0.1.0",
  "has_pck": true,
  "has_dll": true
}
```

See [tools/CreatePck/README.md](tools/CreatePck/README.md) for creating PCK files.

### Building the Mod

```bash
cd mod
dotnet build -c Release \
  -p:STS2GameDir="C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2"

# Copy to game
cp bin/Release/net9.0/STS2.Cli.Mod.dll \
   "C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2/mods/"
```

### VS Code Launch Configuration

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Attach to STS2",
      "type": "coreclr",
      "request": "attach",
      "processId": "${command:pickProcess}"
    }
  ]
}
```

## CLI Design Specification

### Command Philosophy
- **Unix-style**: Simple, composable commands
- **Explicit**: Actions are explicit (`play_card` not just `play`)
- **Consistent**: All commands follow `sts2 <action> [args] [--flags]` pattern

### Command Reference

#### Connection & State
| Command | Description |
|---------|-------------|
| `sts2 ping` | Test connection to mod |
| `sts2 state` | Get current game state (JSON) |

#### Combat
| Command | Description |
|---------|-------------|
| `sts2 play_card <index> [--target <entity_id>]` | Play card from hand |
| `sts2 end_turn` | End current turn |
| `sts2 use_potion <slot> [--target <entity_id>]` | Use potion from slot |
| `sts2 select_card <index>` | Select card during combat prompts (exhaust/discard) |
| `sts2 confirm_selection` | Confirm in-combat selection |

#### Rewards & Navigation
| Command | Description |
|---------|-------------|
| `sts2 claim_reward <index>` | Claim post-combat reward |
| `sts2 pick_card <index>` | Select card from reward screen |
| `sts2 skip_card` | Skip card reward |
| `sts2 proceed` | Proceed to map (from rewards/rest/shop/treasure) |

#### Map & Events
| Command | Description |
|---------|-------------|
| `sts2 map <index>` | Choose map node (from next_options) |
| `sts2 rest <index>` | Choose rest site option |
| `sts2 event <index>` | Choose event option |
| `sts2 advance` | Advance ancient dialogue |

#### Shop
| Command | Description |
|---------|-------------|
| `sts2 buy <index>` | Purchase shop item |

#### Selection Screens
| Command | Description |
|---------|-------------|
| `sts2 select <index>` | Select card (transform/upgrade/remove) or toggle selection |
| `sts2 confirm` | Confirm selection (upgrade/transform preview) |
| `sts2 cancel` | Cancel/skip selection |
| `sts2 pick_relic <index>` | Select relic from choice |
| `sts2 skip_relic` | Skip relic selection |
| `sts2 treasure <index>` | Claim treasure relic |

### Output Specification
**Success** (stdout, exit 0):
```json
{"ok": true, "data": {...}}
```

**Error** (stderr, exit ≠ 0):
```json
{"ok": false, "error": "CODE", "message": "..."}
```

### Exit Code Conventions
| Exit Code | Meaning |
|-----------|---------|
| 0 | Success |
| 1 | Connection error (game not running or mod not loaded) |
| 2 | Invalid state (current scene does not support this action) |
| 3 | Invalid parameter (e.g., card index out of range) |
| 4 | Timeout |
| 5 | Game state changed during action |

## Key Design Decisions

1. **.NET 9.0 for both**: Mod matches game runtime; CLI uses same version for consistency
2. **AOT compilation**: CLI compiled to single-file exe (~3MB), no runtime dependency
3. **HarmonyX for Hooking**: Standard for .NET game modding, game already includes 0Harmony.dll
4. **Named Pipe > HTTP**: Unlike STS2MCP which uses HTTP, we use Named Pipe for:
   - No port conflicts
   - Better security (local only)
   - Unix philosophy compliance
5. **JSON only**: MVP phase only supports JSON output
6. **Local only**: Pipe/Socket only listens locally, no authentication

## Build and Release

### Build Commands

Both Mod and CLI use .NET 9.0:

```bash
# Build entire solution
dotnet build -c Release

# Build specific project
dotnet build STS2.Cli.Mod/ -c Release
dotnet build STS2.Cli.Cmd/STS2.Cli.Cmd.csproj -c Release

# Create PCK file (requires GDRE Tools)
# Download from: https://github.com/bruvzg/gdsdecomp/releases
.\tools\CreatePck\CreatePck.ps1

# Or manually copy a template PCK
cp "game/mods/QuickRestart.pck" "STS2.Cli.Mod/STS2.Cli.Mod.pck"

# Release single-file CLI exe (Windows)
dotnet publish STS2.Cli.Cmd/STS2.Cli.Cmd.csproj -c Release -r win-x64 \
  --self-contained \
  -p:PublishSingleFile=true \
  -p:PublishAot=true

# Deploy mod to game (automatic after build)
# Files copied: STS2.Cli.Mod.dll, STS2.Cli.Mod.json, STS2.Cli.Mod.pck
```

## State Protocol

### State Structure

```json
{
  "screen": "COMBAT",
  "screen_type": "monster",
  "player": {
    "character": "Ironclad",
    "hp": 50,
    "max_hp": 80,
    "energy": 3,
    "block": 0,
    "gold": 150
  }
}
```

### Screen Types

| Screen | Type Values | Key Fields |
|--------|-------------|------------|
| **Combat** | `monster`, `elite`, `boss` | `enemies`, `hand`, `is_player_turn`, `draw_pile`, `discard_pile` |
| **Combat Selection** | `hand_select` | `select_mode` (exhaust/discard/upgrade), `selectable_cards`, `selected_cards` |
| **Post-Combat** | `combat_rewards` | `rewards[]`, `can_proceed` |
| **Card Reward** | `card_reward` | `card_choices[]`, `can_skip` |
| **Map** | `map` | `current_node`, `visited_path`, `next_options[]`, `full_map` |
| **Rest Site** | `rest_site` | `options[]`, `can_proceed` |
| **Shop** | `shop` | `cards[]`, `relics[]`, `potions[]`, `card_removal_cost` |
| **Event** | `event` | `event_id`, `event_name`, `is_ancient`, `in_dialogue`, `options[]` |
| **Card Select** | `card_select` | `select_type` (transform/upgrade/remove/select), `cards[]`, `preview` |
| **Relic Select** | `relic_select` | `relics[]`, `can_skip` |
| **Treasure** | `treasure` | `relics[]`, `can_proceed` |
| **Menu** | `menu` | No run in progress |

### Common State Fields

All states include:
```json
{
  "screen": "COMBAT",
  "screen_type": "monster",
  "player": {
    "character": "Ironclad",
    "hp": 50,
    "max_hp": 80,
    "gold": 150
  }
}
```

### Combat State Example

```json
{
  "screen": "COMBAT",
  "screen_type": "monster",
  "player": {
    "character": "Ironclad",
    "hp": 50,
    "max_hp": 80,
    "energy": 3,
    "block": 5,
    "gold": 150,
    "relics": ["Burning Blood", "Vajra"],
    "potions": [{"slot": 0, "id": "FirePotion", "name": "Fire Potion"}],
    "powers": [{"id": "Strength", "amount": 2}]
  },
  "hand": [
    {
      "index": 0,
      "id": "Strike",
      "name": "Strike",
      "cost": 1,
      "type": "Attack",
      "rarity": "Basic",
      "description": "Deal 6 damage.",
      "upgraded": false,
      "can_play": true,
      "keywords": [{"keyword": "Attack", "description": "..."}]
    }
  ],
  "enemies": [
    {
      "index": 0,
      "entity_id": "jaw_worm_0",
      "name": "Jaw Worm",
      "hp": 45,
      "max_hp": 45,
      "block": 0,
      "intent": {
        "type": "ATTACK",
        "damage": 11,
        "times": 1,
        "description": "Intent to attack for 11 damage."
      },
      "powers": []
    }
  ],
  "is_player_turn": true,
  "draw_pile": 8,
  "discard_pile": 2,
  "exhaust_pile": 0
}
```

### Map State Example

```json
{
  "screen": "MAP",
  "screen_type": "map",
  "player": { "character": "Ironclad", "hp": 50, "max_hp": 80, "gold": 150 },
  "current_node": {"row": 5, "col": 2},
  "visited_path": [{"row": 0, "col": 1}, {"row": 1, "col": 1}, {"row": 2, "col": 2}],
  "next_options": [
    {"index": 0, "row": 6, "col": 1, "type": "Monster", "children": ["Elite", "RestSite"]},
    {"index": 1, "row": 6, "col": 2, "type": "Shop", "children": ["Monster"]},
    {"index": 2, "row": 6, "col": 3, "type": "Unknown", "children": []}
  ],
  "full_map": {
    "nodes": [
      {"row": 0, "col": 0, "type": "Start", "edges": [{"to_row": 1, "to_col": 0}, {"to_row": 1, "to_col": 1}]}
    ]
  }
}
```

## AI Development Tips

1. **Check connection first**: Use `sts2 ping` before calling other commands to confirm the mod is loaded
2. **Check screen type**: Parse `screen` and `screen_type` to determine available actions
3. **Defensive programming**: Always check state fields before actions:
   - Check `can_play` before `play_card`
   - Check `can_skip` before `skip_card` or `skip_relic`
   - Check option `enabled` status before choosing events/rest options
4. **Handle errors**: Check exit codes and stderr for actionable error messages
5. **Use --target for cards/potions**: Some cards/potions require `entity_id` target (from enemies list)

## Usage Examples

### Connection & State
```bash
# Check connection
$ sts2 ping
{"ok":true,"data":{"connected":true}}

# Get current state
$ sts2 state
{"ok":true,"data":{"screen":"COMBAT","screen_type":"monster","player":{...}}}
```

### Combat
```bash
# Play card at index 0, targeting jaw_worm_0
$ sts2 play_card 0 --target jaw_worm_0
{"ok":true,"data":{"action":"play_card","card_index":0,"target":"jaw_worm_0"}}

# Play AoE card (no target needed)
$ sts2 play_card 2
{"ok":true,"data":{"action":"play_card","card_index":2}}

# End turn
$ sts2 end_turn
{"ok":true,"data":{"action":"end_turn"}}

# Use potion in slot 0
$ sts2 use_potion 0 --target jaw_worm_0
{"ok":true,"data":{"action":"use_potion","slot":0,"target":"jaw_worm_0"}}

# Select card during "exhaust" prompt
$ sts2 select_card 1
{"ok":true,"data":{"action":"select_card","card_index":1}}

# Confirm selection (exhaust/discard/upgrade)
$ sts2 confirm_selection
{"ok":true,"data":{"action":"confirm_selection"}}
```

### Rewards & Navigation
```bash
# Claim first reward (gold/potion/relic)
$ sts2 claim_reward 0
{"ok":true,"data":{"action":"claim_reward","reward_index":0}}

# Select second card from reward
$ sts2 pick_card 1
{"ok":true,"data":{"action":"pick_card","card_index":1}}

# Skip card reward
$ sts2 skip_card
{"ok":true,"data":{"action":"skip_card_reward"}}

# Proceed to map after rewards
$ sts2 proceed
{"ok":true,"data":{"action":"proceed"}}
```

### Map & Events
```bash
# Choose map node at index 1 (from next_options)
$ sts2 map 1
{"ok":true,"data":{"action":"choose_map_node","index":1}}

# Choose rest option (0=rest, 1=smith, etc.)
$ sts2 rest 0
{"ok":true,"data":{"action":"choose_rest_option","index":0}}

# Choose event option at index 0
$ sts2 event 0
{"ok":true,"data":{"action":"choose_event_option","index":0}}

# Advance ancient dialogue
$ sts2 advance
{"ok":true,"data":{"action":"advance_dialogue"}}
```

### Shop
```bash
# Buy item at index 2
$ sts2 buy 2
{"ok":true,"data":{"action":"shop_purchase","index":2}}

# Leave shop (proceed to map)
$ sts2 proceed
{"ok":true,"data":{"action":"proceed"}}
```

### Selection Screens
```bash
# Select card for transform/upgrade/remove
$ sts2 select 0
{"ok":true,"data":{"action":"select_card","index":0}}

# Confirm selection
$ sts2 confirm
{"ok":true,"data":{"action":"confirm_selection"}}

# Cancel/skip selection
$ sts2 cancel
{"ok":true,"data":{"action":"cancel_selection"}}

# Select relic
$ sts2 pick_relic 1
{"ok":true,"data":{"action":"select_relic","index":1}}

# Skip relic
$ sts2 skip_relic
{"ok":true,"data":{"action":"skip_relic"}}

# Claim treasure relic
$ sts2 treasure 0
{"ok":true,"data":{"action":"claim_treasure_relic","index":0}}
```

### Error Examples
```bash
# Wrong screen type
$ sts2 play_card 0
{"ok":false,"error":"INVALID_STATE","message":"Action play_card not valid in screen: MAP"}
# exit code: 2

# Index out of range
$ sts2 play_card 10
{"ok":false,"error":"INVALID_PARAM","message":"Card index 10 out of range (hand size: 5)"}
# exit code: 3

# Card requires target
$ sts2 play_card 0
{"ok":false,"error":"INVALID_PARAM","message":"Card 'Strike' requires target but none provided"}
# exit code: 3
```

## Installation Instructions

### Mod Installation
1. Build `STS2.Cli.Mod.dll` from source
2. Ensure you have all three files:
   - `STS2.Cli.Mod.dll` (compiled assembly)
   - `STS2.Cli.Mod.pck` (resource pack with mod_manifest.json)
   - `STS2.Cli.Mod.json` (external manifest)
3. Copy all three files to game directory `Slay the Spire 2/mods/`
4. Launch game, click "Yes" when prompted to load mods
5. Confirm "Running Modded" displays at bottom right

### CLI Installation
1. Download `STS2.Cli.Cmd.exe` and add to PATH
2. Or run `dotnet tool install -g STS2.Cli.Cmd` (if published as global tool)

## Changelog

- **v0.1.0**: Full Singleplayer Coverage
  - CLI command design with Unix philosophy
  - All singleplayer screens supported (combat, map, shop, events, etc.)
  - Named Pipe communication (Mod ↔ CLI)
  - JSON state protocol with screen-type discrimination
  - Reference: STS2MCP for scene coverage patterns
- **Milestone**: Mod core architecture ✅
  - Project renamed from `STS2-CLI` to `STS2-Cli-Mod`
  - Confirmed game uses Godot 4.5.1 + .NET 9.0
  - Confirmed native mod support (.dll + .json for 0.99+)
  - Identified HarmonyX as hook framework
  - Discovered reference projects (SLS2Mods, STS2MCP, spire-codex)
  - Mod loads successfully in-game with Named Pipe server
