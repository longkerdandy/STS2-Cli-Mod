# Character Select Screen Support - Development Plan

This document outlines the implementation plan for AI control of the Character Select screen in Slay the Spire 2.

---

## Overview

### Goal
Enable CLI control of the Character Select screen, allowing AI to:
1. Detect available characters and their status
2. Select a character
3. Set ascension level
4. Start the game (Embark)

### Game Flow
```
Character Select Screen (NCharacterSelectScreen)
  ↓
Select Character (NCharacterSelectButton) → Update UI
  ↓
Set Ascension Level (NAscensionPanel) → Optional
  ↓
Click Embark (NConfirmButton)
  ↓
Neow Event (Ancient) → Use advance_dialogue + choose_event
  ↓
MAP Screen
```

---

## Phase 1: Data Transfer Objects (DTOs)

### New Files

#### `STS2.Cli.Mod/Models/Dto/CharacterSelectStateDto.cs`

```csharp
public class CharacterSelectStateDto
{
    /// <summary>
    ///     Available characters for selection.
    /// </summary>
    public List<CharacterOptionDto> AvailableCharacters { get; set; } = [];

    /// <summary>
    ///     Currently selected character ID (null if none selected).
    /// </summary>
    public string? SelectedCharacter { get; set; }

    /// <summary>
    ///     Current ascension level.
    /// </summary>
    public int CurrentAscension { get; set; }

    /// <summary>
    ///     Maximum available ascension level.
    /// </summary>
    public int MaxAscension { get; set; }

    /// <summary>
    ///     Whether the player can click Embark (character selected).
    /// </summary>
    public bool CanEmbark { get; set; }
}

public class CharacterOptionDto
{
    /// <summary>
    ///     Character identifier (e.g., "ironclad", "silent").
    /// </summary>
    public string CharacterId { get; set; } = string.Empty;

    /// <summary>
    ///     Localized character name.
    /// </summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>
    ///     Whether the character is locked.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    ///     Whether this character is currently selected.
    /// </summary>
    public bool IsSelected { get; set; }
}
```

### Modifications

#### `STS2.Cli.Mod/Models/Dto/GameStateDto.cs`

Add property:
```csharp
/// <summary>
///     Character selection state if on character select screen, null otherwise.
/// </summary>
public CharacterSelectStateDto? CharacterSelect { get; set; }
```

---

## Phase 2: Screen Detection and State Extraction

### Modified Files

#### `STS2.Cli.Mod/State/GameStateExtractor.cs`

**Changes:**

1. **Update DetectScreen()** - Add CHARACTER_SELECT detection:
```csharp
private static string DetectScreen()
{
    // Check Character Select screen BEFORE checking IsInProgress
    if (NCharacterSelectScreen.Instance?.IsInsideTree() == true)
        return "CHARACTER_SELECT";
    
    // Existing logic...
    if (!RunManager.Instance.IsInProgress) return "MENU";
    // ...
}
```

2. **Add ExtractCharacterSelectState()**:
```csharp
private static CharacterSelectStateDto? ExtractCharacterSelectState()
{
    try
    {
        var screen = NCharacterSelectScreen.Instance;
        if (screen == null || !screen.IsInsideTree())
            return null;

        // Get character buttons
        var buttonContainer = screen.GetNode<Control>("CharSelectButtons/ButtonContainer");
        var buttons = UiHelper.FindAll<NCharacterSelectButton>(buttonContainer);
        
        var characters = new List<CharacterOptionDto>();
        string? selectedCharacter = null;

        foreach (var btn in buttons)
        {
            // Use reflection to get CharacterModel
            var characterModel = GetCharacterModel(btn);
            if (characterModel == null) continue;

            var isSelected = IsButtonSelected(screen, btn);
            if (isSelected) 
                selectedCharacter = characterModel.Id.Entry;

            characters.Add(new CharacterOptionDto
            {
                CharacterId = characterModel.Id.Entry,
                CharacterName = characterModel.Name,
                IsLocked = GetIsLocked(btn),
                IsSelected = isSelected
            });
        }

        // Get ascension info
        var ascensionPanel = screen.GetNode<NAscensionPanel>("%AscensionPanel");
        var (currentAsc, maxAsc) = GetAscensionInfo(ascensionPanel);

        return new CharacterSelectStateDto
        {
            AvailableCharacters = characters,
            SelectedCharacter = selectedCharacter,
            CurrentAscension = currentAsc,
            MaxAscension = maxAsc,
            CanEmbark = selectedCharacter != null
        };
    }
    catch (Exception ex)
    {
        Logger.Error($"Failed to extract character select state: {ex.Message}");
        return null;
    }
}
```

3. **Update GetState()** to extract character select state:
```csharp
if (state.Screen == "CHARACTER_SELECT") 
    state.CharacterSelect = ExtractCharacterSelectState();
```

---

## Phase 3: Action Handlers

### New Files

#### `STS2.Cli.Mod/Actions/SelectCharacterHandler.cs`

**Purpose**: Select a character by clicking its button.

