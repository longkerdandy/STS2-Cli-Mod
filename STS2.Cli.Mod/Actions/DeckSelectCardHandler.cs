using System.Reflection;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using Godot;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles card selection from grid-based card selection screens
///     (<see cref="NDeckCardSelectScreen" />, <see cref="NDeckUpgradeSelectScreen" />, etc.).
///     Supports single and multi-select, with preview + confirm flow.
/// </summary>
public static class DeckSelectCardHandler
{
    private static readonly ModLogger Logger = new("DeckSelectCardHandler");

    /// <summary>
    ///     Delay between card selection clicks (for multi-select).
    /// </summary>
    private const int ClickDelayMs = 100;

    /// <summary>
    ///     Delay after all selections to allow preview to appear.
    /// </summary>
    private const int PreviewAppearDelayMs = 300;

    /// <summary>
    ///     Maximum time to wait for the screen to be removed after confirm.
    /// </summary>
    private const int CompletionTimeoutMs = 5000;

    /// <summary>
    ///     Polling interval when waiting for UI transitions.
    /// </summary>
    private const int PollIntervalMs = 100;

    /// <summary>
    ///     Selects cards from a grid-based card selection screen by card ID.
    ///     Must be called on the Godot main thread (via RunOnMainThreadAsync from PipeServer).
    /// </summary>
    /// <param name="cardIds">Array of card IDs to select.</param>
    /// <param name="nthValues">Optional nth values for each card ID (0-based).</param>
    /// <returns>Response object indicating success or failure.</returns>
    public static async Task<object> ExecuteAsync(string[] cardIds, int[]? nthValues = null)
    {
        try
        {
            // Guard: Must be on a grid card selection screen
            var screen = FindGridSelectionScreen();
            if (screen == null)
            {
                return new
                {
                    ok = false,
                    error = "NOT_IN_DECK_CARD_SELECT",
                    message = "Not on a deck card selection screen. Use 'sts2 state' to check current screen."
                };
            }

            // Get prefs for validation
            var prefs = GetPrefs(screen);
            if (prefs == null)
            {
                return new
                {
                    ok = false,
                    error = "INTERNAL_ERROR",
                    message = "Failed to read selection preferences from screen."
                };
            }

            // Validate selection count
            if (cardIds.Length < 1 || cardIds.Length > prefs.Value.MaxSelect)
            {
                return new
                {
                    ok = false,
                    error = "INVALID_SELECTION_COUNT",
                    message = $"This selection requires 1-{prefs.Value.MaxSelect} card(s), but {cardIds.Length} was provided."
                };
            }

            // Get the card list and grid
            var cards = GetCards(screen);
            var grid = GetGrid(screen);
            if (cards == null || grid == null)
            {
                return new
                {
                    ok = false,
                    error = "INTERNAL_ERROR",
                    message = "Failed to access card grid or card list."
                };
            }

            // Find and select each card
            var selectedCardIds = new List<string>();

            for (int i = 0; i < cardIds.Length; i++)
            {
                var cardId = cardIds[i];
                var nth = nthValues != null && i < nthValues.Length ? nthValues[i] : 0;

                // Find the CardModel in the _cards list by ID
                var cardModel = FindCardModelById(cards, cardId, nth);
                if (cardModel == null)
                {
                    return new
                    {
                        ok = false,
                        error = "CARD_NOT_FOUND",
                        message = $"Card '{cardId}' (nth={nth}) not found in selection screen.",
                        available_cards = GetAvailableCardIds(cards)
                    };
                }

                // Find the holder in the grid using the CardModel reference
                var holder = grid.GetCardHolder(cardModel);
                if (holder == null)
                {
                    return new
                    {
                        ok = false,
                        error = "CARD_NOT_FOUND",
                        message = $"Card holder for '{cardId}' (nth={nth}) not found in grid."
                    };
                }

                // Emit Pressed signal on the holder — triggers NCardGrid.OnHolderPressed → HolderPressed signal → OnCardClicked
                Logger.Info($"Selecting card: {cardId} (nth={nth})");
                holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
                selectedCardIds.Add(cardId);

                // Small delay between clicks for multi-select
                if (i < cardIds.Length - 1)
                {
                    await Task.Delay(ClickDelayMs);
                }
            }

            // After selecting all cards:
            // If we selected exactly MaxSelect cards, the preview should auto-appear.
            // If we selected fewer than MaxSelect but >= MinSelect, we need to click the confirm button
            // to trigger PreviewSelection.
            if (cardIds.Length < prefs.Value.MaxSelect && cardIds.Length >= prefs.Value.MinSelect
                && prefs.Value.MinSelect != prefs.Value.MaxSelect)
            {
                // Need to click the manual confirm button to trigger preview
                await Task.Delay(PreviewAppearDelayMs);
                var confirmButton = GetConfirmButton(screen);
                if (confirmButton != null)
                {
                    Logger.Info("Clicking confirm button to trigger preview (min != max selection)");
                    confirmButton.ForceClick();
                }
            }

            // Wait for preview to appear, then confirm
            await Task.Delay(PreviewAppearDelayMs);

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

            // Wait for the screen to be removed from overlay stack
            var completed = await WaitForScreenRemoval(screen);
            if (!completed)
                Logger.Warning("Deck card selection completed but screen was not removed in time");

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
            Logger.Error($"Failed to select deck card: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Skips/cancels the current deck card selection if allowed (cancelable).
    ///     Must be called on the Godot main thread (via RunOnMainThreadAsync from PipeServer).
    /// </summary>
    /// <returns>Response object indicating success or failure.</returns>
    public static async Task<object> ExecuteSkipAsync()
    {
        try
        {
            // Guard: Must be on a grid card selection screen
            var screen = FindGridSelectionScreen();
            if (screen == null)
            {
                return new
                {
                    ok = false,
                    error = "NOT_IN_DECK_CARD_SELECT",
                    message = "Not on a deck card selection screen."
                };
            }

            // Check if cancelable
            var prefs = GetPrefs(screen);
            if (prefs is not { Cancelable: true })
            {
                return new
                {
                    ok = false,
                    error = "CANNOT_SKIP",
                    message = "This deck card selection cannot be cancelled/skipped."
                };
            }

            // Find and click the close button
            var closeButton = GetCloseButton(screen);
            if (closeButton == null)
            {
                return new
                {
                    ok = false,
                    error = "UI_NOT_FOUND",
                    message = "Close button not found on deck card selection screen."
                };
            }

            Logger.Info("Skipping deck card selection via close button");
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
            Logger.Error($"Failed to skip deck card selection: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Finds a <see cref="NCardGridSelectionScreen" /> in the overlay stack.
    /// </summary>
    private static NCardGridSelectionScreen? FindGridSelectionScreen()
    {
        var overlayStack = NOverlayStack.Instance;
        if (overlayStack == null) return null;

        var overlay = overlayStack.Peek();
        if (overlay is NCardGridSelectionScreen gridScreen)
            return gridScreen;

        foreach (var child in overlayStack.GetChildren())
        {
            if (child is NCardGridSelectionScreen childScreen)
                return childScreen;
        }

        return null;
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
    ///     Gets the preview confirm button from the screen via node path.
    ///     NDeckCardSelectScreen uses "%PreviewConfirm" inside "%PreviewContainer".
    ///     NDeckUpgradeSelectScreen uses different node names for single/multi preview.
    ///     Falls back to searching by type.
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
                // Try single preview confirm first
                var singleField = typeof(NDeckUpgradeSelectScreen).GetField("_singlePreviewConfirmButton",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var singleBtn = singleField?.GetValue(screen) as NConfirmButton;
                if (singleBtn != null && GodotObject.IsInstanceValid(singleBtn))
                    return singleBtn;

                // Fall back to multi preview confirm
                var multiField = typeof(NDeckUpgradeSelectScreen).GetField("_multiPreviewConfirmButton",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                return multiField?.GetValue(screen) as NConfirmButton;
            }

            // NSimpleCardSelectScreen: no preview, has _confirmButton directly
            if (screen is NSimpleCardSelectScreen)
            {
                var field = typeof(NSimpleCardSelectScreen).GetField("_confirmButton",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                return field?.GetValue(screen) as NConfirmButton;
            }

            // Generic fallback: search for confirm buttons in the tree
            return UiHelper.FindFirst<NConfirmButton>(screen);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get preview confirm button: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the manual confirm button (for when MinSelect != MaxSelect).
    ///     NDeckCardSelectScreen: _confirmButton (%Confirm).
    ///     NSimpleCardSelectScreen: _confirmButton (%Confirm).
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
    ///     NDeckCardSelectScreen: _closeButton (%Close).
    ///     NDeckUpgradeSelectScreen: _closeButton (%Close).
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
        int matchCount = 0;
        foreach (var card in cards)
        {
            if (card.Id.Entry.Equals(cardId, StringComparison.OrdinalIgnoreCase))
            {
                if (matchCount == nth)
                    return card;
                matchCount++;
            }
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
        var elapsed = 0;
        while (elapsed < CompletionTimeoutMs)
        {
            await Task.Delay(PollIntervalMs);
            elapsed += PollIntervalMs;

            if (!GodotObject.IsInstanceValid(screen) || !screen.IsInsideTree())
                return true;
        }

        return false;
    }
}
