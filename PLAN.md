# Plan: Add Character, Gold, Potions, and Relics to Game State

## Current State
The current `PlayerStateDto` only contains combat stats (HP, Energy, Block, etc.) but lacks:
- Character class/hero information
- Gold amount
- Potions
- Relics

## Reference Implementation (STS2MCP)

Based on `McpMod.StateBuilder.cs`:

### Character Info
```csharp
state["character"] = SafeGetText(() => player.Character.Title);
// The Regent specific: stars counter
if (player.Character.ShouldAlwaysShowStarCounter || combatState.Stars > 0)
    state["stars"] = combatState.Stars;
```

### Gold
```csharp
state["gold"] = player.Gold;  // Direct property on Player
```

### Relics
```csharp
foreach (var relic in player.Relics)
{
    relics.Add(new Dictionary
    {
        ["id"] = relic.Id.Entry,
        ["name"] = SafeGetText(() => relic.Title),
        ["description"] = SafeGetText(() => relic.DynamicDescription),
        ["counter"] = relic.ShowCounter ? relic.DisplayAmount : null,
        ["keywords"] = BuildHoverTips(relic.HoverTipsExcludingRelic)
    });
}
```

### Potions
```csharp
int slotIndex = 0;
foreach (var potion in player.PotionSlots)
{
    if (potion != null)
    {
        potions.Add(new Dictionary
        {
            ["id"] = potion.Id.Entry,
            ["name"] = SafeGetText(() => potion.Title),
            ["description"] = SafeGetText(() => potion.DynamicDescription),
            ["slot"] = slotIndex,
            ["can_use_in_combat"] = potion.Usage == PotionUsage.CombatOnly || potion.Usage == PotionUsage.AnyTime,
            ["target_type"] = potion.TargetType.ToString(),
            ["keywords"] = BuildHoverTips(potion.ExtraHoverTips)
        });
    }
    slotIndex++;
}
```

## Proposed Changes

### 1. New DTOs to Create

#### PotionStateDto
```csharp
public class PotionStateDto
{
    /// <summary>Potion slot index (0-2)</summary>
    public int Slot { get; set; }
    
    /// <summary>Potion ID (e.g., "FirePotion")</summary>
    public string Id { get; set; } = "";
    
    /// <summary>Display name</summary>
    public string Name { get; set; } = "";
    
    /// <summary>Effect description with dynamic values resolved</summary>
    public string? Description { get; set; }
    
    /// <summary>Whether potion can be used in current context</summary>
    public bool CanUseInCombat { get; set; }
    
    /// <summary>Target type: Self, AnyEnemy, etc.</summary>
    public string? TargetType { get; set; }
}
```

#### RelicStateDto
```csharp
public class RelicStateDto
{
    /// <summary>Relic ID (e.g., "BurningBlood")</summary>
    public string Id { get; set; } = "";
    
    /// <summary>Display name</summary>
    public string Name { get; set; } = "";
    
    /// <summary>Effect description with dynamic values resolved</summary>
    public string? Description { get; set; }
    
    /// <summary>Counter value for relics that track counts (null if not applicable)</summary>
    public int? Counter { get; set; }
}
```

### 2. Update PlayerStateDto

Add new properties:
```csharp
public class PlayerStateDto
{
    // Existing properties...
    
    // Character
    public string? CharacterId { get; set; }     // Character class ID
    public string? CharacterName { get; set; }   // Character display name
    
    // The Regent specific resource
    public int? Stars { get; set; }
    
    // Gold
    public int Gold { get; set; }
    
    // Resources
    public List<PotionStateDto> Potions { get; set; } = new();
    public List<RelicStateDto> Relics { get; set; } = new();
}
```

### 3. Update PlayerStateBuilder

