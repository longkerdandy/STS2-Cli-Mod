using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2.Cli.Mod.State.Dto;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds CardStateDto from CardModel.
/// </summary>
public static class CardStateBuilder
{
    /// <summary>
    ///     Builds a card state DTO from a CardModel.
    /// </summary>
    public static CardStateDto Build(CardModel card, int index)
    {
        var state = new CardStateDto
        {
            Index = index,
            Id = card.Id.Entry,
            Name = card.Title,
            IsUpgraded = card.IsUpgraded
        };

        try
        {
            // Cost display
            if (card.EnergyCost.CostsX)
            {
                state.Cost = -1; // X cost represented as -1
                state.CostDisplay = "X";
            }
            else
            {
                int cost = card.EnergyCost.GetAmountToSpend();
                state.Cost = cost;
                state.CostDisplay = cost.ToString();
            }

            // Can play check
            card.CanPlay(out var unplayableReason, out _);
            state.CanPlay = unplayableReason == UnplayableReason.None;
            state.UnplayableReason = unplayableReason != UnplayableReason.None ? unplayableReason.ToString() : null;

            // Description - use GetDescriptionForPile for resolved dynamic vars
            state.Description = CleanGameText(SafeGetCardDescription(card));

            // Type
            state.Type = card.Type.ToString();
        }
        catch (Exception ex)
        {
            // Log error but return partial state
            System.Diagnostics.Debug.WriteLine($"Failed to build card state for {card.Id}: {ex.Message}");
        }

        return state;
    }

    /// <summary>
    ///     Safely gets the resolved card description using GetDescriptionForPile.
    ///     This method resolves dynamic variables like {Block:diff()} to actual values.
    /// </summary>
    private static string? SafeGetCardDescription(CardModel card)
    {
        try
        {
            // GetDescriptionForPile resolves dynamic variables based on context
            return card.GetDescriptionForPile(PileType.Hand);
        }
        catch
        {
            // Fallback to basic description
            try { return card.Description?.GetFormattedText(); }
            catch { return null; }
        }
    }
}
