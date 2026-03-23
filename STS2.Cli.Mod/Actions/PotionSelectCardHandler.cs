using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2.Cli.Mod.Models.Message;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles card selection from potion-opened card selection screens.
/// </summary>
public static class PotionSelectCardHandler
{
    private static readonly ModLogger Logger = new("PotionSelectCardHandler");

    /// <summary>
    ///     Handles the potion_select_card request.
    ///     Validates parameters and delegates to Execute.
    /// </summary>
    public static object HandleRequest(Request request)
    {
        if (request.CardIds == null || request.CardIds.Length == 0)
        {
            Logger.Warning("potion_select_card requested with no card IDs");
            return new { ok = false, error = "MISSING_ARGUMENT", message = "At least one card ID is required" };
        }

        Logger.Info($"Requested to select {request.CardIds.Length} card(s) from potion selection screen");
        return Execute(request.CardIds, request.NthValues);
    }

    /// <summary>
    ///     Handles the potion_select_skip request.
    /// </summary>
    public static object HandleSkipRequest(Request request)
    {
        Logger.Info("Requested to skip potion card selection");
        return ExecuteSkip();
    }

    /// <summary>
    ///     Selects cards from a potion-opened selection screen by card ID.
    ///     Supports multi-selection for potions like Gambler's Brew.
    /// </summary>
    /// <param name="cardIds">Array of card IDs to select.</param>
    /// <param name="nthValues">Optional nth values for each card ID.</param>
    /// <returns>Response object indicating success or failure.</returns>
    /// <remarks>
    ///     Must be called on the Godot main thread (PipeServer handles dispatching).
    /// </remarks>
    public static object Execute(string[] cardIds, int[]? nthValues = null)
    {
        // Guard: Must be in POTION_SELECTION screen
        var selectionScreen = PotionUtils.FindSelectionScreen();
        if (selectionScreen == null)
        {
            return new
            {
                ok = false,
                error = "NOT_IN_POTION_SELECTION",
                message = "Not in potion card selection screen. Use 'sts2 state' to check current screen."
            };
        }

        // Get selection constraints from screen (detect from available cards or use defaults)
        var cardHolders = UiHelper.FindAll<NCardHolder>(selectionScreen);
        var constraints = InferSelectionConstraints(cardHolders);

        // Validate selection count
        if (cardIds.Length < constraints.MinSelect || cardIds.Length > constraints.MaxSelect)
        {
            return new
            {
                ok = false,
                error = "INVALID_SELECTION_COUNT",
                message = $"This potion requires selecting {constraints.MinSelect}-{constraints.MaxSelect} card(s), but {cardIds.Length} was provided."
            };
        }

        // Validate no duplicates
        var uniqueIds = new HashSet<string>();
        for (int i = 0; i < cardIds.Length; i++)
        {
            var key = $"{cardIds[i]}_{nthValues?[i] ?? 0}";
            if (!uniqueIds.Add(key))
            {
                return new
                {
                    ok = false,
                    error = "DUPLICATE_SELECTION",
                    message = $"Card '{cardIds[i]}' (nth={nthValues?[i] ?? 0}) was selected multiple times."
                };
            }
        }

        // Find and select each card by ID
        var selectedCards = new List<SelectedCardInfo>();
        
        for (int i = 0; i < cardIds.Length; i++)
        {
            var cardId = cardIds[i];
            var nth = nthValues?[i] ?? 0;

            var holder = PotionUtils.FindCardHolderById(selectionScreen, cardId, nth);
            if (holder == null)
            {
                return new
                {
                    ok = false,
                    error = "CARD_NOT_FOUND",
                    message = $"Card '{cardId}' (nth={nth}) not found in selection screen."
                };
            }

            // Emit click signal
            Logger.Info($"Selecting card: {cardId} (nth={nth})");
            holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
            
            selectedCards.Add(new SelectedCardInfo
            {
                Index = i,
                CardId = cardId
            });

            // Small delay between clicks for multi-select
            if (i < cardIds.Length - 1)
            {
                OS.DelayMsec(100);
            }
        }

        Logger.Info($"Successfully selected {selectedCards.Count} card(s)");

        return new
        {
            ok = true,
            data = new
            {
                selected_count = selectedCards.Count,
                selected_cards = selectedCards.Select(s => s.CardId).ToList(),
                message = $"Successfully selected {selectedCards.Count} card(s)"
            }
        };
    }

    /// <summary>
    ///     Skips the current potion card selection if allowed.
    /// </summary>
    /// <returns>Response object indicating success or failure.</returns>
    /// <remarks>
    ///     Must be called on the Godot main thread (PipeServer handles dispatching).
    /// </remarks>
    public static object ExecuteSkip()
    {
        // Guard: Must be in POTION_SELECTION screen
        var selectionScreen = PotionUtils.FindSelectionScreen();
        if (selectionScreen == null)
        {
            return new
            {
                ok = false,
                error = "NOT_IN_POTION_SELECTION",
                message = "Not in potion card selection screen."
            };
        }

        // Infer constraints to check if skip is allowed
        var cardHolders = UiHelper.FindAll<NCardHolder>(selectionScreen);
        var constraints = InferSelectionConstraints(cardHolders);

        if (!constraints.CanSkip)
        {
            return new
            {
                ok = false,
                error = "CANNOT_SKIP",
                message = "This potion selection cannot be skipped. You must select at least one card."
            };
        }

        // Find and click skip button
        var skipButton = PotionUtils.FindSkipButton(selectionScreen);
        if (skipButton == null)
        {
            return new
            {
                ok = false,
                error = "SKIP_BUTTON_NOT_FOUND",
                message = "Skip button not found in selection screen."
            };
        }

        Logger.Info("Skipping potion card selection");
        skipButton.ForceClick();

        return new
        {
            ok = true,
            data = new { skipped = true }
        };
    }

    /// <summary>
    ///     Infers selection constraints from the current selection screen.
    ///     This is a heuristic based on the number of cards and typical potion patterns.
    /// </summary>
    private static SelectionConstraints InferSelectionConstraints(List<NCardHolder> cardHolders)
    {
        var cardCount = cardHolders.Count;

        // If we have 3 cards, it's likely a "choose 1 of 3" potion (Attack/Skill/Power/Colorless)
        if (cardCount == 3)
        {
            return new SelectionConstraints(0, 1, true); // Can skip (e.g., Colorless Potion)
        }

        // If we have many cards, it's likely a hand-based selection (Gambler's Brew, Ashwater, etc.)
        if (cardCount > 3)
        {
            return new SelectionConstraints(0, cardCount, true); // Multi-select with skip option
        }

        // Default: single select, cannot skip
        return new SelectionConstraints(1, 1, false);
    }

    /// <summary>
    ///     Helper class to track selected card info.
    /// </summary>
    private class SelectedCardInfo
    {
        public int Index { get; set; }
        public required string CardId { get; set; }
    }
}