```csharp
public static PlayerStateDto Build(Player player)
{
    var state = new PlayerStateDto();

    try
    {
        var creature = player.Creature;
        var playerCombatState = player.PlayerCombatState;

        // Basic stats (existing)...
        
        // ===== NEW: Character Info =====
        state.CharacterId = player.Character?.Id.Entry;
        state.CharacterName = CleanGameText(player.Character?.Title?.GetFormattedText());
        
        // ===== NEW: Gold =====
        state.Gold = player.Gold;
        
        // ===== NEW: The Regent Stars (if applicable) =====
        if (playerCombatState != null && 
            (player.Character?.ShouldAlwaysShowStarCounter == true || playerCombatState.Stars > 0))
        {
            state.Stars = playerCombatState.Stars;
        }
        
        // ===== NEW: Relics =====
        foreach (var relic in player.Relics)
        {
            try
            {
                state.Relics.Add(new RelicStateDto
                {
                    Id = relic.Id.Entry,
                    Name = CleanGameText(relic.Title?.GetFormattedText()) ?? "Unknown",
                    Description = CleanGameText(relic.DynamicDescription?.GetFormattedText()),
                    Counter = relic.ShowCounter ? relic.DisplayAmount : null
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build relic state: {ex.Message}");
            }
        }
        
        // ===== NEW: Potions =====
        int slotIndex = 0;
        foreach (var potion in player.PotionSlots)
        {
            try
            {
                if (potion != null)
                {
                    state.Potions.Add(new PotionStateDto
                    {
                        Slot = slotIndex,
                        Id = potion.Id.Entry,
                        Name = CleanGameText(potion.Title?.GetFormattedText()) ?? "Unknown",
                        Description = CleanGameText(potion.DynamicDescription?.GetFormattedText()),
                        CanUseInCombat = potion.Usage == PotionUsage.CombatOnly || 
                                        potion.Usage == PotionUsage.AnyTime,
                        TargetType = potion.TargetType.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build potion state for slot {slotIndex}: {ex.Message}");
            }
            slotIndex++;
        }

        // Existing buffs extraction...
        state.Buffs = BuildBuffs(creature.Powers);
    }
    catch (Exception ex)
    {
        Logger.Warning($"Failed to build player state: {ex.Message}");
    }

    return state;
}
```

### 4. Game API Reference

| Property | Type | Description |
|----------|------|-------------|
| `player.Character` | `CharacterModel` | Hero class info |
| `player.Character.Id.Entry` | `string` | Character ID (e.g., "Ironclad") |
| `player.Character.Title` | `LocString` | Character display name |
| `player.Character.ShouldAlwaysShowStarCounter` | `bool` | The Regent specific |
| `player.Gold` | `int` | Current gold amount |
| `player.Relics` | `IEnumerable<Relic>` | All acquired relics |
| `relic.Id.Entry` | `string` | Relic ID |
| `relic.Title` | `LocString` | Relic name |
| `relic.DynamicDescription` | `SmartDescription` | Effect description |
| `relic.ShowCounter` | `bool` | Has counter display |
| `relic.DisplayAmount` | `int` | Counter value |
| `player.PotionSlots` | `IList<Potion?>` | Potion slots (null if empty) |
| `potion.Usage` | `PotionUsage` | CombatOnly/AnyTime/Automatic |
| `potion.TargetType` | `TargetType` | Self/AnyEnemy/etc |

### 5. Files to Modify

1. **New Files:**
   - `Models/Dto/PotionStateDto.cs`
   - `Models/Dto/RelicStateDto.cs`

2. **Modified Files:**
   - `Models/Dto/PlayerStateDto.cs` - Add new properties
   - `State/Builders/PlayerStateBuilder.cs` - Add extraction logic

## Implementation Notes

1. **Null Safety**: All nullable properties use `?.` operator with fallback
2. **Text Cleaning**: Use `CleanGameText()` to strip BBCode and format text
3. **Slot Indexing**: Potions must track their slot index for `use_potion` command
4. **Optional Stars**: Only include for The Regent or when stars > 0
5. **Error Handling**: Each potion/relic extraction wrapped in try-catch to prevent total failure

## Benefits

- Complete character build visibility for CLI users
- AI agents can make informed decisions (e.g., use potion based on relic synergy)
- Consistent with STS2MCP's comprehensive state extraction
- Supports all three characters (Ironclad, Silent, Defect, The Regent)
