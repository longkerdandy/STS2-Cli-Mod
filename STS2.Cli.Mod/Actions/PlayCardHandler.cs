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
    public static object Execute(int cardIndex, string? targetId = null)
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
                if (string.IsNullOrEmpty(targetId))
                    return new
                    {
                        ok = false, error = "TARGET_REQUIRED",
                        message = "Card requires a target. Provide 'target' with an entity_id."
                    };

                target = ResolveTarget(targetId);
                if (target == null)
                    return new
                    {
                        ok = false, error = "TARGET_NOT_FOUND",
                        message = $"Target '{targetId}' not found among alive enemies"
                    };
            }

            // Enqueue the play card action via the game's ActionQueue
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(
                new MegaCrit.Sts2.Core.GameActions.PlayCardAction(card, target));

            var targetName = target?.Monster?.Title.GetFormattedText() ?? "enemy";
            var targetMsg = target != null ? $" targeting {targetName}" : "";
            Logger.Info($"Enqueued PlayCardAction: '{card.Title}'{targetMsg}");

            return new
            {
                ok = true,
                data = new { action = "PLAY_CARD", card_index = cardIndex, card_id = card.Id.Entry, target = targetId }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to play card: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Resolves a target creature by entity ID.
    /// </summary>
    private static Creature? ResolveTarget(string entityId)
    {
        try
        {
            var combatState = CombatManager.Instance.DebugOnlyGetState();
            if (combatState == null)
                return null;

            // Try to parse as combat ID (numeric)
            if (uint.TryParse(entityId, out var combatId)) return combatState.GetCreature(combatId);

            // Try to match by entity_id pattern (e.g., "jaw_worm_0")
            var entityCounts = new Dictionary<string, int>();
            foreach (var creature in combatState.Enemies)
            {
                if (!creature.IsAlive)
                    continue;

                var baseId = creature.Monster?.Id.Entry ?? "unknown";
                var count = entityCounts.GetValueOrDefault(baseId, 0);

                entityCounts[baseId] = count + 1;
                var generatedId = $"{baseId}_{count}";

                if (generatedId == entityId)
                    return creature;
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to resolve target '{entityId}': {ex.Message}");
            return null;
        }
    }
}