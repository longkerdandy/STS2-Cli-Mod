using System.Reflection;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Executes game actions by calling game methods via reflection.
/// </summary>
public static class ActionExecutor
{
    private static readonly ModLogger Logger = new("ActionExecutor");

    // Pending actions queue for Harmony patches to process
    private static readonly Queue<GameAction> PendingActions = new();
    private static readonly object LockObj = new();

    /// <summary>
    ///     Queues a play card action.
    /// </summary>
    public static object QueuePlayCard(int cardIndex)
    {
        try
        {
            // Validate card index
            var state = GameStateExtractor.GetState();
            if (state.Screen != "COMBAT" || state.Combat == null)
            {
                return new { ok = false, error = "NOT_IN_COMBAT", message = "Not currently in combat" };
            }

            if (cardIndex < 0 || cardIndex >= state.Combat.Hand.Count)
            {
                return new { ok = false, error = "INVALID_CARD_INDEX", message = $"Card index {cardIndex} out of range (0-{state.Combat.Hand.Count - 1})" };
            }

            var card = state.Combat.Hand[cardIndex];
            if (!card.CanPlay)
            {
                return new { ok = false, error = "CANNOT_PLAY_CARD", message = "Card cannot be played (insufficient energy or other restriction)" };
            }

            lock (LockObj)
            {
                PendingActions.Enqueue(new GameAction
                {
                    Type = ActionType.PlayCard,
                    CardIndex = cardIndex
                });
            }

            Logger.Info($"Queued play card action: index={cardIndex}, card={card.Id}");
            return new { ok = true, data = new { action = "PLAY_CARD", card_index = cardIndex, card_id = card.Id } };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to queue play card: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Queues an end turn action.
    /// </summary>
    public static object QueueEndTurn()
    {
        try
        {
            // Validate in combat
            var state = GameStateExtractor.GetState();
            if (state.Screen != "COMBAT" || state.Combat == null)
            {
                return new { ok = false, error = "NOT_IN_COMBAT", message = "Not currently in combat" };
            }

            if (!state.Combat.IsPlayerTurn)
            {
                return new { ok = false, error = "NOT_PLAYER_TURN", message = "Not player's turn" };
            }

            lock (LockObj)
            {
                PendingActions.Enqueue(new GameAction
                {
                    Type = ActionType.EndTurn
                });
            }

            Logger.Info("Queued end turn action");
            return new { ok = true, data = new { action = "END_TURN" } };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to queue end turn: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Gets and clears pending actions.
    ///     Called by Harmony patch to process actions on main thread.
    /// </summary>
    public static List<GameAction> GetPendingActions()
    {
        lock (LockObj)
        {
            var actions = PendingActions.ToList();
            PendingActions.Clear();
            return actions;
        }
    }

    /// <summary>
    ///     Executes pending actions immediately.
    ///     Should be called from Harmony patch on main thread.
    /// </summary>
    public static void ExecutePendingActions()
    {
        var actions = GetPendingActions();
        foreach (var action in actions)
        {
            try
            {
                switch (action.Type)
                {
                    case ActionType.PlayCard:
                        ExecutePlayCard(action.CardIndex);
                        break;
                    case ActionType.EndTurn:
                        ExecuteEndTurn();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to execute action {action.Type}: {ex.Message}");
            }
        }
    }

    /// <summary>
    ///     Directly plays a card by index.
    /// </summary>
    private static void ExecutePlayCard(int cardIndex)
    {
        Logger.Info($"Executing play card: index={cardIndex}");

        // Find CombatManager
        var combatManagerType = FindType("CombatManager", "BattleManager");
        if (combatManagerType == null)
        {
            Logger.Error("CombatManager not found");
            return;
        }

        var combatManager = GetStaticProperty(combatManagerType, "Instance");
        if (combatManager == null)
        {
            Logger.Error("CombatManager.Instance is null");
            return;
        }

        // Try to find PlayCard method
        var playCardMethod = combatManagerType.GetMethod("PlayCard",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(int) }, null);

        if (playCardMethod != null)
        {
            playCardMethod.Invoke(combatManager, new object[] { cardIndex });
            Logger.Info("PlayCard method invoked successfully");
            return;
        }

        // Alternative: Try to get hand and play directly
        var player = GetPropertyValue(combatManager, "Player", "CurrentPlayer");
        if (player == null)
        {
            Logger.Error("Player not found");
            return;
        }

        var hand = GetPropertyValue(player, "Hand", "HandGroup") as System.Collections.IEnumerable;
        var card = hand?.Cast<object>().ElementAtOrDefault(cardIndex);
        if (card == null)
        {
            Logger.Error($"Card at index {cardIndex} not found");
            return;
        }

        // Try to use card's Use method
        var useMethod = card.GetType().GetMethod("Use",
            BindingFlags.Public | BindingFlags.Instance);
        if (useMethod != null)
        {
            useMethod.Invoke(card, new object?[] { null, null }); // target, source
            Logger.Info("Card.Use method invoked");
        }
        else
        {
            Logger.Error("Could not find method to play card");
        }
    }

    /// <summary>
    ///     Directly ends the turn.
    /// </summary>
    private static void ExecuteEndTurn()
    {
        Logger.Info("Executing end turn");

        // Find CombatManager
        var combatManagerType = FindType("CombatManager", "BattleManager");
        if (combatManagerType == null)
        {
            Logger.Error("CombatManager not found");
            return;
        }

        var combatManager = GetStaticProperty(combatManagerType, "Instance");
        if (combatManager == null)
        {
            Logger.Error("CombatManager.Instance is null");
            return;
        }

        // Try EndTurn method
        var endTurnMethod = combatManagerType.GetMethod("EndTurn",
            BindingFlags.Public | BindingFlags.Instance);

        if (endTurnMethod != null)
        {
            endTurnMethod.Invoke(combatManager, null);
            Logger.Info("EndTurn method invoked successfully");
            return;
        }

        // Alternative: EndPlayerTurn
        endTurnMethod = combatManagerType.GetMethod("EndPlayerTurn",
            BindingFlags.Public | BindingFlags.Instance);

        if (endTurnMethod != null)
        {
            endTurnMethod.Invoke(combatManager, null);
            Logger.Info("EndPlayerTurn method invoked successfully");
            return;
        }

        Logger.Error("Could not find EndTurn method");
    }

    #region Reflection Helpers

    private static Type? FindType(params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(name);
                if (type != null) return type;

                type = assembly.GetTypes().FirstOrDefault(t =>
                    t.Name == name || t.FullName?.EndsWith($".{name}") == true);
                if (type != null) return type;
            }
        }
        return null;
    }

    private static object? GetStaticProperty(Type type, params string[] possibleNames)
    {
        foreach (var name in possibleNames)
        {
            var property = type.GetProperty(name,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (property != null)
                return property.GetValue(null);
        }
        return null;
    }

    private static object? GetPropertyValue(object obj, params string[] possibleNames)
    {
        var type = obj.GetType();
        foreach (var name in possibleNames)
        {
            var property = type.GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
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

    #endregion
}

/// <summary>
///     Represents a queued game action.
/// </summary>
public class GameAction
{
    public ActionType Type { get; set; }
    public int CardIndex { get; set; }
}

public enum ActionType
{
    PlayCard,
    EndTurn
}
