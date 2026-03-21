using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles play card action using the game's native ActionQueue.
/// </summary>
public static class PlayCardHandler
{
    private static readonly ModLogger Logger = new("PlayCardAction");

    /// <summary>
    ///     Plays a card from the player's hand.
    /// </summary>
    public static object Execute(int cardIndex, int? targetCombatId = null)
    {
        try
        {
            // Validate combat state
            if (!CombatManager.Instance.IsInProgress)
                return new { ok = false, error = "NOT_IN_COMBAT", message = "Not currently in combat" };

            if (!CombatManager.Instance.IsPlayPhase)
                return new
                {
                    ok = false, error = "NOT_PLAYER_TURN", message = "Not in play phase - cannot act during enemy turn"
                };

            if (CombatManager.Instance.PlayerActionsDisabled)
                return new
                {
                    ok = false, error = "ACTIONS_DISABLED", message = "Player actions are currently disabled"
                };

            // Get player
            var player = ActionUtils.GetLocalPlayer();
            if (player?.PlayerCombatState == null)
                return new { ok = false, error = "NO_PLAYER", message = "Player not found or not in combat" };

            if (!player.Creature.IsAlive)
                return new { ok = false, error = "PLAYER_DEAD", message = "Player is dead - cannot play cards" };

            // Validate card index
            var hand = player.PlayerCombatState.Hand;
            if (cardIndex < 0 || cardIndex >= hand.Cards.Count)
                return new
                {
                    ok = false, error = "INVALID_CARD_INDEX",
                    message = $"Card index {cardIndex} out of range (hand has {hand.Cards.Count} cards)"
                };

            var card = hand.Cards[cardIndex];

            // Check if the card can be played
            if (!card.CanPlay(out var reason, out _))
                return new
                {
                    ok = false, error = "CANNOT_PLAY_CARD", message = $"Card '{card.Title}' cannot be played: {reason}"
                };

            // Resolve the target if needed
            Creature? target = null;
            if (card.TargetType == TargetType.AnyEnemy)
            {
                if (targetCombatId == null)
                    return new
                    {
                        ok = false, error = "TARGET_REQUIRED",
                        message = "Card requires a target. Provide 'target' with an enemy combat_id."
                    };

                target = ResolveTarget((uint)targetCombatId.Value);
                if (target == null)
                    return new
                    {
                        ok = false, error = "TARGET_NOT_FOUND",
                        message = $"No alive enemy found with combat_id {targetCombatId}"
                    };
            }

            // Execute play card action via the game's action queue
            // This is the same path used by the game's own UI
            try
            {
                // Check if we're still in a valid state before enqueueing
                if (!CombatManager.Instance.IsInProgress)
                    return new { ok = false, error = "COMBAT_ENDED", message = "Combat ended before card could be played" };
                
                // Log state before playing
                var handCountBefore = player.PlayerCombatState.Hand.Cards.Count;
                var energyBefore = player.PlayerCombatState.Energy;
                Logger.Info($"Before play: Hand={handCountBefore}, Energy={energyBefore}");
                
                // Create and enqueue the PlayCardAction
                var action = new MegaCrit.Sts2.Core.GameActions.PlayCardAction(card, target);
                RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);
                
                Logger.Info($"PlayCardAction enqueued: {card.Title}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to enqueue PlayCardAction: {ex.Message}");
                Logger.Error($"Stack trace: {ex.StackTrace}");
                throw;
            }

            var targetName = target?.Monster?.Title.GetFormattedText() ?? "enemy";
            var targetMsg = target != null ? $" targeting {targetName}" : "";
            Logger.Info($"Requested to play card: '{card.Title}'{targetMsg}");

            return new
            {
                ok = true,
                data = new { action = "PLAY_CARD", card_index = cardIndex, card_id = card.Id.Entry, target = targetCombatId }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to play card: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Resolves a target creature by combat ID using the game's native lookup.
    ///     Returns null if the creature is not found or is dead.
    /// </summary>
    private static Creature? ResolveTarget(uint combatId)
    {
        try
        {
            var combatState = CombatManager.Instance.DebugOnlyGetState();
            if (combatState == null)
                return null;

            // Use the game's native lookup by CombatId
            var creature = combatState.GetCreature(combatId);
            if (creature == null)
            {
                Logger.Warning($"No creature found with combat_id {combatId}");
                return null;
            }

            // Verify the creature is a living enemy
            if (!creature.IsAlive)
            {
                Logger.Warning($"Creature with combat_id {combatId} is dead");
                return null;
            }

            if (creature.Side != CombatSide.Enemy)
            {
                Logger.Warning($"Creature with combat_id {combatId} is not an enemy (side={creature.Side})");
                return null;
            }

            Logger.Info($"Resolved target by combat_id: {combatId} -> {creature.Monster!.Title.GetFormattedText()}");
            return creature;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to resolve target with combat_id {combatId}: {ex.Message}");
            return null;
        }
    }
}
