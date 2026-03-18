# Mod Module Development Plan

This document outlines the development roadmap for the STS2-Cli-Mod C# mod module.

---

## Overview

The Mod module is an in-process C# mod that:
- Hooks into Slay the Spire 2 using HarmonyX
- Exposes game state via Named Pipe interface
- Executes game actions (play cards, end turn)
- Communicates with the CLI tool

**Target Runtime**: .NET 9.0 (matching game runtime)  
**Hook Framework**: HarmonyX 2.4.2  
**Communication**: Named Pipe `sts2-cli-mod`

---

## Phase 1: Project Setup

**Goal**: Create a compilable Mod project framework

### Tasks

| # | Task | Deliverable | Verification |
|---|------|-------------|--------------|
| 1.1 | Create project structure | `mod/` directory with .NET 9 class library | Project compiles |
| 1.2 | Configure assembly references | `STS2.CliMod.csproj` with sts2.dll, GodotSharp.dll, 0Harmony.dll | No reference errors |
| 1.3 | Implement entry point | `[ModInitializer]` attributed class | Logger outputs on load |
| 1.4 | Test compilation | Build DLL and load in game | "Running Modded" appears |

### Key Implementation

```csharp
using Godot;

[ModInitializer]
public static class ModInitializer 
{
    public static void Initialize() 
    {
        Logger.Info("STS2.CliMod loaded successfully!");
    }
}
```

### Project Structure

```
mod/
├── STS2.CliMod.csproj
├── ModInitializer.cs
└── (future files...)
```

---

## Phase 2: Named Pipe Communication

**Goal**: Establish bidirectional communication between Mod and CLI

### Tasks

| # | Task | Deliverable | Notes |
|---|------|-------------|-------|
| 2.1 | Implement PipeServer | `PipeServer.cs` class | Async, non-blocking |
| 2.2 | Define message protocol | `Request` and `Response` models | JSON serialization |
| 2.3 | Command routing | `CommandRouter.cs` | Maps commands to handlers |
| 2.4 | Ping/heartbeat | `ping` command response | Connection health check |

### Protocol Specification

**Request Format**:
```json
{"cmd": "ping"}
{"cmd": "state"}
{"cmd": "play", "args": [0]}
{"cmd": "end"}
```

**Response Format**:
```json
{"ok": true, "data": {...}}
{"ok": false, "error": "ERROR_CODE", "message": "..."}
```

### Architecture

```
[CLI] --NamedPipe--> [PipeServer] --CommandRouter--> [Handlers]
```

---

## Phase 3: Game State Extraction

**Goal**: Read combat state from the game

### Prerequisites

- Decompile `sts2.dll` using ILSpy
- Identify key classes:
  - `CombatManager` / `BattleManager`
  - `Player` / `AbstractPlayer`
  - `AbstractCard`
  - `AbstractMonster`

### Tasks

| # | Task | Deliverable | Reference |
|---|------|-------------|-----------|
| 3.1 | Decompile analysis | Document key classes and methods | ILSpy output |
| 3.2 | State extractor | `GameStateExtractor.cs` | SLS2Mods snapshot logic |
| 3.3 | Player state | HP, max HP, energy, block | CombatManager.Instance.Player |
| 3.4 | Hand state | Card list with cost, can_play | Player.Hand |
| 3.5 | Enemy state | HP, intent, powers | CombatManager.Instance.Enemies |
| 3.6 | Serialization | JSON-safe DTOs | Avoid circular references |

### State Structure

```json
{
  "screen": "COMBAT",
  "player": {
    "hp": 50,
    "max_hp": 80,
    "energy": 3,
    "block": 0
  },
  "hand": [
    {"index": 0, "id": "Strike", "cost": 1, "can_play": true}
  ],
  "enemies": [
    {"index": 0, "name": "Cultist", "hp": 50, "intent": "ATTACK_6"}
  ],
  "is_player_turn": true
}
```

---

## Phase 4: Action Execution

