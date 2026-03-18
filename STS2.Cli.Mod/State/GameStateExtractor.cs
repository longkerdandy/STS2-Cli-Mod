using System.Reflection;
using Godot;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.State;

/// <summary>
///     Extracts game state from Slay the Spire 2 using reflection.
///     Based on actual decompiled class names from sts2.dll.
/// </summary>
public static class GameStateExtractor
{
    private static readonly ModLogger Logger = new("GameStateExtractor");

    // Cache for Type lookups
    private static readonly Dictionary<string, Type?> TypeCache = new();
    private static readonly Dictionary<string, PropertyInfo?> PropertyCache = new();
    private static readonly Dictionary<string, FieldInfo?> FieldCache = new();

    // Known type names from decompilation
    private const string CombatManagerType = "MegaCrit.Sts2.Core.Combat.CombatManager";
    private const string CombatStateType = "MegaCrit.Sts2.Core.Combat.CombatState";
    private const string CreatureType = "MegaCrit.Sts2.Core.Entities.Creatures.Creature";
    private const string PlayerType = "MegaCrit.Sts2.Core.Entities.Players.Player";
    private const string CardModelType = "MegaCrit.Sts2.Core.Entities.Cards.CardModel";

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
        // Check CombatManager.IsInProgress
        var combatManagerType = FindType(CombatManagerType);
        if (combatManagerType == null)
        {
            // Fallback to simple name
            combatManagerType = FindType("CombatManager");
        }

        if (combatManagerType != null)
        {
            var instance = GetStaticProperty(combatManagerType, "Instance");
            if (instance != null)
            {
                // IsInProgress indicates active combat
                var inProgress = GetPropertyValue(instance, "IsInProgress") as bool?;
                if (inProgress is true)
                    return "COMBAT";
            }
        }

