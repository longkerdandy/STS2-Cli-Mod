using System.Reflection;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles opening the treasure chest in a treasure room.
///     Mirrors the game's <c>OnChestButtonReleased</c> flow: disables the chest button,
///     then calls <c>OpenChest()</c> via <c>TaskHelper.RunSafely</c> (fire-and-forget).
///     Because <c>OpenChest()</c> is async and blocks waiting for relic picking,
///     this handler uses fire-and-forget, then polls for the relics to appear
///     before returning the updated state.
/// </summary>
public static class OpenChestHandler
{
    private static readonly ModLogger Logger = new("OpenChestHandler");

    private static readonly FieldInfo? ChestButtonField =
        typeof(NTreasureRoom).GetField("_chestButton",
            BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? HasChestBeenOpenedField =
        typeof(NTreasureRoom).GetField("_hasChestBeenOpened",
            BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo? OpenChestMethod =
        typeof(NTreasureRoom).GetMethod("OpenChest",
            BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    ///     Handles the open_chest request.
    /// </summary>
    public static async Task<object> HandleRequestAsync(Request request)
    {
        Logger.Info("Requested to open treasure chest");
        return await ExecuteAsync();
    }

    /// <summary>
    ///     Executes the open_chest command.
    ///     Must be called on the Godot main thread.
    /// </summary>
    private static async Task<object> ExecuteAsync()
    {
        try
        {
            // --- Guard: Check treasure room ---
            var treasureRoom = NRun.Instance?.TreasureRoom;
            if (treasureRoom == null || !treasureRoom.IsInsideTree())
                return new { ok = false, error = "NOT_IN_TREASURE_ROOM", message = "Not currently in a treasure room" };

            // --- Guard: Check chest not already opened ---
            if (GetHasChestBeenOpened(treasureRoom))
                return new { ok = false, error = "CHEST_ALREADY_OPENED", message = "Chest has already been opened" };

            // --- Get chest button ---
            var chestButton = ChestButtonField?.GetValue(treasureRoom) as NButton;
            if (chestButton == null)
                return new { ok = false, error = "UI_NOT_FOUND", message = "Chest button not found" };

            // --- Fire-and-forget: simulate OnChestButtonReleased ---
            // The game calls TaskHelper.RunSafely(OpenChest()) then disables the chest button.
            // OpenChest is async and blocks on relic picking, so we fire-and-forget.
            // Important: do NOT use Task.Run — must stay on Godot main thread.
            if (OpenChestMethod != null)
            {
                var task = (Task?)OpenChestMethod.Invoke(treasureRoom, null);
                if (task != null)
                    _ = TaskHelper.RunSafely(task);
            }

            chestButton.Disable();

            // --- Poll for relics to appear or proceed to be enabled ---
            await ActionUtils.PollUntilAsync(() =>
            {
                // Relics appeared in the synchronizer
                var relics = RunManager.Instance.TreasureRoomRelicSynchronizer.CurrentRelics;
                if (relics is { Count: > 0 })
                    return true;

                // Proceed button enabled (e.g., empty chest scenario)
                if (treasureRoom.ProceedButton is { IsEnabled: true })
                    return true;

                return false;
            }, ActionUtils.UiTimeoutMs);

            // --- Return updated screen state ---
            var screen = StateHandler.DetectCurrentScreen();
            Logger.Info($"After opening chest, detected screen: {screen}");

            return new
            {
                ok = true,
                data = new
                {
                    action = "OPEN_CHEST",
                    screen
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to open chest: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Gets the _hasChestBeenOpened private field value via reflection.
    /// </summary>
    private static bool GetHasChestBeenOpened(NTreasureRoom treasureRoom)
    {
        try
        {
            if (HasChestBeenOpenedField != null)
                return (bool)(HasChestBeenOpenedField.GetValue(treasureRoom) ?? false);
            return false;
        }
        catch
        {
            return false;
        }
    }
}
