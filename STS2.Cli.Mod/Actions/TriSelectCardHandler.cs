using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2.Cli.Mod.Models.Actions;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles card selection from the "choose a card" screen (<see cref="NChooseACardSelectionScreen" />).
///     This screen displays up to 3 generated cards for the player to pick one (or skip).
///     Triggered by potions (AttackPotion, SkillPotion, etc.), cards (Discovery, Quasar, Splash),
///     relics (Toolbox, MassiveScroll, etc.), and monsters (KnowledgeDemon).
/// </summary>
public static class TriSelectCardHandler
{
    private static readonly ModLogger Logger = new("TriSelectCardHandler");

    /// <summary>
    ///     Handles the tri_select_card request.
    ///     Validates parameters and delegates to Execute.
    /// </summary>
    public static object HandleRequest(Request request)
    {
        if (request.CardIds == null || request.CardIds.Length == 0)
        {
            Logger.Warning("tri_select_card requested with no card IDs");
            return new { ok = false, error = "MISSING_ARGUMENT", message = "At least one card ID is required" };
        }

        Logger.Info($"Requested to select {request.CardIds.Length} card(s) from tri-select screen");
        return Execute(request.CardIds, request.NthValues);
    }

    /// <summary>
    ///     Handles the tri_select_skip request.
    /// </summary>
    public static object HandleSkipRequest(Request request)
    {
        Logger.Info("Requested to skip tri-select card selection");
        return ExecuteSkip();
    }

    /// <summary>
    ///     Selects a card from the "choose a card" selection screen by card ID.
    ///     The screen always selects exactly 1 card.
    /// </summary>
    /// <param name="cardIds">Array of card IDs to select (only 1 supported).</param>
    /// <param name="nthValues">Optional nth values for each card ID.</param>
    /// <returns>Response object indicating success or failure.</returns>
    /// <remarks>
    ///     Must be called on the Godot main thread (PipeServer handles dispatching).
    /// </remarks>
    private static object Execute(string[] cardIds, int[]? nthValues = null)
    {
        // Guard: Must be in the TRI_SELECT screen
        var selectionScreen = UiUtils.FindCardSelectionScreen();
        if (selectionScreen == null)
            return new
            {
                ok = false,
                error = "NOT_IN_TRI_SELECT",
                message = "Not in tri-select card selection screen. Use 'sts2 state' to check current screen."
            };

        // Get selection constraints from the screen's _canSkip field
        var constraints = GetScreenConstraints(selectionScreen);

        // Validate selection count
        if (cardIds.Length < constraints.MinSelect || cardIds.Length > constraints.MaxSelect)
            return new
            {
                ok = false,
                error = "INVALID_SELECTION_COUNT",
                message =
                    $"This screen requires selecting {constraints.MinSelect}-{constraints.MaxSelect} card(s), but {cardIds.Length} was provided."
            };

        // Validate no duplicates
        var uniqueIds = new HashSet<string>();
        for (var i = 0; i < cardIds.Length; i++)
        {
            var nthVal = nthValues != null && i < nthValues.Length ? nthValues[i] : 0;
            var key = $"{cardIds[i]}_{nthVal}";
            if (!uniqueIds.Add(key))
                return new
                {
                    ok = false,
                    error = "DUPLICATE_SELECTION",
                    message = $"Card '{cardIds[i]}' (nth={nthVal}) was selected multiple times."
                };
        }

        // Find and select each card by ID
        var selectedCards = new List<SelectedCardDto>();

        for (var i = 0; i < cardIds.Length; i++)
        {
            var cardId = cardIds[i];
            var nth = nthValues != null && i < nthValues.Length ? nthValues[i] : 0;

            var holder = UiUtils.FindCardHolderById(selectionScreen, cardId, nth);
            if (holder == null)
                return new
                {
                    ok = false,
                    error = "CARD_NOT_FOUND",
                    message = $"Card '{cardId}' (nth={nth}) not found in selection screen."
                };

            // Emit click signal
            Logger.Info($"Selecting card: {cardId} (nth={nth})");
            holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);

            selectedCards.Add(new SelectedCardDto
            {
                Index = i,
                CardId = cardId
            });

            // Small delay between clicks for multi-select
            if (i < cardIds.Length - 1) OS.DelayMsec(ActionUtils.ClickDelayMs);
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
    ///     Skips the current card selection if allowed.
    /// </summary>
    /// <returns>Response object indicating success or failure.</returns>
    /// <remarks>
    ///     Must be called on the Godot main thread (PipeServer handles dispatching).
    /// </remarks>
    private static object ExecuteSkip()
    {
        // Guard: Must be in the TRI_SELECT screen
        var selectionScreen = UiUtils.FindCardSelectionScreen();
        if (selectionScreen == null)
            return new
            {
                ok = false,
                error = "NOT_IN_TRI_SELECT",
                message = "Not in tri-select card selection screen."
            };

        // Infer constraints to check if skip is allowed
        var constraints = GetScreenConstraints(selectionScreen);

        if (!constraints.CanSkip)
            return new
            {
                ok = false,
                error = "CANNOT_SKIP",
                message = "This selection cannot be skipped. You must select a card."
            };

        // Find and click the skip button
        var skipButton = UiUtils.FindSkipButton(selectionScreen);
        if (skipButton == null)
            return new
            {
                ok = false,
                error = "SKIP_BUTTON_NOT_FOUND",
                message = "Skip button not found in selection screen."
            };

        Logger.Info("Skipping tri-select card selection");
        skipButton.ForceClick();

        return new
        {
            ok = true,
            data = new { skipped = true }
        };
    }

    /// <summary>
    ///     Reads selection constraints directly from the <see cref="NChooseACardSelectionScreen" />.
    ///     The screen's <c>_canSkip</c> field determines whether selection can be skipped.
    ///     The screen always selects exactly 1 card (MinSelect=0 or 1, MaxSelect=1).
    /// </summary>
    private static SelectionConstraintsDto GetScreenConstraints(NChooseACardSelectionScreen screen)
    {
        try
        {
            var field = typeof(NChooseACardSelectionScreen).GetField("_canSkip",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var canSkip = field?.GetValue(screen) as bool? ?? false;
            return new SelectionConstraintsDto { MinSelect = canSkip ? 0 : 1, MaxSelect = 1, CanSkip = canSkip };
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read _canSkip from screen: {ex.Message}");
            // Fallback: single select, cannot skip
            return new SelectionConstraintsDto { MinSelect = 1, MaxSelect = 1, CanSkip = false };
        }
    }
}
