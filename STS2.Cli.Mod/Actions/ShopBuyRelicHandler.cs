using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>shop_buy_relic</c> CLI command.
///     Buys a relic from the shop by relic_id + nth.
///     Finds the matching <see cref="MerchantRelicEntry" /> in the
///     <see cref="MerchantInventory.RelicEntries" /> list and calls
///     <see cref="MerchantEntry.OnTryPurchaseWrapper" /> to purchase it.
/// </summary>
/// <remarks>
///     <para><b>CLI command:</b> <c>sts2 shop_buy_relic &lt;relic_id&gt; [--nth &lt;n&gt;]</c></para>
///     <para><b>Scene:</b> Merchant room (shop).</para>
/// </remarks>
public static class ShopBuyRelicHandler
{
    private static readonly ModLogger Logger = new("ShopBuyRelicHandler");

    /// <summary>
    ///     Handles the shop_buy_relic request.
    /// </summary>
    public static async Task<object> HandleRequestAsync(Request request)
    {
        if (string.IsNullOrEmpty(request.Id))
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Relic ID required" };

        var relicId = request.Id;
        var nth = request.Nth ?? 0;
        Logger.Info($"Requested to buy relic: {relicId} (nth={nth})");

        return await ExecuteAsync(relicId, nth);
    }

    /// <summary>
    ///     Executes the shop_buy_relic command.
    ///     Must be called on the Godot main thread.
    /// </summary>
    private static async Task<object> ExecuteAsync(string relicId, int nth)
    {
        try
        {
            // --- Guard: Check merchant room ---
            var merchantRoom = NMerchantRoom.Instance;
            if (merchantRoom == null || !merchantRoom.IsInsideTree())
                return new { ok = false, error = "NOT_IN_SHOP", message = "Not currently in a shop" };

            var inventory = merchantRoom.Room?.Inventory;
            if (inventory == null)
                return new { ok = false, error = "NOT_IN_SHOP", message = "Shop inventory not available" };

            // --- Find the relic entry by ID + nth ---
            var entry = FindRelicEntry(inventory, relicId, nth);
            if (entry == null)
                return new { ok = false, error = "ITEM_NOT_FOUND", message = $"Relic '{relicId}' (nth={nth}) not found in shop" };

            // --- Guard: Check item is in stock ---
            if (!entry.IsStocked)
                return new { ok = false, error = "ITEM_SOLD_OUT", message = $"Relic '{relicId}' is sold out" };

            // --- Guard: Check enough gold ---
            if (!entry.EnoughGold)
                return new { ok = false, error = "NOT_ENOUGH_GOLD", message = $"Not enough gold to buy relic '{relicId}' (cost={entry.Cost})" };

            // --- Purchase ---
            var success = await entry.OnTryPurchaseWrapper(inventory);
            if (!success)
                return new { ok = false, error = "PURCHASE_FAILED", message = $"Failed to purchase relic '{relicId}'" };

            Logger.Info($"Successfully purchased relic: {relicId}");

            var screen = StateHandler.DetectCurrentScreen();
            return new
            {
                ok = true,
                data = new
                {
                    action = "SHOP_BUY_RELIC",
                    relic_id = relicId,
                    cost = entry.Cost,
                    screen
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to buy relic: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Finds a relic entry in the inventory by relic_id and nth occurrence.
    /// </summary>
    private static MerchantRelicEntry? FindRelicEntry(MerchantInventory inventory, string relicId, int nth)
    {
        int count = 0;
        for (int i = 0; i < inventory.RelicEntries.Count; i++)
        {
            var entry = inventory.RelicEntries[i];
            var model = entry.Model;
            if (model == null) continue;

            if (string.Equals(model.Id.Entry, relicId, StringComparison.OrdinalIgnoreCase))
            {
                if (count == nth)
                    return entry;
                count++;
            }
        }

        return null;
    }
}
