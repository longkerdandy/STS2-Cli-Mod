using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using STS2.Cli.Mod.Actions.Utils;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>shop_remove_card</c> CLI command.
///     Buys the card removal service from the shop.
///     Uses the fire-and-forget pattern (like SMITH in rest sites) because
///     <see cref="MegaCrit.Sts2.Core.Entities.Merchant.MerchantCardRemovalEntry.OnTryPurchaseWrapper(MegaCrit.Sts2.Core.Entities.Merchant.MerchantInventory?, bool, bool)" />
///     calls <c>DoLocalMerchantCardRemoval</c> which opens a card selection screen
///     and blocks until the player picks a card.
///     After the fire-and-forget launch, polls for the GRID_CARD_SELECT overlay
///     to appear, then returns so the CLI can issue <c>grid_select_card</c>.
/// </summary>
/// <remarks>
///     <para><b>CLI command:</b> <c>sts2 shop_remove_card</c></para>
///     <para><b>Scene:</b> Merchant room (shop), when the card removal service is available.</para>
/// </remarks>
public static class ShopRemoveCardHandler
{
    private static readonly ModLogger Logger = new("ShopRemoveCardHandler");

    /// <summary>
    ///     Handles the shop_remove_card request.
    /// </summary>
    public static async Task<object> HandleRequestAsync(Request request)
    {
        Logger.Info("Requested to buy card removal service");
        return await ExecuteAsync();
    }

    /// <summary>
    ///     Executes the shop_remove_card command.
    ///     Must be called on the Godot main thread.
    /// </summary>
    private static async Task<object> ExecuteAsync()
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

            // --- Guard: Check card removal entry exists ---
            var entry = inventory.CardRemovalEntry;
            if (entry == null)
                return new { ok = false, error = "NOT_IN_SHOP", message = "Card removal service not available in this shop" };

            // --- Guard: Check not already used ---
            if (entry.Used)
                return new { ok = false, error = "CARD_REMOVAL_USED", message = "Card removal service has already been used" };

            // --- Guard: Check enough gold ---
            if (!entry.EnoughGold)
                return new { ok = false, error = "NOT_ENOUGH_GOLD", message = $"Not enough gold for card removal (cost={entry.Cost})" };

            // --- Fire-and-forget: launch the card removal purchase ---
            // OnTryPurchaseWrapper calls DoLocalMerchantCardRemoval which opens a
            // card selection screen (GRID_CARD_SELECT) and blocks until the player
            // picks a card or cancels.
            // Important: do NOT use Task.Run — must stay on Godot main thread.
            _ = ExecuteRemovalFireAndForgetAsync(entry, inventory);

            // --- Poll for GRID_CARD_SELECT overlay to appear ---
            await ActionUtils.PollUntilAsync(() =>
            {
                var overlay = NOverlayStack.Instance?.Peek();
                if (overlay is MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardGridSelectionScreen)
                    return true;

                return false;
            }, ActionUtils.UiTimeoutMs);

            // --- Detect resulting screen ---
            var screen = StateHandler.DetectScreen();
            Logger.Info($"After requesting card removal, detected screen: {screen}");

            return new
            {
                ok = true,
                data = new
                {
                    action = "SHOP_REMOVE_CARD",
                    cost = entry.Cost,
                    screen
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to buy card removal: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Fire-and-forget helper that awaits the card removal purchase.
    ///     The purchase blocks until the player selects a card to remove or cancels.
    ///     Must run on the Godot main thread (caller uses discard <c>_</c>, not <c>Task.Run</c>).
    /// </summary>
    private static async Task ExecuteRemovalFireAndForgetAsync(
        MegaCrit.Sts2.Core.Entities.Merchant.MerchantCardRemovalEntry entry,
        MegaCrit.Sts2.Core.Entities.Merchant.MerchantInventory inventory)
    {
        try
        {
            // cancelable: true allows the player to back out of card selection
            var success = await entry.OnTryPurchaseWrapper(inventory, ignoreCost: false, cancelable: true);
            if (success)
                Logger.Info("Card removal purchase completed successfully");
            else
                Logger.Info("Card removal purchase was cancelled or failed");
        }
        catch (Exception ex)
        {
            Logger.Error($"Fire-and-forget card removal task failed: {ex.Message}");
        }
    }
}