```csharp
public static class SelectCharacterHandler
{
    private static readonly ModLogger Logger = new("SelectCharacterHandler");

    public static object Execute(string characterId)
    {
        return MainThreadExecutor.RunOnMainThread(() =>
        {
            // Guard: Must be on character select screen
            var screen = NCharacterSelectScreen.Instance;
            if (screen == null || !screen.IsInsideTree())
            {
                return new { 
                    ok = false, 
                    error = "NOT_IN_CHARACTER_SELECT",
                    message = "Not on character select screen"
                };
            }

            // Find character button
            var buttonContainer = screen.GetNode<Control>("CharSelectButtons/ButtonContainer");
            var buttons = UiHelper.FindAll<NCharacterSelectButton>(buttonContainer);
            
            var targetBtn = buttons.FirstOrDefault(b => 
            {
                var model = GetCharacterModel(b);
                return model?.Id.Entry.Equals(characterId, StringComparison.OrdinalIgnoreCase) == true;
            });

            if (targetBtn == null)
            {
                return new { 
                    ok = false, 
                    error = "CHARACTER_NOT_FOUND",
                    message = $"Character '{characterId}' not found"
                };
            }

            if (GetIsLocked(targetBtn))
            {
                return new { 
                    ok = false, 
                    error = "CHARACTER_LOCKED",
                    message = $"Character '{characterId}' is locked"
                };
            }

            // Click the button
            Logger.Info($"Selecting character: {characterId}");
            targetBtn.ForceClick();

            return new { 
                ok = true, 
                data = new { character_id = characterId }
            };
        });
    }
}
```

#### `STS2.Cli.Mod/Actions/SetAscensionHandler.cs`

**Purpose**: Set the ascension level.

```csharp
public static class SetAscensionHandler
{
    private static readonly ModLogger Logger = new("SetAscensionHandler");

    public static object Execute(int level)
    {
        return MainThreadExecutor.RunOnMainThread(() =>
        {
            // Guard: Must be on character select screen
            var screen = NCharacterSelectScreen.Instance;
            if (screen == null || !screen.IsInsideTree())
            {
                return new { 
                    ok = false, 
                    error = "NOT_IN_CHARACTER_SELECT",
                    message = "Not on character select screen"
                };
            }

            var ascensionPanel = screen.GetNode<NAscensionPanel>("%AscensionPanel");
            
            // Get max ascension
            var (_, maxAscension) = GetAscensionInfo(ascensionPanel);
            
            if (level < 0 || level > maxAscension)
            {
                return new { 
                    ok = false, 
                    error = "INVALID_ASCENSION_LEVEL",
                    message = $"Ascension level must be between 0 and {maxAscension}"
                };
            }

            // Set ascension level via reflection
            Logger.Info($"Setting ascension level to: {level}");
            SetAscensionLevel(ascensionPanel, level);

            return new { 
                ok = true, 
                data = new { ascension_level = level }
            };
        });
    }
}
```

#### `STS2.Cli.Mod/Actions/EmbarkHandler.cs`

**Purpose**: Click the Embark button to start the game.

```csharp
public static class EmbarkHandler
{
    private static readonly ModLogger Logger = new("EmbarkHandler");

    public static object Execute()
    {
        return MainThreadExecutor.RunOnMainThread(() =>
        {
            // Guard: Must be on character select screen
            var screen = NCharacterSelectScreen.Instance;
            if (screen == null || !screen.IsInsideTree())
            {
                return new { 
                    ok = false, 
                    error = "NOT_IN_CHARACTER_SELECT",
                    message = "Not on character select screen"
                };
            }

            // Check if character is selected
            var selectedBtn = GetSelectedButton(screen);
            if (selectedBtn == null)
            {
                return new { 
                    ok = false, 
                    error = "NO_CHARACTER_SELECTED",
                    message = "No character selected"
                };
            }

            // Find and click embark button
            var embarkBtn = screen.GetNode<NConfirmButton>("ConfirmButton");
            if (embarkBtn == null || !embarkBtn.IsEnabled)
            {
                return new { 
                    ok = false, 
                    error = "EMBARK_NOT_AVAILABLE",
                    message = "Embark button not available"
                };
            }

            Logger.Info("Clicking embark button");
            embarkBtn.ForceClick();

            return new { 
                ok = true, 
                data = new { embarked = true }
            };
        });
    }
}
```

---

## Phase 4: CLI Commands

### Modified Files

#### `STS2.Cli.Cmd/Program.cs`

Add commands:

```csharp
// sts2 select_character <character_id> — select a character
rootCommand.AddCommand(CreateSelectCharacterCommand(prettyOption));

// sts2 set_ascension <level> — set ascension level
rootCommand.AddCommand(CreateSetAscensionCommand(prettyOption));

// sts2 embark — start the game
rootCommand.AddCommand(CreateSimpleCommand("embark", "Start the game from character select", prettyOption));
```