        // TODO: Detect other screens (MAP, SHOP, EVENT, etc.)
        return "UNKNOWN";
    }

    /// <summary>
    ///     Extracts combat state from CombatManager.
    /// </summary>
    private static CombatState? ExtractCombatState()
    {
        try
        {
            var combatState = new CombatState();

            // Get CombatManager
            var combatManagerType = FindType(CombatManagerType, "CombatManager");
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

            // Get CombatState via DebugOnlyGetState() method
            var state = CallMethod(combatManager, "DebugOnlyGetState");
            if (state == null)
            {
                // Try to get _state field directly
                state = GetFieldValue(combatManager, "_state");
            }

            if (state == null)
            {
                Logger.Warning("CombatState is null");
                return null;
            }

            // Extract player state from CombatState.Players
            combatState.Player = ExtractPlayerState(state);

            // Extract hand
            combatState.Hand = ExtractHand(state);

            // Extract enemies from CombatState.Enemies
            combatState.Enemies = ExtractEnemies(state);

            // Turn info
            // IsPlayPhase = player can act
            combatState.IsPlayerTurn = GetPropertyValue(combatManager, "IsPlayPhase") as bool? ?? true;
            combatState.TurnNumber = GetPropertyValue(state, "RoundNumber") as int? ?? 1;

            return combatState;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract combat state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Extracts player state from CombatState.
    /// </summary>
    private static PlayerState ExtractPlayerState(object combatState)
    {
        var playerState = new PlayerState();

        try
        {
            // Get Players list from CombatState
            var players = GetPropertyValue(combatState, "Players") as System.Collections.IEnumerable;
            if (players == null)
            {
                Logger.Warning("Players list not found in CombatState");
                return playerState;
            }

            // Get first player (single player mode)
            var player = players.Cast<object>().FirstOrDefault();
            if (player == null)
            {
                Logger.Warning("No player found");
                return playerState;
            }

            // Player has a Creature property that contains combat stats
            var creature = GetPropertyValue(player, "Creature");
            if (creature != null)
            {
                // Creature stats: CurrentHp, MaxHp, Block
                playerState.Hp = GetPropertyValue(creature, "CurrentHp") as int? ?? 0;
                playerState.MaxHp = GetPropertyValue(creature, "MaxHp") as int? ?? 0;
                playerState.Block = GetPropertyValue(creature, "Block") as int? ?? 0;
            }

            // PlayerCombatState contains hand and energy
            var playerCombatState = GetPropertyValue(player, "PlayerCombatState");
            if (playerCombatState != null)
            {
                // Energy
                playerState.Energy = GetPropertyValue(playerCombatState, "Energy") as int? ?? 0;
                playerState.MaxEnergy = GetPropertyValue(playerCombatState, "MaxEnergy") as int? ?? 3;

                // Hand size (actual cards extracted separately)
                var hand = GetPropertyValue(playerCombatState, "Hand") as System.Collections.IEnumerable;
                if (hand != null)
                {
                    playerState.HandCount = hand.Cast<object>().Count();
                }

                // Draw pile
                var drawPile = GetPropertyValue(playerCombatState, "DrawPile") as System.Collections.IEnumerable;
                if (drawPile != null)
                {
                    playerState.DeckCount = drawPile.Cast<object>().Count();
                }

                // Discard pile
                var discardPile = GetPropertyValue(playerCombatState, "DiscardPile") as System.Collections.IEnumerable;
                if (discardPile != null)
                {
                    playerState.DiscardCount = discardPile.Cast<object>().Count();
                }
            }

            // Buffs/Powers from Creature
            if (creature != null)
            {
                var powers = GetFieldValue(creature, "_powers") as System.Collections.IEnumerable;
                if (powers != null)
                {
                    playerState.Buffs = ExtractBuffs(powers);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to extract player state: {ex.Message}");
        }

        return playerState;
    }

    /// <summary>
    ///     Extracts hand cards from player's combat state.
    /// </summary>
    private static List<CardState> ExtractHand(object combatState)
    {
        var hand = new List<CardState>();

        try
        {
            // Get Players list
            var players = GetPropertyValue(combatState, "Players") as System.Collections.IEnumerable;
            var player = players?.Cast<object>().FirstOrDefault();
            if (player == null) return hand;

            // Get PlayerCombatState
            var playerCombatState = GetPropertyValue(player, "PlayerCombatState");
            if (playerCombatState == null) return hand;

            // Get Hand
            var handObj = GetPropertyValue(playerCombatState, "Hand") as System.Collections.IEnumerable;
            if (handObj == null) return hand;

            var index = 0;
            foreach (var card in handObj)
            {
                if (card == null) continue;

                var cardState = new CardState
                {
                    Index = index++,
                    Id = GetCardId(card),
                    Name = GetPropertyValue(card, "Title")?.ToString() ?? "Unknown",
                    Cost = GetCardCost(card),
                    CanPlay = CanPlayCard(card, playerCombatState),
                    Description = GetCardDescription(card)
                };

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
    ///     Extracts enemy states from CombatState.Enemies.
    /// </summary>
    private static List<EnemyState> ExtractEnemies(object combatState)
    {
        var enemies = new List<EnemyState>();

        try
        {
            // Get Enemies list from CombatState
            var enemiesObj = GetPropertyValue(combatState, "Enemies") as System.Collections.IEnumerable;
            if (enemiesObj == null) return enemies;

            var index = 0;
            foreach (var creature in enemiesObj)
            {
                if (creature == null) continue;

                // Skip dead enemies
                var isAlive = GetPropertyValue(creature, "IsAlive") as bool? ?? false;
                if (!isAlive) continue;

                var enemyState = new EnemyState
                {
                    Index = index++,
                    Id = GetCreatureId(creature),
                    Name = GetPropertyValue(creature, "Name")?.ToString() ?? "Unknown",
                    Hp = GetPropertyValue(creature, "CurrentHp") as int? ?? 0,
                    MaxHp = GetPropertyValue(creature, "MaxHp") as int? ?? 0,
                    Block = GetPropertyValue(creature, "Block") as int? ?? 0,
                    IsMinion = GetPropertyValue(creature, "IsPet") as bool? ?? false,
                    Buffs = new List<BuffState>()
                };

                // Get powers/buffs
                var powers = GetFieldValue(creature, "_powers") as System.Collections.IEnumerable;
                if (powers != null)
                {
                    enemyState.Buffs = ExtractBuffs(powers);
                }

                // Extract intent from Monster
                enemyState.Intent = ExtractIntent(creature);

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
    private static IntentState ExtractIntent(object creature)
    {
        var intentState = new IntentState();

        try
        {
            // Check if this creature is a monster
            var isMonster = GetPropertyValue(creature, "IsMonster") as bool? ?? false;
            if (!isMonster) return intentState;

            // Get Monster model
            var monster = GetPropertyValue(creature, "Monster");
            if (monster == null) return intentState;

            // Get NextMove
            var nextMove = GetPropertyValue(monster, "NextMove");
            if (nextMove == null) return intentState;

            // Get Intents list
            var intents = GetPropertyValue(nextMove, "Intents") as System.Collections.IEnumerable;
            if (intents == null) return intentState;

            // Take first intent
            var intent = intents.Cast<object>().FirstOrDefault();
            if (intent == null) return intentState;

            // Intent properties
            intentState.Type = GetPropertyValue(intent, "Type")?.ToString() ?? "UNKNOWN";
            intentState.Damage = GetPropertyValue(intent, "Damage") as int? ?? 0;
            intentState.HitCount = GetPropertyValue(intent, "HitCount") as int? ?? 1;

            // Description from hover tip
            var description = GetPropertyValue(intent, "Description")?.ToString();
            if (string.IsNullOrEmpty(description))
            {
                // Try to generate from AbstractIntent
                var abstractIntent = GetPropertyValue(intent, "AbstractIntent");
                if (abstractIntent != null)
                {
                    description = GetPropertyValue(abstractIntent, "Description")?.ToString();
                }
            }
            intentState.Description = description ?? "";
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to extract intent: {ex.Message}");
        }

        return intentState;
    }

    /// <summary>
    ///     Extracts buffs from powers collection.
    /// </summary>
    private static List<BuffState> ExtractBuffs(System.Collections.IEnumerable powers)
    {
        var buffs = new List<BuffState>();

        try
        {
            foreach (var power in powers)
            {
                if (power == null) continue;

                var buff = new BuffState
                {
                    Id = GetPropertyValue(power, "Id")?.ToString() 
                        ?? GetPropertyValue(power, "PowerId")?.ToString() 
                        ?? "Unknown",
                    Name = GetPropertyValue(power, "Name")?.ToString() ?? "Unknown",
                    Amount = GetPropertyValue(power, "Amount") as int? 
                        ?? GetPropertyValue(power, "Stack") as int? ?? 1,
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

    #region Card Helpers

    private static string GetCardId(object card)
    {
        // Try different properties for card ID
        var id = GetPropertyValue(card, "Id")?.ToString()
            ?? GetPropertyValue(card, "CardId")?.ToString()
            ?? GetPropertyValue(card, "ID")?.ToString();

        if (string.IsNullOrEmpty(id))
        {
            // Try to get from CardModel
            var cardModel = GetPropertyValue(card, "Card");
            if (cardModel != null)
            {
                id = GetPropertyValue(cardModel, "Id")?.ToString();
            }
        }

        // Check if upgraded
        var upgraded = GetPropertyValue(card, "IsUpgraded") as bool? 
            ?? GetPropertyValue(card, "Upgraded") as bool? ?? false;

        if (upgraded && !string.IsNullOrEmpty(id) && !id.EndsWith("+"))
            id += "+";

        return id ?? "Unknown";
    }

    private static int GetCardCost(object card)
    {
        // Try Cost property
        var cost = GetPropertyValue(card, "Cost") as int?;
        if (cost.HasValue) return cost.Value;

        // Try EnergyCost
        cost = GetPropertyValue(card, "EnergyCost") as int?;
        if (cost.HasValue) return cost.Value;

        // Try CostForTurn (modified cost)
        cost = GetPropertyValue(card, "CostForTurn") as int?;
        if (cost.HasValue) return cost.Value;

        return -1;
    }

    private static bool CanPlayCard(object card, object playerCombatState)
    {
        // Try CanPlay property
        var canPlay = GetPropertyValue(card, "CanPlay") as bool?;
        if (canPlay.HasValue) return canPlay.Value;

        // Try IsPlayable
        canPlay = GetPropertyValue(card, "IsPlayable") as bool?;
        if (canPlay.HasValue) return canPlay.Value;

        // Check if has enough energy
        var cost = GetCardCost(card);
        if (cost >= 0)
        {
            var energy = GetPropertyValue(playerCombatState, "Energy") as int? ?? 0;
            return cost <= energy;
        }

        return true;
    }

    private static string GetCardDescription(object card)
    {
        var desc = GetPropertyValue(card, "Description")?.ToString()
            ?? GetPropertyValue(card, "RawDescription")?.ToString()
            ?? GetPropertyValue(card, "CardDescription")?.ToString();

        if (string.IsNullOrEmpty(desc))
        {
            // Try to get description from Card model
            var cardModel = GetPropertyValue(card, "Card");
            if (cardModel != null)
            {
                desc = GetPropertyValue(cardModel, "Description")?.ToString();
            }
        }

        return desc ?? "";
    }

    #endregion

    #region Creature Helpers

    private static string GetCreatureId(object creature)
    {
        // Try ModelId
        var modelId = GetPropertyValue(creature, "ModelId");
        if (modelId != null)
        {
            return modelId.ToString() ?? "Unknown";
        }

        // Try Monster.Id
        var monster = GetPropertyValue(creature, "Monster");
        if (monster != null)
        {
            var id = GetPropertyValue(monster, "Id")?.ToString();
            if (!string.IsNullOrEmpty(id))
                return id;
        }

        return "Unknown";
    }

    #endregion

    #region Reflection Helpers

    private static Type? FindType(params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            if (string.IsNullOrEmpty(name)) continue;

            if (TypeCache.TryGetValue(name, out var cachedType))
                return cachedType;

            // Try exact name first
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(name);
                    if (type != null)
                    {
                        TypeCache[name] = type;
                        return type;
                    }
                }
                catch
                {
                    continue;
                }
            }

            // Try partial match
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetTypes().FirstOrDefault(t =>
                        t.Name == name || t.FullName?.EndsWith($".{name}") == true);
                    if (type != null)
                    {
                        TypeCache[name] = type;
                        return type;
                    }
                }
                catch
                {
                    continue;
                }
            }

            TypeCache[name] = null;
        }

        return null;
    }

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
            {
                try
                {
                    return property.GetValue(null);
                }
                catch
                {
                    continue;
                }
            }
        }

        return null;
    }

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
                    continue;
                }
            }
        }

        return null;
    }

    private static object? GetFieldValue(object obj, string fieldName)
    {
        var type = obj.GetType();
        var cacheKey = $"{type.FullName}.{fieldName}";

        if (!FieldCache.TryGetValue(cacheKey, out var field))
        {
            field = type.GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            FieldCache[cacheKey] = field;
        }

        if (field != null)
        {
            try
            {
                return field.GetValue(obj);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static object? CallMethod(object obj, string methodName, params object[] args)
    {
        var type = obj.GetType();
        var method = type.GetMethod(methodName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

        if (method != null)
        {
            try
            {
                return method.Invoke(obj, args);
            }
            catch
            {
                return null;
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
    public int HandCount { get; set; }
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
