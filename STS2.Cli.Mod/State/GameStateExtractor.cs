using System.Reflection;
using Godot;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.State;

/// <summary>
///     Extracts game state from Slay the Spire 2 using reflection.
///     Safely accesses game classes without hard dependencies.
/// </summary>
public static class GameStateExtractor
{
    private static readonly ModLogger Logger = new("GameStateExtractor");

    // Cache for Type lookups
    private static readonly Dictionary<string, Type?> TypeCache = new();
    private static readonly Dictionary<string, PropertyInfo?> PropertyCache = new();
    private static readonly Dictionary<string, FieldInfo?> FieldCache = new();

    /// <summary>
    ///     Gets the current game state.
    /// </summary>
    public static GameState GetState()
    {
        try
        {
            var state = new GameState
            {
                Screen = DetectScreen(),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            // Extract combat state if in combat
            if (state.Screen == "COMBAT")
            {
                state.Combat = ExtractCombatState();
            }

            return state;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract game state: {ex.Message}");
            return new GameState { Screen = "ERROR", Error = ex.Message };
        }
    }

    /// <summary>
    ///     Detects which screen the player is currently on.
    /// </summary>
    private static string DetectScreen()
    {
        // Try to find CombatManager or similar
        var combatManagerType = FindType("CombatManager", "BattleManager", "GameStateManager");
        if (combatManagerType != null)
        {
            var instance = GetStaticProperty(combatManagerType, "Instance");
            if (instance != null)
            {
                // Check if in combat by looking for combat-specific properties
                var inCombat = GetPropertyValue(instance, "InCombat", "IsInCombat", "CombatInProgress");
                if (inCombat is true)
                    return "COMBAT";
            }
        }

        // Try SceneManager for other screens
        var sceneManagerType = FindType("SceneManager", "ScreenManager", "GameManager");
        if (sceneManagerType != null)
        {
            var currentScene = GetStaticProperty(sceneManagerType, "CurrentScene", "ActiveScene");
            if (currentScene != null)
            {
                var sceneName = currentScene.ToString();
                return sceneName switch
                {
                    _ when sceneName.Contains("Map", StringComparison.OrdinalIgnoreCase) => "MAP",
                    _ when sceneName.Contains("Shop", StringComparison.OrdinalIgnoreCase) => "SHOP",
                    _ when sceneName.Contains("Event", StringComparison.OrdinalIgnoreCase) => "EVENT",
                    _ when sceneName.Contains("Rest", StringComparison.OrdinalIgnoreCase) => "REST",
                    _ when sceneName.Contains("Boss", StringComparison.OrdinalIgnoreCase) => "BOSS",
                    _ when sceneName.Contains("Treasure", StringComparison.OrdinalIgnoreCase) => "TREASURE",
                    _ => "UNKNOWN"
                };
            }
        }

        // Fallback: check if any combat-related objects exist
        if (IsCombatActive())
            return "COMBAT";

        return "UNKNOWN";
    }

    /// <summary>
    ///     Checks if combat is currently active.
    /// </summary>
    private static bool IsCombatActive()
    {
        // Look for combat scene or combat-related objects
        var combatTypes = new[] { "CombatManager", "BattleManager", "AbstractCreature", "AbstractMonster" };
        foreach (var typeName in combatTypes)
        {
            var type = FindType(typeName);
            if (type != null)
            {
                // Check if there are any active instances
                var instance = GetStaticProperty(type, "Instance");
                if (instance != null)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Extracts combat state including player, hand, and enemies.
    /// </summary>
    private static CombatState? ExtractCombatState()
    {
        try
        {
            var combatState = new CombatState();

            // Get CombatManager instance
            var combatManagerType = FindType("CombatManager", "BattleManager");
            if (combatManagerType == null)
            {
                Logger.Warning("CombatManager type not found");
                return null;
            }

            var combatManager = GetStaticProperty(combatManagerType, "Instance");
            if (combatManager == null)
            {
                Logger.Warning("CombatManager.Instance is null");
                return null;
            }

            // Extract player state
            combatState.Player = ExtractPlayerState(combatManager);

            // Extract hand
            combatState.Hand = ExtractHand(combatManager);

            // Extract enemies
            combatState.Enemies = ExtractEnemies(combatManager);

            // Extract turn info
            combatState.IsPlayerTurn = GetPropertyValue(combatManager, "IsPlayerTurn", "PlayerTurn") as bool? ?? true;
            combatState.TurnNumber = GetPropertyValue(combatManager, "TurnNumber", "Turn") as int? ?? 1;

            return combatState;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract combat state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Extracts player state from combat manager.
    /// </summary>
    private static PlayerState ExtractPlayerState(object combatManager)
    {
        var playerState = new PlayerState();

        try
        {
            // Get player from combat manager
            var player = GetPropertyValue(combatManager, "Player", "CurrentPlayer", "AbstractPlayer");
            if (player == null)
            {
                Logger.Warning("Player not found in CombatManager");
                return playerState;
            }

            // Extract HP
            playerState.Hp = GetPropertyValue(player, "CurrentHealth", "Health", "Hp") as int? ?? 0;
            playerState.MaxHp = GetPropertyValue(player, "MaxHealth", "MaxHp") as int? ?? 0;

            // Extract energy
            playerState.Energy = GetPropertyValue(player, "Energy", "CurrentEnergy") as int? ?? 0;
            playerState.MaxEnergy = GetPropertyValue(player, "MaxEnergy") as int? ?? 3;

            // Extract block
            playerState.Block = GetPropertyValue(player, "Block", "CurrentBlock") as int? ?? 0;

            // Extract buffs/powers
            playerState.Buffs = ExtractBuffs(player);

            // Extract deck/discard counts
            var deck = GetPropertyValue(player, "DrawPile", "Deck", "MasterDeck");
            if (deck is System.Collections.IEnumerable deckEnumerable)
            {
                playerState.DeckCount = deckEnumerable.Cast<object>().Count();
            }

            var discard = GetPropertyValue(player, "DiscardPile", "Discard");
            if (discard is System.Collections.IEnumerable discardEnumerable)
            {
                playerState.DiscardCount = discardEnumerable.Cast<object>().Count();
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract player state: {ex.Message}");
        }

        return playerState;
    }

    /// <summary>
    ///     Extracts hand cards from combat manager.
    /// </summary>
    private static List<CardState> ExtractHand(object combatManager)
    {
        var hand = new List<CardState>();

        try
        {
            // Get hand from player
            var player = GetPropertyValue(combatManager, "Player", "CurrentPlayer");
            if (player == null) return hand;

            var handObj = GetPropertyValue(player, "Hand", "HandGroup");
            if (handObj is not System.Collections.IEnumerable handEnumerable) return hand;

            var index = 0;
            foreach (var card in handEnumerable)
            {
                if (card == null) continue;

                var cardState = new CardState
                {
                    Index = index++,
                    Id = GetPropertyValue(card, "CardId", "ID", "Id")?.ToString() ?? "Unknown",
                    Name = GetPropertyValue(card, "Name", "CardName")?.ToString() ?? "Unknown",
                    Cost = GetPropertyValue(card, "Cost", "EnergyCost") as int? ?? -1,
                    CanPlay = GetPropertyValue(card, "CanPlay", "IsPlayable") as bool? ?? false,
                    Description = GetPropertyValue(card, "Description", "RawDescription")?.ToString() ?? ""
                };

                // Check if card has upgrades
                var upgraded = GetPropertyValue(card, "Upgraded", "IsUpgraded") as bool? ?? false;
                if (upgraded)
                    cardState.Id += "+";

                hand.Add(cardState);
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract hand: {ex.Message}");
        }

        return hand;
    }

    /// <summary>
    ///     Extracts enemy states from combat manager.
    /// </summary>
    private static List<EnemyState> ExtractEnemies(object combatManager)
    {
        var enemies = new List<EnemyState>();

        try
        {
            // Get enemies list
            var enemiesObj = GetPropertyValue(combatManager, "Enemies", "Monsters", "CombatEnemies");
            if (enemiesObj is not System.Collections.IEnumerable enemiesEnumerable) return enemies;

            var index = 0;
            foreach (var enemy in enemiesEnumerable)
            {
                if (enemy == null) continue;

                // Skip dead enemies
                var isDead = GetPropertyValue(enemy, "IsDead", "Dead", "IsDying") as bool? ?? false;
                if (isDead) continue;

                var enemyState = new EnemyState
                {
                    Index = index++,
                    Id = GetPropertyValue(enemy, "Id", "MonsterId")?.ToString() ?? "Unknown",
                    Name = GetPropertyValue(enemy, "Name", "MonsterName")?.ToString() ?? "Unknown",
                    Hp = GetPropertyValue(enemy, "CurrentHealth", "Health", "Hp") as int? ?? 0,
                    MaxHp = GetPropertyValue(enemy, "MaxHealth", "MaxHp") as int? ?? 0,
                    Block = GetPropertyValue(enemy, "Block", "CurrentBlock") as int? ?? 0,
                    IsMinion = GetPropertyValue(enemy, "IsMinion") as bool? ?? false,
                    Buffs = ExtractBuffs(enemy)
                };

                // Extract intent
                enemyState.Intent = ExtractIntent(enemy);

                enemies.Add(enemyState);
            }
        }
        catch (Exception ex)
                {
            Logger.Error($"Failed to extract enemies: {ex.Message}");
        }

        return enemies;
    }

    /// <summary>
    ///     Extracts enemy intent.
    /// </summary>
    private static IntentState ExtractIntent(object enemy)
    {
        var intentState = new IntentState();

        try
        {
            var intent = GetPropertyValue(enemy, "Intent", "CurrentIntent", "NextMove");
            if (intent == null) return intentState;

            intentState.Type = GetPropertyValue(intent, "Type", "IntentType", "MoveType")?.ToString() ?? "UNKNOWN";
            intentState.Damage = GetPropertyValue(intent, "Damage", "IntentDamage") as int? ?? 0;
            intentState.HitCount = GetPropertyValue(intent, "HitCount", "Multiplier", "Times") as int? ?? 1;
            intentState.Description = GetPropertyValue(intent, "Description", "IntentDescription")?.ToString() ?? "";
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to extract intent: {ex.Message}");
        }

        return intentState;
    }

    /// <summary>
    ///     Extracts buffs/powers from a creature.
    /// </summary>
    private static List<BuffState> ExtractBuffs(object creature)
    {
        var buffs = new List<BuffState>();

        try
        {
            var powersObj = GetPropertyValue(creature, "Powers", "Buffs", "PowerGroup");
            if (powersObj is not System.Collections.IEnumerable powersEnumerable) return buffs;

            foreach (var power in powersEnumerable)
            {
                if (power == null) continue;

                var buff = new BuffState
                {
                    Id = GetPropertyValue(power, "Id", "PowerId")?.ToString() ?? "Unknown",
                    Name = GetPropertyValue(power, "Name")?.ToString() ?? "Unknown",
                    Amount = GetPropertyValue(power, "Amount", "Stack", "Count") as int? ?? 1,
                    Description = GetPropertyValue(power, "Description")?.ToString() ?? ""
                };

                buffs.Add(buff);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to extract buffs: {ex.Message}");
        }

        return buffs;
    }

    #region Reflection Helpers

    /// <summary>
    ///     Finds a type by name from the game assembly.
    /// </summary>
    private static Type? FindType(params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            if (TypeCache.TryGetValue(name, out var cachedType))
                return cachedType;

            // Try to find in all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(name);
                if (type != null)
                {
                    TypeCache[name] = type;
                    return type;
                }

                // Try with namespaces
                type = assembly.GetTypes().FirstOrDefault(t =>
                    t.Name == name || t.FullName?.EndsWith($".{name}") == true);
                if (type != null)
                {
                    TypeCache[name] = type;
                    return type;
                }
            }

            TypeCache[name] = null;
        }

        return null;
    }

    /// <summary>
    ///     Gets a static property value.
    /// </summary>
    private static object? GetStaticProperty(Type type, params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            var cacheKey = $"{type.FullName}.{name}";
            if (!PropertyCache.TryGetValue(cacheKey, out var property))
            {
                property = type.GetProperty(name,
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                PropertyCache[cacheKey] = property;
            }

            if (property != null)
                return property.GetValue(null);
        }

        return null;
    }

    /// <summary>
    ///     Gets a property value from an object.
    /// </summary>
    private static object? GetPropertyValue(object obj, params string[] possibleNames)
    {
        var type = obj.GetType();

        foreach (var name in possibleNames)
        {
            var cacheKey = $"{type.FullName}.{name}";
            if (!PropertyCache.TryGetValue(cacheKey, out var property))
            {
                property = type.GetProperty(name,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                PropertyCache[cacheKey] = property;
            }

            if (property != null)
            {
                try
                {
                    return property.GetValue(obj);
                }
                catch
                {
                    // Property might throw, try next
                    continue;
                }
            }
        }

        return null;
    }

    #endregion
}

// State DTOs

public class GameState
{
    public string Screen { get; set; } = "UNKNOWN";
    public long Timestamp { get; set; }
    public string? Error { get; set; }
    public CombatState? Combat { get; set; }
}

public class CombatState
{
    public bool IsPlayerTurn { get; set; } = true;
    public int TurnNumber { get; set; } = 1;
    public PlayerState Player { get; set; } = new();
    public List<CardState> Hand { get; set; } = new();
    public List<EnemyState> Enemies { get; set; } = new();
}

public class PlayerState
{
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Energy { get; set; }
    public int MaxEnergy { get; set; } = 3;
    public int Block { get; set; }
    public int DeckCount { get; set; }
    public int DiscardCount { get; set; }
    public List<BuffState> Buffs { get; set; } = new();
}

public class CardState
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Cost { get; set; }
    public bool CanPlay { get; set; }
    public string Description { get; set; } = "";
}

public class EnemyState
{
    public int Index { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Block { get; set; }
    public bool IsMinion { get; set; }
    public IntentState Intent { get; set; } = new();
    public List<BuffState> Buffs { get; set; } = new();
}

public class IntentState
{
    public string Type { get; set; } = "UNKNOWN";
    public int Damage { get; set; }
    public int HitCount { get; set; } = 1;
    public string Description { get; set; } = "";
}

public class BuffState
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Amount { get; set; }
    public string Description { get; set; } = "";
}
