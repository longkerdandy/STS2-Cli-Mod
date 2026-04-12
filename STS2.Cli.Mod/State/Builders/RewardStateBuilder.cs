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
    ///     Builds the reward state from the current <see cref="NRewardsScreen" />.
    ///     Returns null if the reward screen is not found or has no rewards container.
    /// </summary>
    public static RewardStateDto? Build()
    {
        try
        {
            var screen = UiUtils.FindScreenInOverlay<NRewardsScreen>();
            if (screen == null)
            {
                Logger.Warning("NRewardsScreen not found in overlay stack");
                return null;
            }

            var result = new RewardStateDto();

            // Check if skipping is disallowed (e.g., NeowsBones relic rewards)
            var skipDisallowed = UiUtils.GetPrivateFieldValue<bool>(screen, "_skipDisallowed") ?? false;
            result.CanSkip = !skipDisallowed;

            var rewardsContainer = screen.GetNodeOrNull<Control>("%RewardsContainer");
            if (rewardsContainer == null)
            {
                Logger.Warning("RewardsContainer is null");
                return result;
            }

            // Iterate reward button children (same pattern as NRewardsScreen.AfterOverlayClosed)
            var index = 0;
            foreach (var child in rewardsContainer.GetChildren())
            {
                if (child is not NRewardButton rewardButton) continue;
                var reward = rewardButton.Reward;
                if (reward == null) continue;

                try
                {
                    result.Rewards.Add(BuildRewardItem(reward, index));
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to build reward item at index {index}: {ex.Message}");
                }

                index++;
            }

            // NLinkedRewardSet (mutually-exclusive grouped rewards linked by chains) is fully
            // implemented in the game code but never instantiated in the current version.
            // No code path calls `new LinkedRewardSet(...)`, so we skip it for now.
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build reward state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Builds a single <see cref="RewardItemDto" /> from a <see cref="Reward" /> object
    ///     using pattern matching to extract type-specific fields.
    /// </summary>
    private static RewardItemDto BuildRewardItem(Reward reward, int index)
    {
        var typeName = reward switch
        {
            GoldReward => "Gold",
            PotionReward => "Potion",
            RelicReward => "Relic",
            CardReward => "Card",
            SpecialCardReward => "SpecialCard",
            CardRemovalReward => "CardRemoval",
            _ => reward.GetType().Name
        };

        var item = new RewardItemDto
        {
            Index = index,
            Type = typeName,
            Description = StripGameTags(reward.Description.GetFormattedText())
        };

        switch (reward)
        {
            case GoldReward gold:
                item.GoldAmount = gold.Amount;
                break;

            case PotionReward potionReward:
            {
                var potion = potionReward.Potion;
                if (potion != null)
                {
                    item.PotionId = potion.Id.Entry;
                    item.PotionName = StripGameTags(potion.Title.GetFormattedText());
                    item.PotionRarity = potion.Rarity.ToString();
                }

                break;
            }

            case RelicReward relicReward:
            {
                // _relic is private — use reflection; fallback to ClaimedRelic (set after claim)
                var relic = UiUtils.GetPrivateField<RelicModel>(relicReward, "_relic") ?? relicReward.ClaimedRelic;
                if (relic != null)
                {
                    item.RelicId = relic.Id.Entry;
                    item.RelicName = StripGameTags(relic.Title.GetFormattedText());
                    item.RelicDescription = StripGameTags(relic.DynamicDescription.GetFormattedText());
                    item.RelicRarity = relic.Rarity.ToString();
                }

                break;
            }

            case CardReward cardReward:
            {
                item.CardChoices = [];
                var cardIndex = 0;
                foreach (var card in cardReward.Cards)
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

                break;
            }

            case SpecialCardReward specialCardReward:
            {
                if (UiUtils.GetPrivateField<CardModel>(specialCardReward, "_card") is { } card)
                {
                    item.CardId = card.Id.Entry;
                    item.CardName = StripGameTags(card.Title);
                }

                break;
            }

            case CardRemovalReward:
                break;

            default:
                Logger.Warning($"Unknown reward type at index {index}: {reward.GetType().Name}");
                break;
        }

        return item;
    }

    /// <summary>
    ///     Builds a <see cref="CardChoiceDto" /> from a <see cref="CardModel" />.
    /// </summary>
    private static CardChoiceDto BuildCardChoice(CardModel card, int index)
    {
        return new CardChoiceDto
        {
            Index = index,
            Id = card.Id.Entry,
            Name = StripGameTags(card.Title),
            Description = StripGameTags(GetCardDescription(card)),
            Type = card.Type.ToString(),
            Rarity = card.Rarity.ToString(),
            IsUpgraded = card.IsUpgraded,
            Cost = card.EnergyCost.CostsX ? -1 : card.EnergyCost.GetAmountToSpend()
        };
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