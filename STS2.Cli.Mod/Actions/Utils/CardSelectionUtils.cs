using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2.Cli.Mod.Models.Actions;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions.Utils;

/// <summary>
///     Utility methods for <see cref="NChooseACardSelectionScreen" /> operations.
///     Provides screen lookup, card extraction, and skip-button helpers
///     used by tri-select, potion, and play-card handlers.
/// </summary>
public static class CardSelectionUtils
{
    /// <summary>
    ///     Finds the currently open <see cref="NChooseACardSelectionScreen" /> in the overlay stack.
    ///     Used by potion selection, deck selection (SMITH), and state detection.
    /// </summary>
    /// <returns>The card selection screen if found, or <c>null</c>.</returns>
    public static NChooseACardSelectionScreen? FindCardSelectionScreen()
    {
        return CommonUiUtils.FindScreenInOverlay<NChooseACardSelectionScreen>();
    }

    /// <summary>
    ///     Extracts all selectable cards from a <see cref="NChooseACardSelectionScreen" />
    ///     as a list of <see cref="SelectableCardDto" />.
    /// </summary>
    /// <param name="screen">The card selection screen to extract from.</param>
    /// <returns>A list of selectable card DTOs; empty if no cards found.</returns>
    public static List<SelectableCardDto> ExtractSelectableCards(NChooseACardSelectionScreen screen)
    {
        var cards = new List<SelectableCardDto>();
        var cardHolders = CommonUiUtils.FindAll<NCardHolder>(screen);

        for (var i = 0; i < cardHolders.Count; i++)
        {
            var holder = cardHolders[i];
            var card = holder.CardModel;
            if (card == null) continue;

            cards.Add(new SelectableCardDto
            {
                Index = i,
                CardId = card.Id.Entry,
                CardName = TextUtils.StripGameTags(card.Title),
                CardType = card.Type.ToString(),
                Cost = card.EnergyCost.Canonical,
                Description = TextUtils.StripGameTags(card.Description.GetFormattedText())
            });
        }

        return cards;
    }

    /// <summary>
    ///     Finds a <see cref="NCardHolder" /> in a card selection screen by card ID and nth occurrence.
    /// </summary>
    /// <param name="screen">The card selection screen to search.</param>
    /// <param name="cardId">Card ID to find (case-insensitive).</param>
    /// <param name="nth">Zero-based occurrence index when multiple copies exist.</param>
    /// <returns>The matching card holder, or <c>null</c> if not found or nth is out of range.</returns>
    public static NCardHolder? FindCardHolderById(NChooseACardSelectionScreen screen, string cardId, int nth)
    {
        var cardHolders = CommonUiUtils.FindAll<NCardHolder>(screen);
        var matchingHolders = new List<NCardHolder>();

        foreach (var holder in cardHolders)
        {
            if (holder.CardModel?.Id.Entry.Equals(cardId, StringComparison.OrdinalIgnoreCase) == true)
                matchingHolders.Add(holder);
        }

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
    public static NButton? FindSkipButton(NChooseACardSelectionScreen screen)
    {
        // Try to find by unique node name
        var skipButton = screen.GetNodeOrNull<NButton>("%SkipButton");
        if (skipButton != null) return skipButton;

        // Fallback: search for any button with "skip" in its name
        foreach (var child in screen.GetChildren())
        {
            if (child is NButton button &&
                button.Name.ToString().Contains("Skip", StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }

    /// <summary>
    ///     Reads the private <c>_canSkip</c> field from an <see cref="NChooseACardSelectionScreen" />
    ///     via reflection to determine if the selection can be skipped.
    /// </summary>
    /// <param name="screen">The card selection screen to check.</param>
    /// <returns><c>true</c> if the selection can be skipped; <c>false</c> otherwise.</returns>
    public static bool ReadCanSkip(NChooseACardSelectionScreen screen)
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
