using System.Reflection;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models;
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

    private static readonly FieldInfo? PrefsField =
        typeof(NPlayerHand).GetField("_prefs", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? SelectedCardsField =
        typeof(NPlayerHand).GetField("_selectedCards", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    ///     Cached PropertyInfo for NClickableControl.IsEnabled (public).
    ///     Lazily resolved on first use since the confirm button type is only available at runtime.
    /// </summary>
    private static PropertyInfo? _confirmIsEnabledProp;

    /// <summary>
    ///     Builds the hand selection state from the current <see cref="NPlayerHand" /> instance.
    ///     Returns null if not in hand card selection mode.
    /// </summary>
    public static HandSelectStateDto? Build()
    {
        var hand = NPlayerHand.Instance;
        if (hand is not { IsInCardSelection: true })
            return null;

        try
        {
            var prefs = GetPrefs(hand);
            var selectedCards = GetSelectedCards(hand);

            return new HandSelectStateDto
            {
                Mode = hand.CurrentMode.ToString(),
                Prompt = prefs.HasValue
                    ? TextUtils.StripGameTags(prefs.Value.Prompt.GetFormattedText())
                    : null,
                MinSelect = prefs?.MinSelect ?? 0,
                MaxSelect = prefs?.MaxSelect ?? 0,
                Cancelable = prefs?.Cancelable ?? false,
                RequireManualConfirmation = prefs?.RequireManualConfirmation ?? false,
                SelectedCount = selectedCards?.Count ?? 0,
                CanConfirm = GetConfirmEnabled(hand),
                SelectableCards = BuildSelectableCards(hand),
                SelectedCards = BuildSelectedCards(selectedCards)
            };
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
            return PrefsField == null ? null : (CardSelectorPrefs?)PrefsField.GetValue(hand);
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
            return SelectedCardsField?.GetValue(hand) as List<CardModel>;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get _selectedCards: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Checks if the confirm button is currently enabled via the IsEnabled property.
    /// </summary>
    private static bool GetConfirmEnabled(NPlayerHand hand)
    {
        try
        {
            var confirmButton = hand.GetNodeOrNull<Godot.Control>("%SelectModeConfirmButton");
            if (confirmButton == null) return false;

            _confirmIsEnabledProp ??= confirmButton.GetType().GetProperty("IsEnabled",
                BindingFlags.Public | BindingFlags.Instance);

            return _confirmIsEnabledProp?.GetValue(confirmButton) as bool? ?? false;
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
    private static List<HandSelectCardDto> BuildSelectedCards(List<CardModel>? selectedCards)
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
