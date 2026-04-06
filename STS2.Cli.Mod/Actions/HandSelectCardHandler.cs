using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using STS2.Cli.Mod.Actions.Utils;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.State.Builders;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>hand_select_card</c> and <c>hand_confirm_selection</c> CLI commands.
///     Selects cards from hand during combat (e.g., discard, exhaust, upgrade prompts)
///     or confirms the current hand selection.
///     Unlike deck/potion selection, hand selection happens inline in <see cref="NPlayerHand" />
///     without overlay screens.
/// </summary>
/// <remarks>
///     <para>
///         <b>CLI commands:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <c>sts2 hand_select_card &lt;card_id&gt; [&lt;card_id&gt;...] [--nth &lt;n&gt;...]</c>
///         </item>
///         <item>
///             <c>sts2 hand_confirm_selection</c>
///         </item>
///     </list>
///     <para><b>Scene:</b> Combat, when a card or effect requires the player to select cards from hand.</para>
/// </remarks>
public static class HandSelectCardHandler
{
    private static readonly ModLogger Logger = new("HandSelectCardHandler");

    /// <summary>
    ///     Selects cards from the hand during hand selection mode.
    ///     Must be called on the Godot main thread (via RunOnMainThreadAsync from PipeServer).
    /// </summary>
    /// <param name="request">The request containing card IDs and optional nth values.</param>
    /// <returns>Response object indicating success or failure.</returns>
    public static async Task<object> ExecuteAsync(Request request)
    {
        if (request.CardIds == null || request.CardIds.Length == 0)
        {
            // Single card mode: use Id field
            if (request.Id != null)
            {
                var cardIds = new[] { request.Id };
                var nthValues = request.Nth.HasValue ? new[] { request.Nth.Value } : null;
                Logger.Info("Requested to select 1 card from hand selection");
                return await ExecuteSelectAsync(cardIds, nthValues);
            }

            Logger.Warning("hand_select_card requested with no card IDs");
            return new { ok = false, error = "MISSING_ARGUMENT", message = "At least one card ID is required" };
        }

        Logger.Info($"Requested to select {request.CardIds.Length} card(s) from hand selection");
        return await ExecuteSelectAsync(request.CardIds, request.NthValues);
    }

