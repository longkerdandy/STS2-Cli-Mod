using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
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
            var (target, targetError) = ActionUtils.ResolveTarget(
                player, card.TargetType, targetCombatId, card.Title.ToString());
            if (targetError != null)
                return targetError;

            // --- Enqueue action and wait for completion ---

            // Snapshot history count before the action executes
            var historyBefore = CombatManager.Instance.History.Entries.Count();

            var action = new PlayCardAction(card, target);

            var targetName = target?.Monster?.Title.GetFormattedText();
            var targetMsg = targetName != null ? $" targeting {targetName}" : "";
            Logger.Info($"PlayCardAction enqueued: '{card.Title}'{targetMsg}");

            // Start the action and keep the Task reference for the fallback path
            var enqueueTask = ActionUtils.EnqueueAndAwaitAsync(action, ActionUtils.ActionTimeoutMs);

            // Poll for either card selection screen appearance or action completion.
            // Cards like Discovery, Quasar, and Splash open an NChooseACardSelectionScreen
            // during execution; the action pauses in GatheringPlayerChoice state until the
            // player picks a card. Without this detection, EnqueueAndAwaitAsync would timeout.
            var elapsedMs = 0;
            while (elapsedMs < ActionUtils.UiTimeoutMs)
            {
                await Task.Delay(ActionUtils.DefaultPollIntervalMs);
                elapsedMs += ActionUtils.DefaultPollIntervalMs;

                var selectionScreen = UiUtils.FindCardSelectionScreen();
                if (selectionScreen != null)
                {
                    Logger.Info($"Card selection screen detected for card '{card.Title}'");
                    return BuildCardSelectionResponse(card, cardIndex, targetCombatId, selectionScreen);
                }

                // Action finished normally before any selection screen appeared
                if (action.State is not (GameActionState.WaitingForExecution or GameActionState.Executing
                    or GameActionState.GatheringPlayerChoice))
                    break;
            }

            // Normal path: await the enqueue task for final state
            var finalState = await enqueueTask;
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

    /// <summary>
    ///     Builds a <c>selection_required</c> response when a played card opens
    ///     an <see cref="NChooseACardSelectionScreen" /> (e.g., Discovery, Quasar, Splash).
    ///     The agent should then use <c>tri_select_card</c> or <c>tri_select_skip</c>
    ///     to complete the selection.
    /// </summary>
    private static object BuildCardSelectionResponse(
        CardModel card, int cardIndex, int? targetCombatId, NChooseACardSelectionScreen selectionScreen)
    {
        var cards = UiUtils.ExtractSelectableCards(selectionScreen);
        var canSkip = ReadCanSkip(selectionScreen);

        return new
        {
            ok = true,
            data = new
            {
                status = "selection_required",
                card_index = cardIndex,
                card_id = card.Id.Entry,
                target = targetCombatId,
                min_select = canSkip ? 0 : 1,
                max_select = 1,
                can_skip = canSkip,
                cards
            }
        };
    }

    /// <summary>
    ///     Reads the private <c>_canSkip</c> field from an <see cref="NChooseACardSelectionScreen" />
    ///     via reflection to determine if the selection can be skipped.
    /// </summary>
    private static bool ReadCanSkip(NChooseACardSelectionScreen screen)
    {
        try
        {
            var field = typeof(NChooseACardSelectionScreen).GetField("_canSkip",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(screen) as bool? ?? false;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read _canSkip from screen: {ex.Message}");
            return false;
        }
    }
}