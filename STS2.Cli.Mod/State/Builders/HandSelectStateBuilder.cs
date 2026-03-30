using System.Reflection;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds the hand selection state DTO from <see cref="NPlayerHand" />.
///     Extracts selectable cards, selected cards, and selection constraints
///     using a combination of public APIs and reflection for private fields.
/// </summary>
public static class HandSelectStateBuilder
{
    private static readonly ModLogger Logger = new("HandSelectStateBuilder");

    /// <summary>
    ///     Builds the hand selection state from the current <see cref="NPlayerHand" /> instance.
    ///     Returns null if not in hand card selection mode.
    /// </summary>
    public static HandSelectStateDto? Build()
    {
        var hand = NPlayerHand.Instance;
        if (hand == null || !hand.IsInCardSelection)
            return null;

        try
        {
            var mode = hand.CurrentMode;
            var prefs = GetPrefs(hand);
            var selectedCards = GetSelectedCards(hand);

            var dto = new HandSelectStateDto
            {
                Mode = mode.ToString(),
                Prompt = GetPromptText(hand),
                MinSelect = prefs?.MinSelect ?? 0,
                MaxSelect = prefs?.MaxSelect ?? 0,
                Cancelable = prefs?.Cancelable ?? false,
                RequireManualConfirmation = prefs?.RequireManualConfirmation ?? false,
                SelectedCount = selectedCards?.Count ?? 0,
                CanConfirm = IsConfirmEnabled(hand)
            };

            // Build selectable cards (visible hand card holders that pass the filter)
            dto.SelectableCards = BuildSelectableCards(hand);

            // Build selected cards
            dto.SelectedCards = BuildSelectedCards(hand, selectedCards);

            return dto;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build hand select state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the <see cref="CardSelectorPrefs" /> from the private <c>_prefs</c> field.
    /// </summary>
    internal static CardSelectorPrefs? GetPrefs(NPlayerHand hand)
    {
        try
        {
            var field = typeof(NPlayerHand).GetField("_prefs",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field == null ? null : (CardSelectorPrefs?)field.GetValue(hand);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get _prefs: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the selected cards list from the private <c>_selectedCards</c> field.
    /// </summary>
    internal static List<CardModel>? GetSelectedCards(NPlayerHand hand)
    {
        try
        {
            var field = typeof(NPlayerHand).GetField("_selectedCards",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(hand) as List<CardModel>;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get _selectedCards: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the prompt text from the <c>_selectionHeader</c> MegaRichTextLabel.
    /// </summary>
    private static string? GetPromptText(NPlayerHand hand)
    {
        try
        {
            var prefs = GetPrefs(hand);
            if (prefs == null) return null;

            var promptText = prefs.Value.Prompt.GetFormattedText();
            return TextUtils.StripGameTags(promptText);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get prompt text: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Checks if the confirm button is currently enabled.
    /// </summary>
    private static bool IsConfirmEnabled(NPlayerHand hand)
    {
        try
        {
            var confirmButton = hand.GetNodeOrNull<Godot.Control>("%SelectModeConfirmButton");
            if (confirmButton == null) return false;

            // NConfirmButton.IsEnabled is not directly accessible; check if Disabled property is false
            // NConfirmButton inherits from NClickableControl which has a _disabled field
            var disabledField = confirmButton.GetType().GetField("_disabled",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (disabledField != null)
            {
                var disabled = disabledField.GetValue(confirmButton) as bool?;
                return disabled == false;
            }

            return false;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to check confirm button state: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Builds the list of selectable cards from the hand's <see cref="NPlayerHand.ActiveHolders" />.
    ///     ActiveHolders returns only visible holders (cards that pass the selection filter).
    /// </summary>
    private static List<HandSelectCardDto> BuildSelectableCards(NPlayerHand hand)
    {
        var cards = new List<HandSelectCardDto>();
        try
        {
            var activeHolders = hand.ActiveHolders;
            for (var i = 0; i < activeHolders.Count; i++)
            {
                var holder = activeHolders[i];
                var cardModel = holder.CardNode?.Model;
                if (cardModel == null) continue;

                cards.Add(new HandSelectCardDto
                {
                    Index = i,
                    CardId = cardModel.Id.Entry,
                    CardName = TextUtils.StripGameTags(cardModel.Title),
                    CardType = cardModel.Type.ToString(),
                    Cost = cardModel.EnergyCost.Canonical,
                    Description = TextUtils.StripGameTags(cardModel.Description.GetFormattedText())
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to build selectable cards: {ex.Message}");
        }

        return cards;
    }

    /// <summary>
    ///     Builds the list of already-selected cards from the <c>_selectedCards</c> field.
    /// </summary>
    private static List<HandSelectCardDto> BuildSelectedCards(NPlayerHand hand, List<CardModel>? selectedCards)
    {
        var cards = new List<HandSelectCardDto>();
        if (selectedCards == null) return cards;

        try
        {
            for (var i = 0; i < selectedCards.Count; i++)
            {
                var cardModel = selectedCards[i];
                cards.Add(new HandSelectCardDto
                {
                    Index = i,
                    CardId = cardModel.Id.Entry,
                    CardName = TextUtils.StripGameTags(cardModel.Title),
                    CardType = cardModel.Type.ToString(),
                    Cost = cardModel.EnergyCost.Canonical,
                    Description = TextUtils.StripGameTags(cardModel.Description.GetFormattedText())
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to build selected cards: {ex.Message}");
        }

        return cards;
    }
}
