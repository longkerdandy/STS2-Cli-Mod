using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>shop_buy_potion</c> CLI command.
///     Buys a potion from the shop by potion_id + nth.
///     Finds the matching <see cref="MerchantPotionEntry" /> in the
///     <see cref="MerchantInventory.PotionEntries" /> list and calls
///     <see cref="MerchantEntry.OnTryPurchaseWrapper" /> to purchase it.
///     Note: potion purchase can fail with <see cref="PurchaseStatus.FailureSpace" />
///     if the player's potion belt is full.
/// </summary>
/// <remarks>
///     <para>
///         <b>CLI command:</b> <c>sts2 shop_buy_potion &lt;potion_id&gt; [--nth &lt;n&gt;]</c>
///     </para>
///     <para><b>Scene:</b> Merchant room (shop).</para>
/// </remarks>
public static class ShopBuyPotionHandler
{
    private static readonly ModLogger Logger = new("ShopBuyPotionHandler");

    /// <summary>
    ///     Executes the shop_buy_potion command.
    ///     Must be called on the Godot main thread.
    /// </summary>
    public static async Task<object> ExecuteAsync(Request request)
    {
        if (string.IsNullOrEmpty(request.Id))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Potion ID required" };

        var potionId = request.Id;
        var nth = request.Nth ?? 0;
        Logger.Info($"Requested to buy potion: {potionId} (nth={nth})");

        try
        {
            // --- Guard: Check merchant room ---
            var merchantRoom = NMerchantRoom.Instance;
            if (merchantRoom == null || !merchantRoom.IsInsideTree())
                return new { ok = false, error = "NOT_IN_SHOP", message = "Not currently in a shop" };

            var inventory = merchantRoom.Room.Inventory;

            // --- Find the potion entry by ID + nth ---
            var entry = FindPotionEntry(inventory, potionId, nth);
            if (entry == null)
                return new
                {
                    ok = false, error = "ITEM_NOT_FOUND", message = $"Potion '{potionId}' (nth={nth}) not found in shop"
                };

            // --- Guard: Check item is in stock ---
            if (!entry.IsStocked)
                return new { ok = false, error = "ITEM_SOLD_OUT", message = $"Potion '{potionId}' is sold out" };

            // --- Guard: Check enough gold ---
            if (!entry.EnoughGold)
                return new
                {
                    ok = false, error = "NOT_ENOUGH_GOLD",
                    message = $"Not enough gold to buy potion '{potionId}' (cost={entry.Cost})"
                };

            // --- Purchase ---
            // OnTryPurchaseWrapper handles the full flow: PotionCmd.TryToProcure, gold deduction, etc.
            // It returns false if the potion belt is full (FailureSpace) or purchase is forbidden.
            var success = await entry.OnTryPurchaseWrapper(inventory);
            if (!success)
                return new
                {
                    ok = false, error = "POTION_BELT_FULL",
                    message = $"Failed to purchase potion '{potionId}' (potion belt may be full)"
                };

            Logger.Info($"Successfully purchased potion: {potionId}");

            var screen = StateHandler.DetectScreen();
            return new
            {
                ok = true,
                data = new
                {
                    action = "SHOP_BUY_POTION",
                    potion_id = potionId,
                    cost = entry.Cost,
                    screen
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to buy potion: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Finds a potion entry in the inventory by potion_id and nth occurrence.
    /// </summary>
    private static MerchantPotionEntry? FindPotionEntry(MerchantInventory inventory, string potionId, int nth)
    {
        var count = 0;
        foreach (var entry in inventory.PotionEntries)
        {
            var model = entry.Model;
            if (model == null) continue;

            if (string.Equals(model.Id.Entry, potionId, StringComparison.OrdinalIgnoreCase))
            {
                if (count == nth)
                    return entry;
                count++;
            }
        }

        return null;
    }
}