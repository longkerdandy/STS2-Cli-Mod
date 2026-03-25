using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles play card action using the game's native ActionQueue.
///     After enqueuing the action, waits for completion and collects execution results
///     (damage dealt, block gained, powers applied) from <c>CombatHistory</c>.
/// </summary>
public static class PlayCardHandler
{
    private static readonly ModLogger Logger = new("PlayCardHandler");

    /// <summary>
    ///     Handles the play_card request.
    ///     Validates parameters and delegates to ExecuteAsync.
    /// </summary>
    public static async Task<object> HandleRequestAsync(Request request)
    {
        if (string.IsNullOrEmpty(request.Id))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Card ID required (e.g., STRIKE_IRONCLAD)" };

        var nthValue = request.Nth ?? 0;
        Logger.Info(
            $"Requested to play card {request.Id}, nth={nthValue}, target={request.Target?.ToString() ?? "none"}");

        return await ExecuteAsync(request.Id, nthValue, request.Target);
    }

    /// <summary>
    ///     Plays a card from the player's hand by ID and returns the execution results.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    /// <param name="cardId">Card ID to play (e.g., "STRIKE_IRONCLAD").</param>
    /// <param name="nth">N-th occurrence when multiple copies exist (0-based).</param>
    /// <param name="targetCombatId">Optional target combat ID for targeted cards.</param>
    private static async Task<object> ExecuteAsync(string cardId, int nth = 0, int? targetCombatId = null)
    {
        try
        {
            // --- Validation (synchronous, single frame) ---

            var combatError = ActionUtils.ValidateCombatReady();
            if (combatError != null) return combatError;

            var player = ActionUtils.GetLocalPlayer();
            if (player?.PlayerCombatState == null)
                return new { ok = false, error = "NO_PLAYER", message = "Player not found or not in combat" };

            if (!player.Creature.IsAlive)
                return new { ok = false, error = "PLAYER_DEAD", message = "Player is dead - cannot play cards" };

            var hand = player.PlayerCombatState.Hand;

            // Find card by ID
            var (card, cardIndex, findError) = FindCardById(hand, cardId, nth);
            if (findError != null)
                return findError;

            if (!card.CanPlay(out var reason, out _))
                return new
                {
                    ok = false, error = "CANNOT_PLAY_CARD", message = $"Card '{card.Title}' cannot be played: {reason}"
                };

            // Resolve target
            Creature? target = null;
            if (card.TargetType == TargetType.AnyEnemy)
            {
                if (targetCombatId == null)
                    return new
                    {
                        ok = false, error = "TARGET_REQUIRED",
                        message = "Card requires a target. Provide 'target' with an enemy combat_id."
                    };

                target = ActionUtils.ResolveEnemyTarget((uint)targetCombatId.Value);
                if (target == null)
                    return new
                    {
                        ok = false, error = "TARGET_NOT_FOUND",
                        message = $"No hittable enemy found with combat_id {targetCombatId}"
                    };
            }
            else if (targetCombatId != null)
            {
                return new
                {
                    ok = false, error = "TARGET_NOT_ALLOWED",
                    message = $"Card '{card.Title}' has target type '{card.TargetType}' and does not accept a target"
                };
            }

            // --- Enqueue action and wait for completion ---

            // Snapshot history count before the action executes
            var historyBefore = CombatManager.Instance.History.Entries.Count();

            var action = new PlayCardAction(card, target);

            var targetName = target?.Monster?.Title.GetFormattedText();
            var targetMsg = targetName != null ? $" targeting {targetName}" : "";
            Logger.Info($"PlayCardAction enqueued: '{card.Title}'{targetMsg}");

            var finalState = await ActionUtils.EnqueueAndAwaitAsync(action, ActionUtils.ActionTimeoutMs);
            if (finalState == null)
            {
                Logger.Warning("PlayCardAction timed out waiting for completion");
                return new { ok = false, error = "TIMEOUT", message = "Card action did not complete in time" };
            }

            if (finalState == GameActionState.Canceled)
            {
                Logger.Info("PlayCardAction was cancelled by the game");
                return new
                {
                    ok = false, error = "ACTION_CANCELLED",
                    message = $"Card '{card.Title}' action was cancelled by the game"
                };
            }

            // --- Collect results from CombatHistory ---

            var results = CombatHistoryBuilder.BuildFromHistory(historyBefore);
            Logger.Info($"PlayCardAction completed with {results.Count} result entries");

            return new
            {
                ok = true,
                data = new
                {
                    card_index = cardIndex,
                    card_id = card.Id.Entry,
                    target = targetCombatId,
                    results
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to play card: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Finds a card in the hand by ID and nth occurrence.
    /// </summary>
    /// <param name="hand">The player's hand (CardPile).</param>
    /// <param name="cardId">Card ID to find.</param>
    /// <param name="nth">N-th occurrence (0-based).</param>
    /// <returns>Tuple of (card, cardIndex, error). If the error is not null, card and cardIndex are invalid.</returns>
    private static (CardModel Card, int Index, object? Error) FindCardById(CardPile hand, string cardId, int nth)
    {
        // Find all matching cards
        var matchingCards = new List<(CardModel Card, int Index)>();
        for (var i = 0; i < hand.Cards.Count; i++)
            if (hand.Cards[i].Id.Entry.Equals(cardId, StringComparison.OrdinalIgnoreCase))
                matchingCards.Add((hand.Cards[i], i));

        if (matchingCards.Count == 0)
        {
            // Build a list of available card IDs for the error message
            var availableIds = hand.Cards.Select(c => c.Id.Entry).Distinct().ToList();
            var availableStr = string.Join(", ", availableIds);
            Logger.Warning($"Card '{cardId}' not found in hand. Available: {availableStr}");

            return (null!, 0, new
            {
                ok = false,
                error = "CARD_NOT_FOUND",
                message = $"Card '{cardId}' not found in hand. Available cards: {availableStr}"
            });
        }

        if (nth < 0 || nth >= matchingCards.Count)
        {
            Logger.Warning($"Card '{cardId}' has {matchingCards.Count} copies, but nth={nth} was requested");
            return (null!, 0, new
            {
                ok = false,
                error = "INVALID_CARD_INDEX",
                message =
                    $"Card '{cardId}' has {matchingCards.Count} copies in hand. Use nth from 0 to {matchingCards.Count - 1}."
            });
        }

        var selected = matchingCards[nth];
        Logger.Info(
            $"Found card '{cardId}' at hand index {selected.Index} (nth={nth}, total matches={matchingCards.Count})");

        return (selected.Card, selected.Index, null);
    }
}