**Goal**: Execute in-game actions programmatically

### Tasks

| # | Task | Deliverable | Challenge |
|---|------|-------------|-----------|
| 4.1 | Harmony patch setup | Patch framework | Find hook points |
| 4.2 | Play card logic | `PlayCard(int index)` | Target selection |
| 4.3 | End turn logic | `EndTurn()` | Wait for resolution |
| 4.4 | Safety validation | Pre-condition checks | Prevent crashes |

### Implementation Approach

Using HarmonyX to patch game methods:

```csharp
[HarmonyPatch(typeof(CombatManager), "Update")]
public class CombatManagerPatch
{
    public static bool Prefix(CombatManager __instance)
    {
        // Check for pending actions from pipe
        // Execute if valid
        // Return true to allow normal update
    }
}
```

### Validation Rules

- Is player turn?
- Is card index valid?
- Has enough energy?
- Is action queue idle?

---

## Phase 5: Integration & Testing

**Goal**: End-to-end functionality with CLI

### Tasks

| # | Task | Acceptance Criteria |
|---|------|---------------------|
| 5.1 | E2E testing | All CLI commands work correctly |
| 5.2 | Error handling | Graceful degradation, no crashes |
| 5.3 | Performance | No visible game lag |
| 5.4 | Documentation | Protocol spec complete |

### Test Scenarios

```bash
# Test ping
$ sts2 ping
{"ok":true,"data":{"connected":true}}

# Test state in combat
$ sts2 state
{"ok":true,"data":{"screen":"COMBAT",...}}

# Test play card
$ sts2 play 0
{"ok":true,"data":{"action":"PLAY_CARD","card":"Strike"}}

# Test end turn
$ sts2 end
{"ok":true,"data":{"action":"END_TURN"}}
```

---

## Phase Summary

| Phase | Focus | Milestone |
|-------|-------|-----------|
| 1 | Project setup & communication | Pipe connection works |
| 2 | State extraction | Can read full combat state |
| 3 | Action execution | Can play cards and end turn |
| 4 | Integration & polish | MVP complete |

---

## Technical Specifications

### Dependencies

```xml
<!-- STS2.CliMod.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
  
  <ItemGroup>
    <!-- Game assemblies -->
    <Reference Include="sts2">
      <HintPath>$(STS2GameDir)\data_sts2_windows_x86_64\sts2.dll</HintPath>
    </Reference>
    <Reference Include="GodotSharp">
      <HintPath>$(STS2GameDir)\data_sts2_windows_x86_64\GodotSharp.dll</HintPath>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>$(STS2GameDir)\data_sts2_windows_x86_64\0Harmony.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

### Build Commands

```bash
# Build mod
dotnet build mod/ -c Release \
  -p:STS2GameDir="C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2"

# Deploy
cp mod/bin/Release/net9.0/STS2.CliMod.dll \
   "C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2/mods/"
```

### Debugging

- **Logs**: `%APPDATA%/SlayTheSpire2/logs/`
- **Attach**: Use VS Code "Attach to Process"
- **Hot reload**: Replace DLL when game closed

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Class names changed in update | High | Reference projects track updates |
| Harmony patch conflicts | Medium | Use non-destructive prefixes |
| Performance overhead | Low | Cache state, minimize reflection |
| Multiplayer compatibility | Medium | Test single-player only first |

---

## References

- [SLS2Mods](https://github.com/luojiesi/SLS2Mods) - State capture/restore examples
- [STS2MCP](https://github.com/Gennadiyev/STS2MCP) - Complete API implementation
- [spire-codex](https://github.com/ptrlrd/spire-codex) - Game data structure reference

---

## Next Steps

1. **Immediate**: Begin Phase 1 - Project Setup
2. **Decision**: Whether to fork STS2MCP or build from scratch
3. **Prerequisite**: Install .NET 9.0 SDK and ILSpy

---

*Last Updated: 2026-03-18*  
*Version: v0.1.0-draft*
