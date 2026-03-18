using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Executes game actions using direct type references.
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
            // Validate we're in combat
            if (!CombatManager.Instance.IsInProgress)
            {
                return new { ok = false, error = "NOT_IN_COMBAT", message = "Not currently in combat" };
            }

            // Get player
            var combatState = CombatManager.Instance.DebugOnlyGetState();
            if (combatState == null)
            {
                return new { ok = false, error = "NO_COMBAT_STATE", message = "Combat state is null" };
            }

            var player = combatState.Players.FirstOrDefault();
            if (player?.PlayerCombatState == null)
            {
                return new { ok = false, error = "NO_PLAYER", message = "Player not found" };
            }

            // Validate card index
            var hand = player.PlayerCombatState.Hand.Cards;
            if (cardIndex < 0 || cardIndex >= hand.Count)
            {
                return new { ok = false, error = "INVALID_CARD_INDEX", message = $"Card index {cardIndex} out of range (0-{hand.Count - 1})" };
            }

            var card = hand[cardIndex];

            // Check if can play
            card.CanPlay(out var unplayableReason, out _);
            if (unplayableReason != UnplayableReason.None)
            {
                return new { ok = false, error = "CANNOT_PLAY_CARD", message = $"Cannot play card: {unplayableReason}" };
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
            return new { ok = true, data = new { action = "PLAY_CARD", card_index = cardIndex, card_id = card.Id.Entry } };
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
            // Validate we're in combat and it's player turn
            var combatManager = CombatManager.Instance;
            if (!combatManager.IsInProgress)
            {
                return new { ok = false, error = "NOT_IN_COMBAT", message = "Not currently in combat" };
            }

            if (!combatManager.IsPlayPhase)
            {
                return new { ok = false, error = "NOT_PLAYER_TURN", message = "Not player's turn or cannot act now" };
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

        var combatManager = CombatManager.Instance;
        if (!combatManager.IsInProgress)
        {
            Logger.Error("Combat not in progress");
            return;
        }

        var combatState = combatManager.DebugOnlyGetState();
        if (combatState == null)
        {
            Logger.Error("Combat state is null");
            return;
        }

        var player = combatState.Players.FirstOrDefault();
        if (player?.PlayerCombatState == null)
        {
            Logger.Error("Player not found");
            return;
        }

        var hand = player.PlayerCombatState.Hand.Cards;
        if (cardIndex < 0 || cardIndex >= hand.Count)
        {
            Logger.Error($"Invalid card index: {cardIndex}");
            return;
        }

        var card = hand[cardIndex];
        
        // Use the PlayCard method on PlayerCombatState or card
        // In STS2, this might be done via commands or direct method calls
        // For now, we log it - actual implementation needs game-specific logic
        Logger.Info($"Would play card: {card.Title} (implementation pending)");

        // TODO: Implement actual card playing
        // This typically involves:
        // 1. Creating a PlayCardGameAction
        // 2. Adding it to the action queue
        // 3. Or calling a method like player.PlayerCombatState.PlayCard(card)
    }

    /// <summary>
    ///     Directly ends the turn.
    /// </summary>
    private static void ExecuteEndTurn()
    {
        Logger.Info("Executing end turn");

        var combatManager = CombatManager.Instance;
        if (!combatManager.IsInProgress)
        {
            Logger.Error("Combat not in progress");
            return;
        }

        // In STS2, ending turn might be done via:
        // 1. CombatManager method
        // 2. Player method
        // 3. GameAction queue
        
        // TODO: Implement actual end turn
        // Possible approaches:
        // - combatManager.EndTurn()
        // - player.EndTurn()
        // - ActionQueue.Add(new EndTurnAction())
        
        Logger.Info("End turn implementation pending");
    }
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
