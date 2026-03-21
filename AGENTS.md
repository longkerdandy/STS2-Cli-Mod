# AGENTS.md - Coding Agent Instructions for STS2-Cli-Mod

A CLI control mod for Slay the Spire 2. Two independent .NET 9 / C# 12 projects communicating via Named Pipe with JSON messages.

## Architecture

```
Agent -> sts2 CLI (STS2.Cli.Cmd) -> Named Pipe -> C# Mod (STS2.Cli.Mod, in-process) -> Game
```

- **STS2.Cli.Mod/**: In-process game mod. References `sts2.dll` and `GodotSharp.dll` from the game directory. One NuGet package (`System.IO.Pipes.AccessControl` for Windows pipe ACL). Runs inside Godot 4.5.1 engine.
- **STS2.Cli.Cmd/**: Standalone CLI tool. Depends on `System.CommandLine`. Outputs JSON to stdout/stderr.
- The two projects share no code. `Request`/`Response` models are intentionally duplicated.

## Build Commands

```bash
# Build entire solution (requires game DLLs for the Mod project)
dotnet build -c Release

# Build CLI only (no game dependency)
dotnet build STS2.Cli.Cmd/STS2.Cli.Cmd.csproj -c Release

# Build Mod only (requires STS2GameDir pointing to game install)
dotnet build STS2.Cli.Mod/ -c Release \
  -p:STS2GameDir="C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2"

# Publish CLI as single-file exe
dotnet publish STS2.Cli.Cmd/STS2.Cli.Cmd.csproj -c Release -r win-x64 \
  --self-contained -p:PublishSingleFile=true -p:PublishAot=true
```

The Mod project auto-deploys DLL + JSON to `$(STS2GameDir)/mods/` after build via the `DeployMod` MSBuild target.

## Testing

There are no test projects. Testing is done manually by running the game with the mod loaded. Verify the mod works by:

1. `sts2 ping` - confirms pipe connection
2. `sts2 state` - confirms state extraction (returns full combat state as JSON)
3. `sts2 play_card <index> [--target <combat_id>]` - confirms card play with execution results (damage, block, powers)
4. `sts2 end_turn` - confirms end turn action

## Code Style Guidelines

### Language & Framework

- C# 12.0, .NET 9.0, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`
- File-scoped namespaces everywhere: `namespace STS2.Cli.Mod.State.Builders;`
- No `AllowUnsafeBlocks`, no `#region` blocks

### Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Namespaces | PascalCase, dot-separated | `STS2.Cli.Mod.Actions` |
| Classes / Interfaces | PascalCase | `PipeServer`, `CardStateBuilder` |
| Public/private methods | PascalCase | `GetState()`, `DetectScreen()` |
| Properties | PascalCase | `IsPlayerTurn`, `CanPlay` |
| Constants (`const`) | PascalCase | `PipeName`, `DefaultTimeoutMs` |
| Private fields | `_camelCase` | `_pipe`, `_reader`, `_cts` |
| Local variables | camelCase | `combatState`, `aliveEnemies` |
| Parameters | camelCase | `card`, `timeoutMs`, `ct` |
| JSON wire format | snake_case or camelCase | `"card_index"`, `"ok"` |
| Error codes | SCREAMING_SNAKE_CASE strings | `"NOT_IN_COMBAT"`, `"TARGET_REQUIRED"` |

**Exception**: The `Logger` field omits the underscore prefix by convention: `private static readonly ModLogger Logger = new("ClassName");`

### Import Ordering

1. `System.*` namespaces
2. Third-party / game namespaces (`Godot`, `MegaCrit.Sts2.*`)
3. Project-internal namespaces (`STS2.Cli.Mod.*`, `STS2.Cli.Cmd.*`)
4. Using aliases and static usings last

```csharp
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;
```

### Formatting

- 4-space indentation (no tabs)
- Allman brace style (opening brace on new line)
- Target-typed `new()` preferred: `new CancellationTokenSource()`
- Collection expressions for inline arrays: `["--pretty", "-p"]`
- Empty collections use `= []` for DTOs (prefer consistency with existing code)
- No trailing commas
- Lines generally under 120 characters

### Class Design Patterns

- **Static utility classes** for stateless logic: `TextUtils`, `JsonOptions`, `ActionUtils`
- **Static handler classes** for actions: `PlayCardHandler` (`ExecuteAsync()`), `EndTurnHandler` (`Execute()`)
- **Static builder classes** with `Build()` / `BuildFromHistory()` for data extraction: `CardStateBuilder`, `PlayerStateBuilder`, `CombatHistoryBuilder`
- **DTO classes** with public auto-properties `{ get; set; }`, no behavior, nullable fields where optional
- Use `required` keyword for mandatory properties: `public required string Cmd { get; set; }`

### Internal class organization

1. Private static readonly fields (Logger, constants)
2. Private instance fields
3. Public properties
4. `Dispose()` (when applicable)
5. Public methods
6. Private helper methods

### Documentation

- XML doc comments (`/// <summary>`) on all public classes and methods
- Use `<param>`, `<returns>`, `<remarks>`, `<example>`, `<see cref="...">` tags as appropriate
- Inline `//` comments for design decisions, TODOs, and non-obvious logic
- ReSharper suppressions via `[SuppressMessage]` on DTOs where needed

### Error Handling

- Wrap game API calls in try-catch; return partial/default state on failure (never crash the game)
- Log errors via `Logger.Warning()` or `Logger.Error()` with the exception message
- Action handlers use sequential guard clauses with early returns, each returning a specific error
- Structured error responses: `new { ok = false, error = "CODE", message = "description" }`
- Exit codes: 0=success, 1=connection, 2=invalid state, 3=invalid param, 4=timeout, 5=state changed

### Null Handling

- Nullable reference types are enabled; use `string?`, `int?` etc. for optional fields
- Use null-conditional (`?.`) and null-coalescing (`??`) operators
- Pattern matching for null checks: `if (obj is not { IsConnected: true })`
- Avoid null-forgiving `!` except where nullability is logically guaranteed

### Async Patterns

- Standard TAP (Task-based Asynchronous Pattern)
- Pass `CancellationToken` through async chains
- Fire-and-forget only for server startup: `_ = Task.Run(async () => { ... });`
- `MainThreadExecutor.RunOnMainThread<T>()` blocks pipe thread to synchronize with Godot main thread
- `MainThreadExecutor.RunOnMainThreadAsync<T>()` kicks off an async chain on the main thread, returns `Task<T>` the pipe thread can await (used for multi-frame actions like `play_card`)
- No `ConfigureAwait(false)` (not needed in Godot context)

### JSON Serialization

- **Mod**: `JsonIgnoreCondition.WhenWritingNull`, custom `IgnoreEmptyCollections` modifier, `UnsafeRelaxedJsonEscaping`
- **CLI**: `PropertyNamingPolicy.CamelCase`, `UnsafeRelaxedJsonEscaping`, separate `Default` and `Pretty` variants
- Anonymous types for ad-hoc responses: `new { ok = true, data = new { connected = true } }`

### Thread Safety

- Pipe server runs on a background thread; game state must be accessed on the Godot main thread
- Use `MainThreadExecutor` (ConcurrentQueue + ProcessFrame signal) to marshal calls
- `RunOnMainThread<T>()` for synchronous single-frame work (state reads, end_turn)
- `RunOnMainThreadAsync<T>()` for async multi-frame work (play_card waits for action completion)

## Project References

| Project | Why It Matters |
|---|---|
| [STS2MCP](https://github.com/Gennadiyev/STS2MCP) | Same goal (AI agent control). Reference for state extraction and action execution patterns |
| [SLS2Mods](https://github.com/luojiesi/SLS2Mods) | HarmonyX patching examples for STS2 |
| [spire-codex](https://github.com/ptrlrd/spire-codex) | Full game data structure via decompilation |

## Key Game Classes

Access game state through singletons: `CombatManager.Instance`, `RunManager.Instance`. Key namespaces live in `MegaCrit.Sts2.Core.*`. Decompile `sts2.dll` with ILSpy for full class reference.

## STS2 Reverse Engineering

A sibling project at `~/STS2-Reverse-Engineering` contains:

- **`decompiled/sts2/`**: Full ILSpy decompilation of `sts2.dll`, organized by namespace (e.g. `MegaCrit.Sts2.Core.Combat`, `MegaCrit.Sts2.Core.Cards`). Use this to look up game class internals, method signatures, and data structures.
- **`doc/`**: Analysis documents summarizing game internals:
  - `card-guide.md` - Card system, card models, and card mechanics
  - `combat-guide.md` - Combat flow, action queue, and combat history
  - `map-guide.md` - Map generation and navigation
  - `monster-guide.md` - Monster definitions, moves, and intents
  - `player-guide.md` - Player state, energy, and hand management
  - `potion-guide.md` - Potion system and effects
  - `relic-guide.md` - Relic system and triggers

When you need to understand a game API, search the decompiled source there rather than guessing.

## Logs

Mod logs are written to the Godot log file, prefixed with `[STS2.Cli.Mod]`:
- Windows: `%APPDATA%\SlayTheSpire2\logs\godot.log`
- macOS: `~/Library/Application Support/SlayTheSpire2/logs/godot.log`
- Linux: `~/.local/share/SlayTheSpire2/logs/godot.log`
