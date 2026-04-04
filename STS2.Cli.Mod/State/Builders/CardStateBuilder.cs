using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds <see cref="CardStateDto" /> from <see cref="CardModel" />.
///     Assignment order matches <see cref="CardStateDto" /> field layout.
/// </summary>
public static class CardStateBuilder
{
    private static readonly ModLogger Logger = new("CardStateBuilder");

    /// <summary>
    ///     Builds a card state DTO from a <see cref="CardModel" /> at the given hand index.
    /// </summary>
    public static CardStateDto? Build(CardModel card, int index)
    {
        try
        {
            card.CanPlay(out var unplayableReason, out _);

            var state = new CardStateDto
            {
                Index = index,
                Id = card.Id.Entry,
                Name = StripGameTags(card.Title),
                Description = StripGameTags(GetCardDescription(card)),
                Type = card.Type.ToString(),
                Rarity = card.Rarity.ToString(),
                TargetType = card.TargetType.ToString(),
                IsUpgraded = card.IsUpgraded,
                Cost = card.EnergyCost.CostsX ? -1 : card.EnergyCost.GetAmountToSpend(),
                CanPlay = unplayableReason == UnplayableReason.None,
                UnplayableReason = unplayableReason != UnplayableReason.None
                    ? unplayableReason.ToString()
                    : null
            };

            // Star cost: null = no star cost, -1 = X-star
            if (card.CanonicalStarCost >= 0)
                state.StarCost = card.HasStarCostX ? -1 : card.GetStarCostWithModifiers();

            foreach (var keyword in card.Keywords)
                if (keyword != CardKeyword.None)
                    state.Keywords.Add(keyword.ToString());

            foreach (var tag in card.Tags)
                if (tag != CardTag.None)
                    state.Tags.Add(tag.ToString());

            if (card.DynamicVars.TryGetValue("Damage", out var damageVar))
                state.Damage = (int)damageVar.PreviewValue;

            if (card.DynamicVars.TryGetValue("Block", out var blockVar))
                state.Block = (int)blockVar.PreviewValue;

            if (card.Enchantment is { } enchantment)
                state.Enchantment = enchantment.Id.Entry;

            if (card.Affliction is { } affliction)
                state.Affliction = affliction.Id.Entry;

            return state;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build card state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Gets the resolved card description, falling back to raw formatted text.
    /// </summary>
    private static string GetCardDescription(CardModel card)
    {
        try
        {
            return card.GetDescriptionForPile(PileType.Hand);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get description for pile: {ex.Message}");
            try
            {
                return card.Description.GetFormattedText();
            }
            catch (Exception ex2)
            {
                Logger.Warning($"Failed to get formatted description: {ex2.Message}");
                return string.Empty;
            }
        }
    }
}