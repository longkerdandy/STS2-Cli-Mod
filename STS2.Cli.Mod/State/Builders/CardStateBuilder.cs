using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2.Cli.Mod.State;
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
    public static CardStateDto Build(CardModel card, int index)
    {
        // Index + Identity + Classification + Upgrade (all from safe non-nullable properties)
        var state = new CardStateDto
        {
            Index = index,
            Id = card.Id.Entry,
            Name = StripGameTags(card.Title),
            Description = StripGameTags(SafeGetCardDescription(card)),
            Type = card.Type.ToString(),
            Rarity = card.Rarity.ToString(),
            TargetType = card.TargetType.ToString(),
            IsUpgraded = card.IsUpgraded
        };

        // Cost — Energy
        try
        {
            if (card.EnergyCost.CostsX)
                state.Cost = -1;
            else
                state.Cost = card.EnergyCost.GetAmountToSpend();
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read energy cost for {card.Id}: {ex.Message}");
        }

        // Cost — Star (null = no star cost, -1 = X-star)
        try
        {
            if (card.CanonicalStarCost >= 0)
            {
                if (card.HasStarCostX)
                    state.StarCost = -1;
                else
                    state.StarCost = card.GetStarCostWithModifiers();
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read star cost for {card.Id}: {ex.Message}");
        }

        // Keywords
        try
        {
            foreach (var keyword in card.Keywords)
            {
                if (keyword != CardKeyword.None)
                    state.Keywords.Add(keyword.ToString());
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read keywords for {card.Id}: {ex.Message}");
        }

        // Tags
        try
        {
            foreach (var tag in card.Tags)
            {
                if (tag != CardTag.None)
                    state.Tags.Add(tag.ToString());
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read tags for {card.Id}: {ex.Message}");
        }

        // DynamicVars — Damage (preview value after all modifiers)
        try
        {
            if (card.DynamicVars.TryGetValue("Damage", out var damageVar))
                state.Damage = (int)damageVar.PreviewValue;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read damage for {card.Id}: {ex.Message}");
        }

        // DynamicVars — Block (preview value after all modifiers)
        try
        {
            if (card.DynamicVars.TryGetValue("Block", out var blockVar))
                state.Block = (int)blockVar.PreviewValue;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read block for {card.Id}: {ex.Message}");
        }

        // Enchantment
        try
        {
            if (card.Enchantment is { } enchantment)
                state.Enchantment = enchantment.Id.Entry;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read enchantment for {card.Id}: {ex.Message}");
        }

        // Affliction
        try
        {
            if (card.Affliction is { } affliction)
                state.Affliction = affliction.Id.Entry;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read affliction for {card.Id}: {ex.Message}");
        }

        // Playability
        try
        {
            card.CanPlay(out var unplayableReason, out _);
            state.CanPlay = unplayableReason == UnplayableReason.None;
            state.UnplayableReason = unplayableReason != UnplayableReason.None
                ? unplayableReason.ToString()
                : null;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to check playability for {card.Id}: {ex.Message}");
        }

        return state;
    }

    /// <summary>
    ///     Safely gets the resolved card description using <see cref="CardModel.GetDescriptionForPile" />.
    ///     This method resolves dynamic variables like damage and block to actual values.
    /// </summary>
    private static string SafeGetCardDescription(CardModel card)
    {
        try
        {
            // GetDescriptionForPile resolves dynamic variables based on pile context
            return card.GetDescriptionForPile(PileType.Hand);
        }
        catch
        {
            // Fallback to the basic description LocString
            try
            {
                return card.Description.GetFormattedText();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