Helper methods:
```csharp
private static Command CreateSelectCharacterCommand(Option<bool> prettyOption)
{
    var command = new Command("select_character", "Select a character");
    var characterIdArg = new Argument<string>("character_id", "Character identifier (e.g., ironclad, silent)");
    command.AddArgument(characterIdArg);
    command.SetHandler(async (string characterId) => {
        await CommandRunner.ExecuteSelectCharacterAsync(characterId);
    }, characterIdArg);
    return command;
}

private static Command CreateSetAscensionCommand(Option<bool> prettyOption)
{
    var command = new Command("set_ascension", "Set ascension level");
    var levelArg = new Argument<int>("level", "Ascension level (0-20)");
    command.AddArgument(levelArg);
    command.SetHandler(async (int level) => {
        await CommandRunner.ExecuteSetAscensionAsync(level);
    }, levelArg);
    return command;
}
```

---

## Phase 5: Server Routing

### Modified Files

#### `STS2.Cli.Mod/Server/PipeServer.cs`

Add routes in ProcessRequestAsync:

```csharp
"select_character" => HandleSelectCharacterRequest(request.Id),
"set_ascension" => HandleSetAscensionRequest(request.Args?[0] ?? 0),
"embark" => HandleEmbarkRequest(),
```

Add handler methods:
```csharp
private static object HandleSelectCharacterRequest(string? characterId)
{
    if (string.IsNullOrEmpty(characterId))
        return new { ok = false, error = "MISSING_ARGUMENT", message = "Character ID is required" };
    
    return SelectCharacterHandler.Execute(characterId);
}

private static object HandleSetAscensionRequest(int level)
{
    return SetAscensionHandler.Execute(level);
}

private static object HandleEmbarkRequest()
{
    return EmbarkHandler.Execute();
}
```

---

## Phase 6: Error Codes

### New Error Codes

| Error Code | Exit Code | Description |
|------------|-----------|-------------|
| `NOT_IN_CHARACTER_SELECT` | 2 | Not on character select screen |
| `CHARACTER_NOT_FOUND` | 3 | Specified character not found |
| `CHARACTER_LOCKED` | 2 | Character is locked |
| `INVALID_ASCENSION_LEVEL` | 3 | Ascension level out of range |
| `NO_CHARACTER_SELECTED` | 2 | No character selected when trying to embark |
| `EMBARK_NOT_AVAILABLE` | 2 | Embark button not available/enabled |

Add to `CommandRunner.cs`:
```csharp
"NOT_IN_CHARACTER_SELECT" or "CHARACTER_LOCKED" or "NO_CHARACTER_SELECTED" or "EMBARK_NOT_AVAILABLE" => ExitInvalidState,
"CHARACTER_NOT_FOUND" or "INVALID_ASCENSION_LEVEL" => ExitInvalidParam,
```

---

## Phase 7: Documentation

### Update `AGENTS.md`

Add to testing checklist:
```markdown
14. `sts2.exe select_character <character_id>` - confirms character selection
15. `sts2.exe set_ascension <level>` - confirms ascension level setting
16. `sts2.exe embark` - confirms game start from character select
```

### Update `docs/cli-reference.md`

Add sections for:
- `select_character` command
- `set_ascension` command
- `embark` command
- Character select state structure
- Error codes

---

## Usage Flow

```bash
# 1. User manually navigates to Character Select screen

# 2. Check available characters
$ sts2 state
{
  "screen": "CHARACTER_SELECT",
  "character_select": {
    "available_characters": [
      {"character_id": "ironclad", "character_name": "Ironclad", "is_locked": false, "is_selected": false},
      {"character_id": "silent", "character_name": "Silent", "is_locked": false, "is_selected": false},
      {"character_id": "defect", "character_name": "Defect", "is_locked": false, "is_selected": false}
    ],
    "selected_character": null,
    "current_ascension": 0,
    "max_ascension": 20,
    "can_embark": false
  }
}

# 3. Select character
$ sts2 select_character ironclad
{"ok": true, "data": {"character_id": "ironclad"}}

# 4. Set ascension (optional)
$ sts2 set_ascension 10
{"ok": true, "data": {"ascension_level": 10}}

# 5. Start game
$ sts2 embark
{"ok": true, "data": {"embarked": true}}

# 6. Handle Neow event (Ancient)
$ sts2 state
{
  "screen": "EVENT",
  "event": {
    "event_id": "NEOW",
    "layout_type": "Ancient",
    "is_in_dialogue": true
  }
}

$ sts2 advance_dialogue --auto
$ sts2 choose_event 0

# 7. Now on map
$ sts2 state
{"screen": "MAP"}
```

---

## Implementation Notes

### Reflection Requirements

Several fields/methods are private and require reflection:

1. **NCharacterSelectButton.CharacterModel** - Get private field
2. **NCharacterSelectButton.IsLocked** - Get private field or property
3. **NCharacterSelectScreen._selectedButton** - Get private field
4. **NAscensionPanel** current/max level - Get via properties

### Character IDs

Standard character IDs:
- `ironclad` - Ironclad
- `silent` - Silent (The Silent)
- `defect` - Defect
- `regent` - Regent
- `necrobinder` - Necrobinder (may be locked)

---

*Created: 2026-03-23*
*Version: v0.7.0*
