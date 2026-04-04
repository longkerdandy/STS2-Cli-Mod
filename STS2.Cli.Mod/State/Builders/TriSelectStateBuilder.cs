using System.Reflection;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds the tri-select (choose-a-card) state DTO from <see cref="NChooseACardSelectionScreen" />.
///     Extracts selectable cards, skip availability, and infers the selection type
///     from the card composition (e.g., all attacks → choose_from_pool_attack).
/// </summary>
public static class TriSelectStateBuilder
{
    private static readonly ModLogger Logger = new("TriSelectStateBuilder");

    /// <summary>
    ///     Builds the tri-select state from the currently open <see cref="NChooseACardSelectionScreen" />.
    ///     Finds the screen via <see cref="UiUtils.FindScreenInOverlay{T}" />.
    ///     Returns null if no screen is found.
    /// </summary>
    public static TriSelectStateDto? Build()
    {
        var screen = UiUtils.FindScreenInOverlay<NChooseACardSelectionScreen>();
        if (screen == null)
        {
            Logger.Warning("No NChooseACardSelectionScreen found in overlay stack");
            return null;
        }

        return Build(screen);
    }

    /// <summary>
    ///     Builds the tri-select state from the given <see cref="NChooseACardSelectionScreen" />.
    /// </summary>
    /// <param name="screen">The card selection screen to extract data from.</param>
    /// <returns>DTO with selectable cards, constraints, and selection type; null on failure.</returns>
    public static TriSelectStateDto? Build(NChooseACardSelectionScreen screen)
    {
        try
        {
            var cardHolders = UiUtils.FindAll<NCardHolder>(screen);
            var canSkip = ReadCanSkip(screen);
            var cards = new List<SelectableCardDto>();

            for (var i = 0; i < cardHolders.Count; i++)
            {
                var holder = cardHolders[i];
                var card = holder.CardModel;
                if (card == null) continue;

                cards.Add(new SelectableCardDto
                {
                    Index = i,
                    CardId = card.Id.Entry,
                    CardName = StripGameTags(card.Title),
                    CardType = card.Type.ToString(),
                    Cost = card.EnergyCost.Canonical,
                    Description = StripGameTags(card.Description.GetFormattedText())
                });
            }

            return new TriSelectStateDto
            {
                SelectionType = InferSelectionType(cards),
                MinSelect = canSkip ? 0 : 1,
                MaxSelect = 1,
                CanSkip = canSkip,
                Cards = cards
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build tri-select state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Infers the selection type from the available cards.
    ///     If all 3 cards share the same type, returns <c>choose_from_pool_{type}</c>.
    /// </summary>
    private static string InferSelectionType(List<SelectableCardDto> cards)
    {
        if (cards.Count == 3)
        {
            var types = cards.Select(c => c.CardType).Distinct().ToList();
            return types.Count == 1 ? $"choose_from_pool_{types[0]?.ToLowerInvariant()}" : "choose_from_pool";
        }

        return cards.Count > 3 ? "choose_from_hand" : "unknown";
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
        catch
        {
            return false;
        }
    }
}
