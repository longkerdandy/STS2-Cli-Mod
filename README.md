# STS2 CLI Mod

A CLI control mod for **Slay the Spire 2**. Enables AI agents (or human scripters) to observe game state and execute actions entirely from the command line.

Two independent .NET 9 / C# projects communicate via Named Pipe with JSON messages:

```
AI Agent ──> sts2 CLI ──Named Pipe──> In-Game Mod ──> Game
```

- **STS2.Cli.Mod** — An in-process game mod that runs inside the Godot engine, reads game state and executes actions on behalf of the CLI.
- **STS2.Cli.Cmd** — A standalone CLI tool (`sts2` / `sts2.exe`) that sends commands over the pipe and prints JSON responses to stdout.

## Features

- **Full game state extraction** — Query the complete game state as structured JSON: combat (hand, enemies, intents, powers, draw/discard/exhaust piles), map, events, shop, rewards, rest sites, and more.
- **Action execution** — Play cards, use potions, end turns, navigate the map, pick rewards, buy from shops, choose event options, and handle every game screen.
- **16+ screen types detected** — Combat, Map, Event, Shop, Reward, Rest Site, Treasure, Character Select, Crystal Sphere mini-game, and others.
- **40+ commands** — Covering the full game flow from main menu to game over.
- **Structured JSON protocol** — All responses follow `{"ok": true, "data": {...}}` / `{"ok": false, "error": "CODE", "message": "..."}` format with snake_case naming.
- **Cross-platform CLI** — Windows, macOS (Intel & Apple Silicon), and Linux builds. WSL fully supported.

## Installation

### One-Line Install

**Windows (PowerShell):**

```powershell
irm https://raw.githubusercontent.com/longkerdandy/STS2-Cli-Mod/main/install.ps1 | iex
```

**WSL / macOS / Linux:**

```bash
curl -fsSL https://raw.githubusercontent.com/longkerdandy/STS2-Cli-Mod/main/install.sh | bash
```

The scripts automatically:
- Detect your Steam game directory (including custom Steam library folders)
- Download the latest CLI and Mod from GitHub Releases
- Install the CLI to your PATH (Windows: `%LOCALAPPDATA%\sts2-cli`, macOS/Linux: `~/.local/bin`)
- Deploy the Mod to the game's `mods/` directory

> **WSL note:** The CLI is installed to the Windows side (`%LOCALAPPDATA%\sts2-cli\sts2.exe`) with a bash alias, since the game and Named Pipe run on Windows.

### Install a Specific Version

**PowerShell:**

```powershell
$env:STS2_VERSION="0.102.1"; irm https://raw.githubusercontent.com/longkerdandy/STS2-Cli-Mod/main/install.ps1 | iex
```

**bash:**

```bash
curl -fsSL https://raw.githubusercontent.com/longkerdandy/STS2-Cli-Mod/main/install.sh | bash -s -- -v 0.102.1
```

### Uninstall

**PowerShell:**

```powershell
$env:STS2_UNINSTALL="1"; irm https://raw.githubusercontent.com/longkerdandy/STS2-Cli-Mod/main/install.ps1 | iex
```

**bash:**

```bash
curl -fsSL https://raw.githubusercontent.com/longkerdandy/STS2-Cli-Mod/main/install.sh | bash -s -- --uninstall
```

### Manual Install

