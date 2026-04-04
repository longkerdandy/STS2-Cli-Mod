using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;
using static STS2.Cli.Mod.Utils.TextUtils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds <see cref="ShopStateDto" /> from the current <see cref="NMerchantRoom" />.
///     Reads the <see cref="MegaCrit.Sts2.Core.Entities.Merchant.MerchantInventory" /> data model
///     to extract cards, relics, potions, and card removal service info.
/// </summary>
public static class ShopStateBuilder
{
    private static readonly ModLogger Logger = new("ShopStateBuilder");

    /// <summary>
    ///     Builds the shop state from the current <see cref="NMerchantRoom" />.
    ///     Returns null if the merchant room is not found or not in the scene tree.
    /// </summary>
    public static ShopStateDto? Build()
    {
        try
        {
            var merchantRoom = NRun.Instance?.MerchantRoom;
            if (merchantRoom == null || !merchantRoom.IsInsideTree())
            {
                Logger.Warning("NMerchantRoom is null or not in tree");
                return null;
            }

            var inventory = merchantRoom.Room.Inventory;

            var cards = BuildCards(inventory);
            var relics = BuildRelics(inventory);
            var potions = BuildPotions(inventory);
            var cardRemoval = BuildCardRemoval(inventory);
            var playerGold = inventory.Player?.Gold ?? 0;

            // Check proceed button state
            var canProceed = false;
            var proceedButton = merchantRoom.ProceedButton;
            canProceed = proceedButton.IsEnabled;

            Logger.Info($"Built shop state: cards={cards.Count}, relics={relics.Count}, potions={potions.Count}, " +
                        $"cardRemoval={cardRemoval != null}, gold={playerGold}, canProceed={canProceed}");

            return new ShopStateDto
            {
                Cards = cards,
                Relics = relics,
                Potions = potions,
                CardRemoval = cardRemoval,
                PlayerGold = playerGold,
                CanProceed = canProceed
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build shop state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Builds the list of card DTOs from the merchant inventory.
    ///     Combines character cards and colorless cards into a single indexed list.
    /// </summary>
    private static List<ShopCardDto> BuildCards(
        MegaCrit.Sts2.Core.Entities.Merchant.MerchantInventory inventory)
    {
        var cards = new List<ShopCardDto>();
        var index = 0;

        foreach (var entry in inventory.CardEntries)
        {
            try
            {
                var card = entry.CreationResult?.Card;
                cards.Add(new ShopCardDto
                {
                    Index = index,
                    CardId = card?.Id.Entry ?? string.Empty,
                    CardName = card != null ? StripGameTags(card.Title) : string.Empty,
                    Description = card != null
                        ? StripGameTags(card.Description.GetFormattedText())
                        : string.Empty,
                    CardType = card?.Type.ToString() ?? string.Empty,
                    Rarity = card?.Rarity.ToString() ?? string.Empty,
                    Cost = entry.Cost,
                    IsOnSale = entry.IsOnSale,
                    IsStocked = entry.IsStocked
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build shop card at index {index}: {ex.Message}");
            }

            index++;
        }

        return cards;
    }

    /// <summary>
    ///     Builds the list of relic DTOs from the merchant inventory.
    /// </summary>
    private static List<ShopRelicDto> BuildRelics(
        MegaCrit.Sts2.Core.Entities.Merchant.MerchantInventory inventory)
    {
        var relics = new List<ShopRelicDto>();

        for (var i = 0; i < inventory.RelicEntries.Count; i++)
        {
            try
            {
                var entry = inventory.RelicEntries[i];
                var model = entry.Model;
                relics.Add(new ShopRelicDto
                {
                    Index = i,
                    RelicId = model?.Id.Entry ?? string.Empty,
                    RelicName = model != null
                        ? StripGameTags(model.Title.GetFormattedText())
                        : string.Empty,
                    Description = model != null
                        ? StripGameTags(model.DynamicDescription.GetFormattedText())
                        : string.Empty,
                    Rarity = model?.Rarity.ToString() ?? string.Empty,
                    Cost = entry.Cost,
                    IsStocked = entry.IsStocked
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build shop relic at index {i}: {ex.Message}");
            }
        }

        return relics;
    }

    /// <summary>
    ///     Builds the list of potion DTOs from the merchant inventory.
    /// </summary>
    private static List<ShopPotionDto> BuildPotions(
        MegaCrit.Sts2.Core.Entities.Merchant.MerchantInventory inventory)
    {
        var potions = new List<ShopPotionDto>();

        for (var i = 0; i < inventory.PotionEntries.Count; i++)
        {
            try
            {
                var entry = inventory.PotionEntries[i];
                var model = entry.Model;
                potions.Add(new ShopPotionDto
                {
                    Index = i,
                    PotionId = model?.Id.Entry ?? string.Empty,
                    PotionName = model != null
                        ? StripGameTags(model.Title.GetFormattedText())
                        : string.Empty,
                    Description = model != null
                        ? StripGameTags(model.DynamicDescription.GetFormattedText())
                        : string.Empty,
                    Rarity = model?.Rarity.ToString() ?? string.Empty,
                    Cost = entry.Cost,
                    IsStocked = entry.IsStocked
                });
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to build shop potion at index {i}: {ex.Message}");
            }
        }

        return potions;
    }

    /// <summary>
    ///     Builds the card removal service DTO, or null if not available.
    /// </summary>
    private static ShopCardRemovalDto? BuildCardRemoval(
        MegaCrit.Sts2.Core.Entities.Merchant.MerchantInventory inventory)
    {
        var entry = inventory.CardRemovalEntry;
        if (entry == null) return null;

        return new ShopCardRemovalDto
        {
            Cost = entry.Cost,
            IsUsed = entry.Used
        };
    }
}
