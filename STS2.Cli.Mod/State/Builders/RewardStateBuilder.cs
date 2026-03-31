using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds <see cref="RewardStateDto" /> from the <see cref="NRewardsScreen" /> overlay.
///     Traverses the reward screen's UI buttons to extract reward data from the underlying
///     <see cref="Reward" /> objects.
/// </summary>
public static class RewardStateBuilder
{
    private static readonly ModLogger Logger = new("RewardStateBuilder");

    /// <summary>
    ///     Cached reflection field for <see cref="RelicReward" />._relic (private).
    ///     <c>ClaimedRelic</c> is only set after claim, so we must use reflection to read
    ///     the relic before the player claims it.
    /// </summary>
    private static readonly FieldInfo? RelicField =
        typeof(RelicReward).GetField("_relic", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    ///     Cached reflection field for <see cref="SpecialCardReward" />._card (private readonly).
    /// </summary>
    private static readonly FieldInfo? SpecialCardField =
        typeof(SpecialCardReward).GetField("_card", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    ///     Builds the reward state from the current <see cref="NRewardsScreen" />.
    ///     Returns null if the reward screen is not found or has no rewards container.
    /// </summary>
    public static RewardStateDto? Build()
    {
        var screen = CommonUiUtils.FindScreenInOverlay<NRewardsScreen>();
        if (screen == null)
        {
            Logger.Warning("NRewardsScreen not found in overlay stack");
            return null;
        }

        var result = new RewardStateDto();

        // Access the rewards container via Godot unique name path
        // NRewardsScreen._Ready() sets: _rewardsContainer = GetNode<Control>("%RewardsContainer")
        Control? rewardsContainer;
        try
        {
            rewardsContainer = screen.GetNode<Control>("%RewardsContainer");
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to access RewardsContainer: {ex.Message}");
            return result;
        }

        if (rewardsContainer == null)
        {
            Logger.Warning("RewardsContainer is null");
            return result;
        }

        // Iterate reward button children (same pattern as NRewardsScreen.AfterOverlayClosed)
        var index = 0;
        foreach (var child in rewardsContainer.GetChildren())
            if (child is NRewardButton rewardButton)
                try
                {
                    var reward = rewardButton.Reward;
                    if (reward == null) continue;

                    var item = BuildRewardItem(reward, index);
                    result.Rewards.Add(item);
                    index++;
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to build reward item at index {index}: {ex.Message}");
                    index++;
                }

        // NLinkedRewardSet (mutually-exclusive grouped rewards linked by chains) is fully
        // implemented in the game code but never instantiated in the current version.
        // No code path calls `new LinkedRewardSet(...)`, so we skip it for now.
        return result;
    }

    /// <summary>
    ///     Builds a single <see cref="RewardItemDto" /> from a <see cref="Reward" /> object
    ///     using pattern matching to extract type-specific fields.
    /// </summary>
    private static RewardItemDto BuildRewardItem(Reward reward, int index)
    {
        var item = new RewardItemDto
        {
            Index = index,
            Type = GetRewardTypeName(reward),
            Description = SafeGetDescription(reward)
        };

        switch (reward)
        {
            case GoldReward gold:
                BuildGoldFields(gold, item);
                break;

            case PotionReward potion:
                BuildPotionFields(potion, item);
                break;

            case RelicReward relic:
                BuildRelicFields(relic, item);
                break;

            case CardReward card:
                BuildCardFields(card, item);
                break;

            case SpecialCardReward specialCard:
                BuildSpecialCardFields(specialCard, item);
                break;

            case CardRemovalReward:
                // No extra fields — type and description are sufficient
                break;

            default:
                Logger.Warning($"Unknown reward type at index {index}: {reward.GetType().Name}");
                break;
        }

        return item;
    }

    /// <summary>
    ///     Gets the reward type name string for the JSON output.
    /// </summary>
    private static string GetRewardTypeName(Reward reward)
    {
        return reward switch
        {
            GoldReward => "Gold",
            PotionReward => "Potion",
            RelicReward => "Relic",
            CardReward => "Card",
            SpecialCardReward => "SpecialCard",
            CardRemovalReward => "CardRemoval",
            _ => reward.GetType().Name
        };
    }

    /// <summary>
    ///     Safely reads the localized description from a reward.
    /// </summary>
    private static string SafeGetDescription(Reward reward)
    {
        try
        {
            return StripGameTags(reward.Description.GetFormattedText());
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get reward description: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    ///     Populates Gold-specific fields on the reward item.
    /// </summary>
    private static void BuildGoldFields(GoldReward gold, RewardItemDto item)
    {
        try
        {
            item.GoldAmount = gold.Amount;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read gold amount: {ex.Message}");
        }
    }

    /// <summary>
    ///     Populates Potion-specific fields on the reward item.
    /// </summary>
    private static void BuildPotionFields(PotionReward potionReward, RewardItemDto item)
    {
        try
        {
            var potion = potionReward.Potion;
            if (potion == null) return;

            item.PotionId = potion.Id.Entry;
            item.PotionName = StripGameTags(potion.Title.GetFormattedText());
            item.PotionRarity = potion.Rarity.ToString();
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read potion fields: {ex.Message}");
        }
    }

    /// <summary>
    ///     Populates Relic-specific fields on the reward item.
    ///     Uses reflection to access the private <c>_relic</c> field since <c>ClaimedRelic</c>
    ///     is only populated after claiming the reward.
    /// </summary>
    private static void BuildRelicFields(RelicReward relicReward, RewardItemDto item)
    {
        try
        {
            // _relic is private — use reflection
            // Fallback: try ClaimedRelic (set after claim)
            var relic = RelicField?.GetValue(relicReward) as RelicModel ?? relicReward.ClaimedRelic;
            if (relic == null) return;

            item.RelicId = relic.Id.Entry;
            item.RelicName = StripGameTags(relic.Title.GetFormattedText());
            item.RelicDescription = StripGameTags(relic.DynamicDescription.GetFormattedText());
            item.RelicRarity = relic.Rarity.ToString();
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read relic fields: {ex.Message}");
        }
    }

    /// <summary>
    ///     Populates Card-specific fields on the reward item.
    ///     Reads the card choices from <see cref="CardReward.Cards" />.
    /// </summary>
    private static void BuildCardFields(CardReward cardReward, RewardItemDto item)
    {
        try
        {
            var cards = cardReward.Cards;

            item.CardChoices = [];
            var cardIndex = 0;
            foreach (var card in cards)
            {
                try
                {
                    item.CardChoices.Add(BuildCardChoice(card, cardIndex));
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to build card choice at index {cardIndex}: {ex.Message}");
                }

                cardIndex++;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read card choices: {ex.Message}");
        }
    }

    /// <summary>
    ///     Populates SpecialCard-specific fields on the reward item.
    ///     Uses reflection to access the private <c>_card</c> field.
    /// </summary>
    private static void BuildSpecialCardFields(SpecialCardReward specialCardReward, RewardItemDto item)
    {
        try
        {
            if (SpecialCardField?.GetValue(specialCardReward) is not CardModel card) return;

            item.CardId = card.Id.Entry;
            item.CardName = StripGameTags(card.Title);
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read special card fields: {ex.Message}");
        }
    }

    /// <summary>
    ///     Builds a <see cref="CardChoiceDto" /> from a <see cref="CardModel" />.
    /// </summary>
    private static CardChoiceDto BuildCardChoice(CardModel card, int index)
    {
        var choice = new CardChoiceDto
        {
            Index = index,
            Id = card.Id.Entry,
            Name = StripGameTags(card.Title),
            Description = StripGameTags(SafeGetCardDescription(card)),
            Type = card.Type.ToString(),
            Rarity = card.Rarity.ToString(),
            IsUpgraded = card.IsUpgraded
        };

        // Energy cost
        try
        {
            if (card.EnergyCost.CostsX)
                choice.Cost = -1;
            else
                choice.Cost = card.EnergyCost.GetAmountToSpend();
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to read card cost for {card.Id}: {ex.Message}");
        }

        return choice;
    }

    /// <summary>
    ///     Safely gets the resolved card description.
    /// </summary>
    private static string SafeGetCardDescription(CardModel card)
    {
        try
        {
            return card.GetDescriptionForPile(PileType.Hand);
        }
        catch
        {
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