    /// <summary>
    ///     Confirms the current hand selection by clicking the confirm button.
    ///     Must be called on the Godot main thread (via RunOnMainThreadAsync from PipeServer).
    /// </summary>
    /// <returns>Response object indicating success or failure.</returns>
    public static async Task<object> ExecuteConfirmAsync()
    {
        Logger.Info("Requested to confirm hand selection");

        try
        {
            // Guard: Must be in hand card selection mode
            var hand = NPlayerHand.Instance;
            if (hand is not { IsInCardSelection: true })
                return new
                {
                    ok = false,
                    error = "NOT_IN_HAND_SELECT",
                    message = "Not in hand card selection mode."
                };

            // Find the confirm button
            var confirmButton = hand.GetNodeOrNull<NConfirmButton>("%SelectModeConfirmButton");
            if (confirmButton == null)
                return new
                {
                    ok = false,
                    error = "UI_NOT_FOUND",
                    message = "Confirm button not found in hand selection."
                };

            // Check if confirm is enabled (enough cards selected)
            // NClickableControl exposes a public IsEnabled property (backed by protected _isEnabled field)
            if (!confirmButton.IsEnabled)
            {
                var prefs = HandSelectStateBuilder.GetPrefs(hand);
                var selectedCards = HandSelectStateBuilder.GetSelectedCards(hand);
                var count = selectedCards?.Count ?? 0;
                return new
                {
                    ok = false,
                    error = "CANNOT_CONFIRM",
                    message =
                        $"Cannot confirm: {count} card(s) selected, but {prefs?.MinSelect ?? 0}-{prefs?.MaxSelect ?? 0} required."
                };
            }

            // Click the confirm button — this calls OnSelectModeConfirmButtonPressed
            // which sets the result on _selectionCompletionSource
            Logger.Info("Clicking confirm button to finalize hand selection");
            confirmButton.ForceClick();

            // Wait for selection mode to end
            var completed = await ActionUtils.PollUntilAsync(
                () => !hand.IsInCardSelection,
                ActionUtils.UiTimeoutMs);

            if (!completed)
                Logger.Warning("Hand selection confirmed but mode did not exit in time");

            Logger.Info("Hand selection confirmed successfully");

            return new
            {
                ok = true,
                data = new
                {
                    confirmed = true,
                    message = "Hand selection confirmed."
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to confirm hand selection: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Internal method to perform the actual card selection from hand.
    /// </summary>
    private static async Task<object> ExecuteSelectAsync(string[] cardIds, int[]? nthValues = null)
    {
        try
        {
            // Guard: Must be in hand card selection mode
            var hand = NPlayerHand.Instance;
            if (hand is not { IsInCardSelection: true })
                return new
                {
                    ok = false,
                    error = "NOT_IN_HAND_SELECT",
                    message = "Not in hand card selection mode. Use 'sts2 state' to check current screen."
                };

            // Get prefs for validation
            var prefs = HandSelectStateBuilder.GetPrefs(hand);
            if (prefs == null)
                return new
                {
                    ok = false,
                    error = "INTERNAL_ERROR",
                    message = "Failed to read selection preferences from hand."
                };

            // Get current selected count to validate total won't exceed max
            var currentSelected = HandSelectStateBuilder.GetSelectedCards(hand);
            var currentCount = currentSelected?.Count ?? 0;

            if (currentCount + cardIds.Length > prefs.Value.MaxSelect)
                return new
                {
                    ok = false,
                    error = "INVALID_SELECTION_COUNT",
                    message =
                        $"Selecting {cardIds.Length} card(s) would exceed the maximum of {prefs.Value.MaxSelect}. " +
                        $"Currently {currentCount} card(s) already selected."
                };

            // Select each card by emitting Pressed signal on the holder
            var selectedCardIds = new List<string>();

            for (var i = 0; i < cardIds.Length; i++)
            {
                var cardId = cardIds[i];
                var nth = nthValues != null && i < nthValues.Length ? nthValues[i] : 0;

                // Re-read active holders each iteration since selection changes the visible set
                var activeHolders = hand.ActiveHolders;
                var holder = FindHolderByCardId(activeHolders, cardId, nth);

                if (holder == null)
                    return new
                    {
                        ok = false,
                        error = "CARD_NOT_FOUND",
                        message = $"Card '{cardId}' (nth={nth}) not found in selectable hand cards.",
                        available_cards = GetAvailableCardIds(hand.ActiveHolders)
                    };

                // Emit Pressed signal on the holder — triggers OnHolderPressed → SelectCardInSimpleMode/UpgradeMode
                Logger.Info($"Selecting card: {cardId} (nth={nth})");
                holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
                selectedCardIds.Add(cardId);

                // Small delay between clicks for multi-select
                if (i < cardIds.Length - 1)
                    await Task.Delay(ActionUtils.ClickDelayMs);
            }

            // After selection, check if selection auto-completed
            // (happens when _selectedCards.Count >= MaxSelect, see CheckIfSelectionComplete)
            await Task.Delay(ActionUtils.PostClickDelayMs);

            // Check if hand is still in selection mode
            var stillSelecting = hand.IsInCardSelection;

            Logger.Info($"Successfully selected {selectedCardIds.Count} card(s), still_selecting={stillSelecting}");

            return new
            {
                ok = true,
                data = new
                {
                    selected_count = selectedCardIds.Count,
                    selected_cards = selectedCardIds,
                    still_selecting = stillSelecting,
                    message = stillSelecting
                        ? $"Selected {selectedCardIds.Count} card(s). Selection still active — " +
                          "use 'hand_confirm_selection' to confirm or select more cards."
                        : $"Selected {selectedCardIds.Count} card(s). Selection auto-completed."
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to select hand card: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Finds a <see cref="NHandCardHolder" /> by card ID with nth disambiguation
    ///     among the active (visible) holders.
    /// </summary>
    private static NHandCardHolder? FindHolderByCardId(
        IReadOnlyList<NHandCardHolder> holders, string cardId, int nth)
    {
        var matchCount = 0;
        foreach (var holder in holders)
        {
            var model = holder.CardNode?.Model;
            if (model == null) continue;

            if (model.Id.Entry.Equals(cardId, StringComparison.OrdinalIgnoreCase))
            {
                if (matchCount == nth)
                    return holder;
                matchCount++;
            }
        }

        return null;
    }

    /// <summary>
    ///     Gets available card IDs from the active holders for error messages.
    /// </summary>
    private static List<string> GetAvailableCardIds(IReadOnlyList<NHandCardHolder> holders)
    {
        var ids = new List<string>();
        foreach (var holder in holders)
        {
            var model = holder.CardNode?.Model;
            if (model != null)
                ids.Add(model.Id.Entry);
        }

        return ids;
    }
}