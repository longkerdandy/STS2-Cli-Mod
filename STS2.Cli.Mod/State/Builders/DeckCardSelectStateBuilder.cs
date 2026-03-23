using System.Reflection;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2.Cli.Mod.Models.Dto;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Extracts deck card selection state from <see cref="NCardGridSelectionScreen" /> subtypes.
///     Handles: NDeckCardSelectScreen (remove), NDeckUpgradeSelectScreen (upgrade),
///     NDeckTransformSelectScreen (transform), NDeckEnchantSelectScreen (enchant),
///     NSimpleCardSelectScreen (generic).
/// </summary>
public static class DeckCardSelectStateBuilder
{
    private static readonly ModLogger Logger = new("DeckCardSelectStateBuilder");

    /// <summary>
    ///     Builds the deck card selection state DTO from the given screen.
    /// </summary>
    /// <param name="screen">A <see cref="NCardGridSelectionScreen" /> subtype from the overlay stack.</param>
    /// <returns>DTO with selectable cards, constraints, and selection type.</returns>
    public static DeckCardSelectStateDto? Build(NCardGridSelectionScreen screen)
    {
        try
        {
            // Get the cards list from the protected _cards field on the base class
            var cards = GetCards(screen);
            if (cards == null || cards.Count == 0)
            {
                Logger.Warning("No cards found on grid selection screen");
                return null;
            }

            // Get CardSelectorPrefs from the private _prefs field on the concrete subclass
            var prefs = GetPrefs(screen);

            // Get card holders from the grid for index info
            var grid = GetGrid(screen);

            // Determine selection type from the screen type
            var selectionType = InferSelectionType(screen);

            // Get prompt text
            string? prompt = null;
            if (prefs.HasValue)
            {
                try
                {
                    prompt = TextUtils.StripGameTags(prefs.Value.Prompt.GetFormattedText());
                }
                catch
                {
                    // LocString may fail if key doesn't exist
                    Logger.Warning("Failed to get prompt text from CardSelectorPrefs");
                }
            }

            // Build card DTOs
            var cardDtos = new List<SelectableCardDto>();
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                try
                {
                    cardDtos.Add(new SelectableCardDto
                    {
                        Index = i,
                        CardId = card.Id.Entry,
                        CardName = TextUtils.StripGameTags(card.Title),
                        CardType = card.Type.ToString(),
                        Cost = card.EnergyCost.Canonical,
                        Description = TextUtils.StripGameTags(card.Description.GetFormattedText())
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to build card DTO at index {i}: {ex.Message}");
                    cardDtos.Add(new SelectableCardDto
                    {
                        Index = i,
                        CardId = card.Id.Entry,
                        CardName = card.Id.Entry,
                        CardType = "Unknown"
                    });
                }
            }

            return new DeckCardSelectStateDto
            {
                SelectionType = selectionType,
                Prompt = prompt,
                MinSelect = prefs?.MinSelect ?? 1,
                MaxSelect = prefs?.MaxSelect ?? 1,
                Cancelable = prefs?.Cancelable ?? false,
                Cards = cardDtos
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build deck card select state: {ex.Message}");
            return null;
        }
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
            Logger.Warning($"Failed to get _cards via reflection: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the <see cref="CardSelectorPrefs" /> from the private <c>_prefs</c> field
    ///     on the concrete subclass.
    /// </summary>
    private static CardSelectorPrefs? GetPrefs(NCardGridSelectionScreen screen)
    {
        try
        {
            // _prefs is declared on each concrete subclass, not on the base
            var field = screen.GetType().GetField("_prefs",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Logger.Warning($"_prefs field not found on {screen.GetType().Name}");
                return null;
            }

            return (CardSelectorPrefs?)field.GetValue(screen);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get _prefs via reflection: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the <see cref="NCardGrid" /> from the protected <c>_grid</c> field on the base class.
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
            Logger.Warning($"Failed to get _grid via reflection: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Infers the selection type from the concrete screen type.
    /// </summary>
    private static string InferSelectionType(NCardGridSelectionScreen screen)
    {
        return screen switch
        {
            NDeckCardSelectScreen => "remove",
            NDeckUpgradeSelectScreen => "upgrade",
            NDeckTransformSelectScreen => "transform",
            NDeckEnchantSelectScreen => "enchant",
            NSimpleCardSelectScreen => "generic",
            _ => "unknown"
        };
    }
}
