using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles card selection from grid-based card selection screens
///     (<see cref="NSimpleCardSelectScreen" />, <see cref="NDeckCardSelectScreen" />,
///     <see cref="NDeckUpgradeSelectScreen" />, etc.).
///     Supports single and multi-select flows.
///     <see cref="NDeckCardSelectScreen" /> and <see cref="NDeckUpgradeSelectScreen" /> use a
///     preview + confirm flow (select → preview appears → click preview confirm).
///     <see cref="NSimpleCardSelectScreen" /> has no preview: when MinSelect == MaxSelect,
///     selection auto-completes; otherwise a confirm button enables after selecting enough cards.
/// </summary>
public static class GridSelectCardHandler
{
    private static readonly ModLogger Logger = new("GridSelectCardHandler");

    /// <summary>
    ///     Handles the grid_select_card request.
    ///     Validates parameters and delegates to ExecuteAsync.
    /// </summary>
    public static async Task<object> HandleRequestAsync(Request request)
    {
        if (request.CardIds == null || request.CardIds.Length == 0)
        {
            Logger.Warning("grid_select_card requested with no card IDs");
            return new { ok = false, error = "MISSING_ARGUMENT", message = "At least one card ID is required" };
        }

        Logger.Info($"Requested to select {request.CardIds.Length} card(s) from grid card selection screen");
        return await ExecuteAsync(request.CardIds, request.NthValues);
    }

    /// <summary>
    ///     Handles the grid_select_skip request.
    /// </summary>
    public static async Task<object> HandleSkipRequestAsync(Request request)
    {
        Logger.Info("Requested to skip grid card selection");
        return await ExecuteSkipAsync();
    }

