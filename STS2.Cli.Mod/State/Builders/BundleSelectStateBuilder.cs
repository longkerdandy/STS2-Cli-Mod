using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds the bundle selection screen state DTO from <see cref="NChooseABundleSelectionScreen" />.
///     Extracts available bundles, their cards, preview state, and button availability
///     from the overlay screen triggered by the Scroll Boxes relic.
/// </summary>
public static class BundleSelectStateBuilder
{
    private static readonly ModLogger Logger = new("BundleSelectStateBuilder");

    /// <summary>
    ///     Builds the bundle selection state from the currently open <see cref="NChooseABundleSelectionScreen" />.
    ///     Finds the screen via <see cref="CommonUiUtils.FindScreenInOverlay{T}" />.
    ///     Returns null if no screen is found.
    /// </summary>
    public static BundleSelectStateDto? Build()
    {
        var screen = CommonUiUtils.FindScreenInOverlay<NChooseABundleSelectionScreen>();
        if (screen == null)
        {
            Logger.Warning("No NChooseABundleSelectionScreen found in overlay stack");
            return null;
        }

        return Build(screen);
    }

    /// <summary>
    ///     Builds the bundle selection state from the given <see cref="NChooseABundleSelectionScreen" />.
    /// </summary>
    /// <param name="screen">The bundle selection screen to extract data from.</param>
    /// <returns>DTO with bundles, preview state, and button availability; null on failure.</returns>
    public static BundleSelectStateDto? Build(NChooseABundleSelectionScreen screen)
    {
        try
        {
            // Extract all NCardBundle nodes from the screen
            var bundleNodes = CommonUiUtils.FindAll<NCardBundle>(screen);
            var bundles = new List<BundleDto>();

            for (var i = 0; i < bundleNodes.Count; i++)
            {
                var bundleNode = bundleNodes[i];
                var cards = new List<SelectableCardDto>();

                // Each NCardBundle.Bundle is IReadOnlyList<CardModel>
                var cardModels = bundleNode.Bundle;
                if (cardModels != null)
                {
                    for (var j = 0; j < cardModels.Count; j++)
                    {
                        var card = cardModels[j];
                        cards.Add(new SelectableCardDto
                        {
                            Index = j,
                            CardId = card.Id.Entry,
                            CardName = StripGameTags(card.Title),
                            CardType = card.Type.ToString(),
                            Cost = card.EnergyCost.Canonical,
                            Description = StripGameTags(card.Description.GetFormattedText())
                        });
                    }
                }

                bundles.Add(new BundleDto
                {
                    Index = i,
                    CardCount = cards.Count,
                    Cards = cards
                });
            }

            // Check preview state via %BundlePreviewContainer visibility
            var previewContainer = screen.GetNodeOrNull<Control>("%BundlePreviewContainer");
            var previewShowing = previewContainer?.Visible == true;

            // Extract preview cards from %Cards container if preview is showing
            var previewCards = new List<SelectableCardDto>();
            if (previewShowing)
            {
                var cardsContainer = screen.GetNodeOrNull<Control>("%Cards");
                if (cardsContainer != null)
                {
                    var previewHolders = CommonUiUtils.FindAll<NPreviewCardHolder>(cardsContainer);
                    for (var i = 0; i < previewHolders.Count; i++)
                    {
                        var card = previewHolders[i].CardModel;
                        if (card == null) continue;

                        previewCards.Add(new SelectableCardDto
                        {
                            Index = i,
                            CardId = card.Id.Entry,
                            CardName = StripGameTags(card.Title),
                            CardType = card.Type.ToString(),
                            Cost = card.EnergyCost.Canonical,
                            Description = StripGameTags(card.Description.GetFormattedText())
                        });
                    }
                }
            }

            // Check button states
            var confirmButton = screen.GetNodeOrNull<NConfirmButton>("%Confirm");
            var cancelButton = screen.GetNodeOrNull<NBackButton>("%Cancel");

            return new BundleSelectStateDto
            {
                Bundles = bundles,
                PreviewShowing = previewShowing,
                PreviewCards = previewCards,
                CanConfirm = confirmButton?.IsEnabled == true,
                CanCancel = cancelButton?.IsEnabled == true
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build bundle select state: {ex.Message}");
            return null;
        }
    }
}
