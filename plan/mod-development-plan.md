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

## Completed Work ✅

### Phase 1: Project Setup - COMPLETE

| Task | Status | Notes |
|------|--------|-------|
| Create project structure | ✅ | `STS2.Cli.Mod/` .NET 9 class library |
| Configure assembly references | ✅ | sts2.dll, GodotSharp.dll, 0Harmony.dll |
| Implement entry point | ✅ | `[ModInitializer("Initialize")]` working |
| Test compilation | ✅ | Loads successfully, "Running Modded" confirmed |
| Git support | ✅ | Repository initialized, .gitignore configured |

### Phase 2: Named Pipe Communication - COMPLETE

| Task | Status | Notes |
|------|--------|-------|
| Implement PipeServer | ✅ | Async, non-blocking, Byte mode |
| Define message protocol | ✅ | JSON Request/Response models |
| Command routing | ✅ | Maps commands to handlers |
| Ping/heartbeat | ✅ | `{"ok":true,"data":{"connected":true}}` |
| Fix connection issues | ✅ | PipeTransmissionMode.Byte + explicit ACL permissions |
| Fix dispose exceptions | ✅ | `leaveOpen: true` for StreamReader/Writer |

### CLI Implementation - COMPLETE

| Command | Status | Notes |
|---------|--------|-------|
| `sts2 ping` | ✅ | Connection test working |
| `sts2 state` | ✅ | Returns game state (currently mock data) |
| `sts2 play_card <index>` | ✅ | Command exists, execution stubbed |
| `sts2 end_turn` | ✅ | Command exists, execution stubbed |
| `sts2 use_potion <slot>` | ✅ | Command exists, execution stubbed |

---

## Current Phase: Game State Extraction & Action Execution

### Phase 3: Real Game State Extraction 🔄 IN PROGRESS

**Current Status**: Mock data implemented, needs real game integration

**Goal**: Read actual combat state from the game

#### Prerequisites

- [ ] Decompile `sts2.dll` using ILSpy
- [ ] Identify key classes:
  - `CombatManager` / `BattleManager`
  - `Player` / `AbstractPlayer`
  - `AbstractCard`
  - `AbstractMonster`

#### Tasks

| # | Task | Deliverable | Status |
|---|------|-------------|--------|
| 3.1 | Decompile analysis | Document key classes and methods | ⏳ Pending |
| 3.2 | State extractor | `GameStateExtractor.cs` | ⏳ Pending |
| 3.3 | Player state | HP, max HP, energy, block | ⏳ Pending |
| 3.4 | Hand state | Card list with cost, can_play | ⏳ Pending |
| 3.5 | Enemy state | HP, intent, powers | ⏳ Pending |
| 3.6 | Screen detection | COMBAT/MAP/SHOP/EVENT | ⏳ Pending |

**Current Mock Response**:
```json
{
  "screen": "COMBAT",
  "player": {"hp": 50, "max_hp": 80, "energy": 3, "block": 0},
  "hand": [{"index": 0, "id": "Strike", "cost": 1, "can_play": true}],
  "enemies": [{"index": 0, "name": "Cultist", "hp": 50, "intent": "ATTACK_6"}],
  "is_player_turn": true
}
```

---

### Phase 4: Real Action Execution 🔄 IN PROGRESS

**Current Status**: Framework ready, needs Harmony patch integration

**Goal**: Execute in-game actions programmatically

#### Tasks

| # | Task | Deliverable | Status |
|---|------|-------------|--------|
| 4.1 | Harmony patch setup | Patch framework | ⏳ Pending |
| 4.2 | Play card logic | `PlayCard(int index)` | ⏳ Pending |
| 4.3 | End turn logic | `EndTurn()` | ⏳ Pending |
| 4.4 | Safety validation | Pre-condition checks | ⏳ Pending |

#### Implementation Approach

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

#### Validation Rules

- Is player turn?
- Is card index valid?
- Has enough energy?
- Is action queue idle?

---

## Phase 5: Integration & Testing

**Goal**: End-to-end functionality with CLI

### Tasks

| # | Task | Acceptance Criteria | Status |
|---|------|---------------------|--------|
| 5.1 | E2E testing | All CLI commands work correctly | ⏳ Pending |
| 5.2 | Error handling | Graceful degradation, no crashes | ✅ Partial |
| 5.3 | Performance | No visible game lag | ⏳ Pending |
| 5.4 | Documentation | Protocol spec complete | ⏳ Pending |

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

| Phase | Focus | Status | Milestone |
|-------|-------|--------|-----------|
| 1 | Project setup | ✅ Complete | Mod loads in game |
| 2 | Named Pipe communication | ✅ Complete | `sts2 ping` works |
| 3 | Real state extraction | 🔄 In Progress | Read actual game state |
| 4 | Real action execution | 🔄 In Progress | Play cards, end turn |
| 5 | Integration & polish | ⏳ Pending | MVP complete |

---

## Technical Specifications

### Project Structure

```
STS2-Cli-Mod/
├── STS2.Cli.Mod/              # C# Mod (.NET 9)
│   ├── ModInitializer.cs      # Entry point
│   ├── Server/PipeServer.cs   # Named Pipe server
│   ├── Models/                # Request/Response
│   └── STS2.Cli.Mod.csproj
├── STS2.Cli.Cmd/              # CLI tool (.NET 9)
│   ├── Program.cs             # Entry point
│   ├── Services/PipeClient.cs # Named Pipe client
│   ├── Services/CommandRunner.cs
│   ├── Models/                # Request/Response
│   └── STS2.Cli.Cmd.csproj
├── plan/
│   └── mod-development-plan.md
└── AGENTS.md
```

### Dependencies

```xml
<!-- STS2.Cli.Mod.csproj -->
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
# Build CLI
dotnet build STS2.Cli.Cmd/ -c Release

# Build Mod
dotnet build STS2.Cli.Mod/ -c Release

# Run CLI
dotnet STS2.Cli.Cmd/bin/Release/net9.0/win-x64/sts2.dll ping
```

### Debugging

- **Logs**: `%APPDATA%/SlayTheSpire2/logs/godot.log`
- **Attach**: Use VS Code "Attach to Process"
- **Hot reload**: Replace DLL when game closed

---

## Key Fixes Applied

| Issue | Solution |
|-------|----------|
| Connection hang | `CancellationTokenSource` for timeout instead of `ConnectAsync(timeoutMs)` |
| Pipe not accessible | `NamedPipeServerStreamAcl.Create` with explicit `PipeSecurity` |
| Dispose exception | `leaveOpen: true` in StreamReader/Writer constructors |
| Message mode issues | `PipeTransmissionMode.Byte` instead of `Message` |

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

1. **Immediate**: Install ILSpy and decompile sts2.dll
2. **Find**: CombatManager, Player, Card classes
3. **Implement**: Real state extraction in HandleStateRequest()
4. **Implement**: Harmony patches for action execution
5. **Test**: Full combat loop with real game

---

*Last Updated: 2026-03-18*  
*Version: v0.2.0*