Download the latest release from [GitHub Releases](https://github.com/longkerdandy/STS2-Cli-Mod/releases):

1. **CLI** — `sts2-cli-v{version}-{platform}.zip` (or `.tar.gz`). Extract and add to your PATH.
2. **Mod** — `sts2-mod-v{version}.zip`. Extract `STS2.Cli.Mod.dll` and `STS2.Cli.Mod.json` into `<game directory>/mods/`.

## Quick Start

1. Launch **Slay the Spire 2** with mods enabled
2. Open a terminal and verify the connection:

```bash
sts2 ping
# {"ok":true,"data":{"connected":true}}
```

3. Query the current game state:

```bash
sts2 state -p
```

4. Execute actions based on the current screen:

```bash
# Start a new run
sts2 new_run
sts2 select_character ironclad
sts2 embark

# Navigate the map
sts2 choose_map_node 3 1

# Play cards in combat
sts2 play_card STRIKE --target 0
sts2 play_card DEFEND
sts2 end_turn

# Use a potion
sts2 use_potion FIRE_POTION --target 0
```

## Commands

All commands output JSON to stdout. Use `--pretty` / `-p` for human-readable formatting.

**Exit codes:** 0 = success, 1 = connection error, 2 = invalid state, 3 = invalid parameter, 4 = timeout, 5 = state changed.

For the full command reference with detailed parameters and response formats, see [docs/cli-reference.md](docs/cli-reference.md).

### Connection & State

| Command | Description |
|---------|-------------|
| `ping` | Test connection to the mod |
| `state` | Get current game state as JSON |

### Main Menu

| Command | Description |
|---------|-------------|
| `new_run` | Start a new game |
| `continue_run` | Continue a saved run |
| `abandon_run` | Abandon the saved run |
| `choose_game_mode <mode>` | Select game mode (standard, daily, custom) |

### Character Select

| Command | Description |
|---------|-------------|
| `select_character <id>` | Select a character |
| `set_ascension <level>` | Set ascension level (0-20) |
| `embark` | Start the run |

### Combat

| Command | Description |
|---------|-------------|
| `play_card <card_id> [--target <combat_id>]` | Play a card from hand |
| `end_turn` | End the current turn |
| `use_potion <potion_id> [--target <combat_id>]` | Use a potion |
| `hand_select_card <card_id> [...]` | Select cards from hand (discard, exhaust, etc.) |
| `hand_confirm_selection` | Confirm hand card selection |
| `grid_select_card <card_id> [...]` | Select cards from grid (remove, upgrade, etc.) |
| `grid_select_skip` | Skip grid card selection |

### Map & Navigation

| Command | Description |
|---------|-------------|
| `choose_map_node <col> <row>` | Travel to a map node |
| `proceed` | Leave current screen and go to map |

### Rewards

| Command | Description |
|---------|-------------|
| `reward_claim --type <type> [--id <id>]` | Claim a reward (gold, potion, relic) |
| `reward_choose_card --card_id <id>` | Pick a card reward |
| `reward_skip_card` | Skip a card reward |

### Events

| Command | Description |
|---------|-------------|
| `choose_event <index>` | Choose an event option |
| `advance_dialogue [--auto]` | Advance dialogue in Ancient events |

### Card Selection Screens

| Command | Description |
|---------|-------------|
| `tri_select_card <card_id> [...]` | Select from three-choose-one |
| `tri_select_skip` | Skip three-choose-one selection |

### Rest Site

| Command | Description |
|---------|-------------|
| `choose_rest_option <option_id>` | Choose rest option (HEAL, SMITH, etc.) |

### Treasure Room

| Command | Description |
|---------|-------------|
| `open_chest` | Open the treasure chest |
| `pick_relic <index>` | Pick a relic from the chest |

### Shop

| Command | Description |
|---------|-------------|
| `shop_buy_card <card_id>` | Buy a card |
| `shop_buy_relic <relic_id>` | Buy a relic |
| `shop_buy_potion <potion_id>` | Buy a potion |
| `shop_remove_card` | Buy card removal service |

### Relic & Bundle Selection

| Command | Description |
|---------|-------------|
| `relic_select <index>` | Select a relic from boss/event choice |
| `relic_skip` | Skip relic selection |
| `bundle_select <index>` | Preview a bundle (Scroll Boxes relic) |
| `bundle_confirm` | Confirm bundle selection |
| `bundle_cancel` | Cancel bundle preview |

### Crystal Sphere

| Command | Description |
|---------|-------------|
| `crystal_set_tool <tool>` | Switch tool (big / small) |
| `crystal_click_cell <x> <y>` | Click a cell to clear fog |
| `crystal_proceed` | Leave the mini-game |

### Game Over

| Command | Description |
|---------|-------------|
| `return_to_menu` | Return to main menu |

### Utility

| Command | Description |
|---------|-------------|
| `report_bug --title <t> --description <d>` | Save a structured bug report locally |

## Architecture

```
STS2-Cli-Mod/
├── STS2.Cli.Cmd/         # Standalone CLI tool
│   ├── Commands/          #   Command definitions (System.CommandLine)
│   ├── Client/            #   Named Pipe client
│   └── Models/            #   Request/Response DTOs
├── STS2.Cli.Mod/          # In-process game mod
│   ├── Actions/           #   Action handlers (play_card, end_turn, ...)
│   ├── State/Builders/    #   State extraction (combat, map, event, ...)
│   ├── Server/            #   Named Pipe server
│   ├── Models/            #   Request + State DTOs
│   └── Utils/             #   Thread marshalling, JSON, logging
├── install.ps1            # Windows installer
├── install.sh             # WSL / macOS / Linux installer
└── docs/
    └── cli-reference.md   # Full command reference
```

The two projects share no code. They communicate exclusively through a Named Pipe (`sts2-cli-mod`) using JSON messages. The mod runs inside the Godot 4.5.1 engine and marshals all game state access to the main thread.

## Building from Source

Requires [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) and a local copy of Slay the Spire 2.

```bash
# Build the Mod (auto-deploys to game mods/ directory)
dotnet build STS2.Cli.Mod/STS2.Cli.Mod.csproj -c Release

# Publish the CLI as a single-file executable
dotnet publish STS2.Cli.Cmd/STS2.Cli.Cmd.csproj -c Release -r win-x64
```

Supported runtime identifiers: `win-x64`, `osx-x64`, `osx-arm64`, `linux-x64`.

## Related Projects

| Project | Description |
|---------|-------------|
| [STS2MCP](https://github.com/Gennadiyev/STS2MCP) | MCP-based AI agent control for STS2 |
| [SLS2Mods](https://github.com/luojiesi/SLS2Mods) | HarmonyX patching examples for STS2 |
| [spire-codex](https://github.com/ptrlrd/spire-codex) | Full game data via decompilation |

## License

This project is not affiliated with Mega Crit Games.
