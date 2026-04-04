using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>shop_buy_card</c> CLI command.
///     Buys a card from the shop by card_id + nth.
///     Finds the matching <see cref="MerchantCardEntry" /> in the
///     <see cref="MerchantInventory.CardEntries" /> list and calls
///     <see cref="MerchantEntry.OnTryPurchaseWrapper" /> to purchase it.
/// </summary>
/// <remarks>
///     <para><b>CLI command:</b> <c>sts2 shop_buy_card &lt;card_id&gt; [--nth &lt;n&gt;]</c></para>
///     <para><b>Scene:</b> Merchant room (shop).</para>
/// </remarks>
public static class ShopBuyCardHandler
{
    private static readonly ModLogger Logger = new("ShopBuyCardHandler");

    /// <summary>
    ///     Executes the shop_buy_card command.
    ///     Must be called on the Godot main thread.
    /// </summary>
    public static async Task<object> ExecuteAsync(Request request)
    {
        if (string.IsNullOrEmpty(request.Id))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Card ID required" };

        var cardId = request.Id;
        var nth = request.Nth ?? 0;
        Logger.Info($"Requested to buy card: {cardId} (nth={nth})");
        
        try
        {
            // --- Guard: Check merchant room ---
            var merchantRoom = NMerchantRoom.Instance;
            if (merchantRoom == null || !merchantRoom.IsInsideTree())
                return new { ok = false, error = "NOT_IN_SHOP", message = "Not currently in a shop" };

            var inventory = merchantRoom.Room.Inventory;

            // --- Find the card entry by ID + nth ---
            var entry = FindCardEntry(inventory, cardId, nth);
            if (entry == null)
                return new { ok = false, error = "ITEM_NOT_FOUND", message = $"Card '{cardId}' (nth={nth}) not found in shop" };

            // --- Guard: Check item is in stock ---
            if (!entry.IsStocked)
                return new { ok = false, error = "ITEM_SOLD_OUT", message = $"Card '{cardId}' is sold out" };

            // --- Guard: Check enough gold ---
            if (!entry.EnoughGold)
                return new { ok = false, error = "NOT_ENOUGH_GOLD", message = $"Not enough gold to buy card '{cardId}' (cost={entry.Cost})" };

            // --- Purchase ---
            var success = await entry.OnTryPurchaseWrapper(inventory);
            if (!success)
                return new { ok = false, error = "PURCHASE_FAILED", message = $"Failed to purchase card '{cardId}'" };

            Logger.Info($"Successfully purchased card: {cardId}");

            var screen = StateHandler.DetectScreen();
            return new
            {
                ok = true,
                data = new
                {
                    action = "SHOP_BUY_CARD",
                    card_id = cardId,
                    cost = entry.Cost,
                    screen
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to buy card: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Finds a card entry in the inventory by card_id and nth occurrence.
    /// </summary>
    private static MerchantCardEntry? FindCardEntry(MerchantInventory inventory, string cardId, int nth)
    {
        var count = 0;
        foreach (var entry in inventory.CardEntries)
        {
            var card = entry.CreationResult?.Card;
            if (card == null) continue;

            if (string.Equals(card.Id.Entry, cardId, StringComparison.OrdinalIgnoreCase))
            {
                if (count == nth)
                    return entry;
                count++;
            }
        }

        return null;
    }
}
