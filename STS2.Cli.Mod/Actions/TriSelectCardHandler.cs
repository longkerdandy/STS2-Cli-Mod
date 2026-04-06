using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2.Cli.Mod.Actions.Utils;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>tri_select_card</c> and <c>tri_select_skip</c> CLI commands.
///     Selects or skips cards from the "choose a card" screen (<see cref="NChooseACardSelectionScreen" />).
///     This screen displays up to 3 generated cards for the player to pick one (or skip).
///     Triggered by potions (AttackPotion, SkillPotion, etc.), cards (Discovery, Quasar, Splash),
///     relics (Toolbox, MassiveScroll, etc.), and monsters (KnowledgeDemon).
/// </summary>
/// <remarks>
///     <para>
///         <b>CLI commands:</b>
///     </para>
///     <list type="bullet">
///         <item>
///             <c>sts2 tri_select_card &lt;card_id&gt; [&lt;card_id&gt;...] [--nth &lt;n&gt;...]</c>
///         </item>
///         <item>
///             <c>sts2 tri_select_skip</c>
///         </item>
///     </list>
///     <para><b>Scene:</b> Combat or event, when a potion/card/relic/monster triggers a "choose a card" selection.</para>
/// </remarks>
public static class TriSelectCardHandler
{
    private static readonly ModLogger Logger = new("TriSelectCardHandler");

    /// <summary>
    ///     Selects a card from the "choose a card" selection screen by card ID.
    ///     Validates parameters and current screen state.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    public static object Execute(Request request)
    {
        if (request.CardIds == null || request.CardIds.Length == 0)
        {
            Logger.Warning("tri_select_card requested with no card IDs");
            return new { ok = false, error = "MISSING_ARGUMENT", message = "At least one card ID is required" };
        }

        var cardIds = request.CardIds;
        var nthValues = request.NthValues;

        Logger.Info($"Requested to select {cardIds.Length} card(s) from tri-select screen");

        try
        {
            // Guard: Must be in the TRI_SELECT screen
            var selectionScreen = UiUtils.FindScreenInOverlay<NChooseACardSelectionScreen>();
            if (selectionScreen == null)
                return new
                {
                    ok = false,
                    error = "NOT_IN_TRI_SELECT",
                    message = "Not in tri-select card selection screen. Use 'sts2 state' to check current screen."
                };

            // Validate selection count (tri-select always picks exactly 1 card)
            var canSkip = ReadCanSkip(selectionScreen);
            if (cardIds.Length > 1 || (!canSkip && cardIds.Length < 1))
                return new
                {
                    ok = false,
                    error = "INVALID_SELECTION_COUNT",
                    message = canSkip
                        ? $"This screen allows selecting 0-1 card(s), but {cardIds.Length} were provided."
                        : $"This screen requires selecting exactly 1 card, but {cardIds.Length} were provided."
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
            var selectedCardIds = new List<string>();

            for (var i = 0; i < cardIds.Length; i++)
            {
                var cardId = cardIds[i];
                var nth = nthValues != null && i < nthValues.Length ? nthValues[i] : 0;

                var holder = FindCardHolderById(selectionScreen, cardId, nth);
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

                selectedCardIds.Add(cardId);

                // Small delay between clicks for multi-select
                if (i < cardIds.Length - 1) OS.DelayMsec(ActionUtils.ClickDelayMs);
            }

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
            Logger.Error($"Failed to select card from tri-select: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Skips the current card selection if allowed.
    ///     Validates current screen state.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    public static object ExecuteSkip(Request _)
    {
        Logger.Info("Requested to skip tri-select card selection");

        try
        {
            // Guard: Must be in the TRI_SELECT screen
            var selectionScreen = UiUtils.FindScreenInOverlay<NChooseACardSelectionScreen>();
            if (selectionScreen == null)
                return new
                {
                    ok = false,
                    error = "NOT_IN_TRI_SELECT",
                    message = "Not in tri-select card selection screen."
                };

            // Infer constraints to check if skip is allowed
            var canSkip = ReadCanSkip(selectionScreen);

            if (!canSkip)
                return new
                {
                    ok = false,
                    error = "CANNOT_SKIP",
                    message = "This selection cannot be skipped. You must select a card."
                };

            // Find and click the skip button
            var skipButton = FindSkipButton(selectionScreen);
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
        catch (Exception ex)
        {
            Logger.Error($"Failed to skip tri-select: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Finds a <see cref="NCardHolder" /> in a card selection screen by card ID and nth occurrence.
    /// </summary>
    /// <param name="screen">The card selection screen to search.</param>
    /// <param name="cardId">Card ID to find (case-insensitive).</param>
    /// <param name="nth">Zero-based occurrence index when multiple copies exist.</param>
    /// <returns>The matching card holder, or <c>null</c> if not found or nth is out of range.</returns>
    private static NCardHolder? FindCardHolderById(NChooseACardSelectionScreen screen, string cardId, int nth)
    {
        var cardHolders = UiUtils.FindAll<NCardHolder>(screen);
        var matchingHolders = new List<NCardHolder>();

        foreach (var holder in cardHolders)
            if (holder.CardModel?.Id.Entry.Equals(cardId, StringComparison.OrdinalIgnoreCase) == true)
                matchingHolders.Add(holder);

        if (nth < 0 || nth >= matchingHolders.Count)
            return null;

        return matchingHolders[nth];
    }

    /// <summary>
    ///     Finds the skip button on a <see cref="NChooseACardSelectionScreen" />.
    ///     Looks for a unique-name node <c>%SkipButton</c> first, then falls back to
    ///     a child whose name contains "Skip".
    /// </summary>
    /// <param name="screen">The card selection screen to search.</param>
    /// <returns>The skip button if found, or <c>null</c>.</returns>
    private static NButton? FindSkipButton(NChooseACardSelectionScreen screen)
    {
        // Try to find by unique node name
        var skipButton = screen.GetNodeOrNull<NButton>("%SkipButton");
        if (skipButton != null) return skipButton;

        // Fallback: search for any button with "skip" in its name
        foreach (var child in screen.GetChildren())
            if (child is NButton button &&
                button.Name.ToString().Contains("Skip", StringComparison.OrdinalIgnoreCase))
                return button;

        return null;
    }

    /// <summary>
    ///     Reads the private <c>_canSkip</c> field from an <see cref="NChooseACardSelectionScreen" />
    ///     via reflection to determine if the selection can be skipped.
    /// </summary>
    /// <param name="screen">The card selection screen to check.</param>
    /// <returns><c>true</c> if the selection can be skipped; <c>false</c> otherwise.</returns>
    private static bool ReadCanSkip(NChooseACardSelectionScreen screen)
    {
        try
        {
            var field = typeof(NChooseACardSelectionScreen).GetField("_canSkip",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(screen) as bool? ?? false;
        }
        catch
        {
            return false;
        }
    }
}