    /// <summary>
    ///     Selects cards from a grid-based card selection screen by card ID.
    ///     Must be called on the Godot main thread (via RunOnMainThreadAsync from PipeServer).
    /// </summary>
    /// <param name="cardIds">Array of card IDs to select.</param>
    /// <param name="nthValues">Optional nth values for each card ID (0-based).</param>
    /// <returns>Response object indicating success or failure.</returns>
    private static async Task<object> ExecuteAsync(string[] cardIds, int[]? nthValues = null)
    {
        try
        {
            // Guard: Must be on a grid card selection screen
            var screen = FindGridSelectionScreen();
            if (screen == null)
                return new
                {
                    ok = false,
                    error = "NOT_IN_GRID_CARD_SELECT",
                    message = "Not on a grid card selection screen. Use 'sts2 state' to check current screen."
                };

            // Get prefs for validation
            var prefs = GetPrefs(screen);
            if (prefs == null)
                return new
                {
                    ok = false,
                    error = "INTERNAL_ERROR",
                    message = "Failed to read selection preferences from screen."
                };

            // Validate selection count
            if (cardIds.Length < 1 || cardIds.Length > prefs.Value.MaxSelect)
                return new
                {
                    ok = false,
                    error = "INVALID_SELECTION_COUNT",
                    message =
                        $"This selection requires 1-{prefs.Value.MaxSelect} card(s), but {cardIds.Length} was provided."
                };

            // Get the card list and grid
            var cards = GetCards(screen);
            var grid = GetGrid(screen);
            if (cards == null || grid == null)
                return new
                {
                    ok = false,
                    error = "INTERNAL_ERROR",
                    message = "Failed to access card grid or card list."
                };

            // Find and select each card
            var selectedCardIds = new List<string>();

            for (var i = 0; i < cardIds.Length; i++)
            {
                var cardId = cardIds[i];
                var nth = nthValues != null && i < nthValues.Length ? nthValues[i] : 0;

                // Find the CardModel in the _cards list by ID
                var cardModel = FindCardModelById(cards, cardId, nth);
                if (cardModel == null)
                    return new
                    {
                        ok = false,
                        error = "CARD_NOT_FOUND",
                        message = $"Card '{cardId}' (nth={nth}) not found in selection screen.",
                        available_cards = GetAvailableCardIds(cards)
                    };

                // Find the holder in the grid using the CardModel reference
                var holder = grid.GetCardHolder(cardModel);
                if (holder == null)
                    return new
                    {
                        ok = false,
                        error = "CARD_NOT_FOUND",
                        message = $"Card holder for '{cardId}' (nth={nth}) not found in grid."
                    };

                // Emit Pressed signal on the holder — triggers NCardGrid.OnHolderPressed → HolderPressed signal → OnCardClicked
                Logger.Info($"Selecting card: {cardId} (nth={nth})");
                holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
                selectedCardIds.Add(cardId);

                // Small delay between clicks for multi-select
                if (i < cardIds.Length - 1) await Task.Delay(ActionUtils.ClickDelayMs);
            }

            // After selecting all cards, the confirmation flow depends on screen type:
            //
            // NSimpleCardSelectScreen (combat grid overlays):
            //   - No preview flow. When MinSelect == MaxSelect, auto-completes via CompleteSelection()
            //     after selecting enough cards — screen is removed from overlay immediately.
            //   - When MinSelect != MaxSelect, _confirmButton enables after count >= MinSelect.
            //     Click it to complete.
            //
            // NDeckCardSelectScreen / NDeckUpgradeSelectScreen (deck operations):
            //   - Has preview flow. After selecting MaxSelect cards, preview auto-appears.
            //   - If fewer than MaxSelect but >= MinSelect, click confirm to trigger preview.
            //   - Then click preview confirm button to finalize.

            if (screen is NSimpleCardSelectScreen)
            {
                // NSimpleCardSelectScreen: no preview
                if (prefs.Value.MinSelect != prefs.Value.MaxSelect
                    && cardIds.Length >= prefs.Value.MinSelect
                    && cardIds.Length < prefs.Value.MaxSelect)
                {
                    // Manual confirm needed (e.g., Dredge with variable selection count)
                    await Task.Delay(ActionUtils.ClickDelayMs);
                    var confirmBtn = GetConfirmButton(screen);
                    if (confirmBtn != null)
                    {
                        Logger.Info("Clicking confirm button on NSimpleCardSelectScreen (min != max)");
                        confirmBtn.ForceClick();
                    }
                }
                // else: auto-completes when MinSelect == MaxSelect and enough cards selected
            }
            else
            {
                // NDeckCardSelectScreen / NDeckUpgradeSelectScreen: preview + confirm flow
                if (cardIds.Length < prefs.Value.MaxSelect && cardIds.Length >= prefs.Value.MinSelect
                                                           && prefs.Value.MinSelect != prefs.Value.MaxSelect)
                {
                    // Need to click the manual confirmation button to trigger preview
                    await Task.Delay(ActionUtils.PreviewAppearDelayMs);
                    var confirmButton = GetConfirmButton(screen);
                    if (confirmButton != null)
                    {
                        Logger.Info("Clicking confirm button to trigger preview (min != max selection)");
                        confirmButton.ForceClick();
                    }
                }

                // Wait for the preview to appear, then confirm
                await Task.Delay(ActionUtils.PreviewAppearDelayMs);

                // Click the preview confirm button to finalize
                var previewConfirm = GetPreviewConfirmButton(screen);
                if (previewConfirm != null)
                {
                    Logger.Info("Clicking preview confirm button to finalize selection");
                    previewConfirm.ForceClick();
                }
                else
                {
                    Logger.Warning("Preview confirm button not found — selection may complete via other path");
                }
            }

            // Wait for the screen to be removed from the overlay stack
            var completed = await WaitForScreenRemoval(screen);
            if (!completed)
                Logger.Warning("Grid card selection completed but screen was not removed in time");

            Logger.Info($"Successfully selected {selectedCardIds.Count} card(s)");

            return new
            {
                ok = true,
                data = new
                {
                    selected_count = selectedCardIds.Count,
                    selected_cards = selectedCardIds,
                    message = $"Successfully selected {selectedCardIds.Count} card(s)"
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to select grid card: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Skips/cancels the current grid card selection if allowed (cancelable).
    ///     Must be called on the Godot main thread (via RunOnMainThreadAsync from PipeServer).
    /// </summary>
    /// <returns>Response object indicating success or failure.</returns>
    private static async Task<object> ExecuteSkipAsync()
    {
        try
        {
            // Guard: Must be on a grid card selection screen
            var screen = FindGridSelectionScreen();
            if (screen == null)
                return new
                {
                    ok = false,
                    error = "NOT_IN_GRID_CARD_SELECT",
                    message = "Not on a grid card selection screen."
                };

            // Check if cancelable
            var prefs = GetPrefs(screen);
            if (prefs is not { Cancelable: true })
                return new
                {
                    ok = false,
                    error = "CANNOT_SKIP",
                    message = "This grid card selection cannot be cancelled/skipped."
                };

            // Find and click the close button
            var closeButton = GetCloseButton(screen);
            if (closeButton == null)
                return new
                {
                    ok = false,
                    error = "UI_NOT_FOUND",
                    message = "Close button not found on grid card selection screen."
                };

            Logger.Info("Skipping grid card selection via close button");
            closeButton.ForceClick();

            // Wait for the screen to be removed
            var completed = await WaitForScreenRemoval(screen);
            if (!completed)
                Logger.Warning("Skip completed but screen was not removed in time");

            return new
            {
                ok = true,
                data = new { skipped = true }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to skip grid card selection: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Finds a <see cref="NCardGridSelectionScreen" /> in the overlay stack.
    /// </summary>
    private static NCardGridSelectionScreen? FindGridSelectionScreen()
    {
        return UiUtils.FindScreenInOverlay<NCardGridSelectionScreen>();
    }

    /// <summary>
    ///     Gets the card list from the protected <c>_cards</c> field on the base class.
    /// </summary>
    private static IReadOnlyList<CardModel>? GetCards(NCardGridSelectionScreen screen)
    {
        try
        {
            var field = typeof(NCardGridSelectionScreen).GetField("_cards",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(screen) as IReadOnlyList<CardModel>;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get _cards: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the <see cref="NCardGrid" /> from the protected <c>_grid</c> field.
    /// </summary>
    private static NCardGrid? GetGrid(NCardGridSelectionScreen screen)
    {
        try
        {
            var field = typeof(NCardGridSelectionScreen).GetField("_grid",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(screen) as NCardGrid;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get _grid: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the <see cref="CardSelectorPrefs" /> from the private <c>_prefs</c> field.
    /// </summary>
    private static CardSelectorPrefs? GetPrefs(NCardGridSelectionScreen screen)
    {
        try
        {
            var field = screen.GetType().GetField("_prefs",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : (CardSelectorPrefs?)field.GetValue(screen);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get _prefs: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the preview confirm button from the screen.
    ///     Only applicable to <see cref="NDeckCardSelectScreen" /> and <see cref="NDeckUpgradeSelectScreen" />
    ///     which have a preview + confirm flow.
    ///     Not used for <see cref="NSimpleCardSelectScreen" /> (no preview).
    /// </summary>
    private static NConfirmButton? GetPreviewConfirmButton(NCardGridSelectionScreen screen)
    {
        try
        {
            // NDeckCardSelectScreen: _previewConfirmButton is inside %PreviewContainer
            if (screen is NDeckCardSelectScreen)
            {
                var field = typeof(NDeckCardSelectScreen).GetField("_previewConfirmButton",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                return field?.GetValue(screen) as NConfirmButton;
            }

            // NDeckUpgradeSelectScreen: has single and multi preview
            if (screen is NDeckUpgradeSelectScreen)
            {
                // Try a single preview confirm first
                var singleField = typeof(NDeckUpgradeSelectScreen).GetField("_singlePreviewConfirmButton",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (singleField?.GetValue(screen) is NConfirmButton singleBtn && GodotObject.IsInstanceValid(singleBtn))
                    return singleBtn;

                // Fall back to multi preview confirm
                var multiField = typeof(NDeckUpgradeSelectScreen).GetField("_multiPreviewConfirmButton",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                return multiField?.GetValue(screen) as NConfirmButton;
            }

            // Generic fallback: search for confirmation buttons in the tree
            return UiUtils.FindFirst<NConfirmButton>(screen);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get preview confirm button: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the manual confirmation button (for when MinSelect != MaxSelect).
    ///     <see cref="NDeckCardSelectScreen" />: _confirmButton (%Confirm) — triggers preview.
    ///     <see cref="NSimpleCardSelectScreen" />: _confirmButton (%Confirm) — completes selection directly.
    /// </summary>
    private static NConfirmButton? GetConfirmButton(NCardGridSelectionScreen screen)
    {
        try
        {
            if (screen is NDeckCardSelectScreen)
            {
                var field = typeof(NDeckCardSelectScreen).GetField("_confirmButton",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                return field?.GetValue(screen) as NConfirmButton;
            }

            if (screen is NSimpleCardSelectScreen)
            {
                var field = typeof(NSimpleCardSelectScreen).GetField("_confirmButton",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                return field?.GetValue(screen) as NConfirmButton;
            }

            // For other screen types, try generic search
            return screen.GetNodeOrNull<NConfirmButton>("%Confirm");
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get confirm button: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the close/cancel button for cancelable selections.
    ///     <see cref="NDeckCardSelectScreen" />: _closeButton (%Close).
    ///     <see cref="NDeckUpgradeSelectScreen" />: _closeButton (%Close).
    ///     <see cref="NSimpleCardSelectScreen" /> has no close button (not cancelable).
    /// </summary>
    private static NBackButton? GetCloseButton(NCardGridSelectionScreen screen)
    {
        try
        {
            // Both NDeckCardSelectScreen and NDeckUpgradeSelectScreen have _closeButton
            var field = screen.GetType().GetField("_closeButton",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                return field.GetValue(screen) as NBackButton;

            // Fallback: search by node path
            return screen.GetNodeOrNull<NBackButton>("%Close");
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get close button: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Finds a <see cref="CardModel" /> by card ID with nth disambiguation.
    /// </summary>
    private static CardModel? FindCardModelById(IReadOnlyList<CardModel> cards, string cardId, int nth)
    {
        var matchCount = 0;
        foreach (var card in cards)
            if (card.Id.Entry.Equals(cardId, StringComparison.OrdinalIgnoreCase))
            {
                if (matchCount == nth)
                    return card;
                matchCount++;
            }

        return null;
    }

    /// <summary>
    ///     Gets available card IDs for error messages.
    /// </summary>
    private static List<string> GetAvailableCardIds(IReadOnlyList<CardModel> cards)
    {
        return cards.Select(c => c.Id.Entry).ToList();
    }

    /// <summary>
    ///     Waits for a grid selection screen to be removed from the scene tree.
    /// </summary>
    private static async Task<bool> WaitForScreenRemoval(NCardGridSelectionScreen screen)
    {
        return await ActionUtils.PollUntilAsync(
            () => !GodotObject.IsInstanceValid(screen) || !screen.IsInsideTree(),
            ActionUtils.UiTimeoutMs);
    }
}
