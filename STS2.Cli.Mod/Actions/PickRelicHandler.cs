using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Actions.Utils;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>pick_relic</c> CLI command.
///     Picks a relic in the treasure room by index.
///     Calls <see cref="MegaCrit.Sts2.Core.Multiplayer.Game.TreasureRoomRelicSynchronizer.PickRelicLocally" />
///     with the resolved relic index, then polls for the proceed button to become enabled.
/// </summary>
/// <remarks>
///     <para><b>CLI command:</b> <c>sts2 pick_relic &lt;index&gt;</c></para>
///     <para><b>Scene:</b> Treasure room, after the chest has been opened and relics are displayed.</para>
/// </remarks>
public static class PickRelicHandler
{
    private static readonly ModLogger Logger = new("PickRelicHandler");

    /// <summary>
    ///     Handles the pick_relic request.
    ///     Accepts an index argument (args[0]) for the relic to pick.
    /// </summary>
    public static async Task<object> HandleRequestAsync(Request request)
    {
        if (request.Args == null || request.Args.Length < 1)
            return new { ok = false, error = "MISSING_ARGUMENT", message = "Relic index required (0-based)" };

        var relicIndex = request.Args[0];
        Logger.Info($"Requested to pick relic at index {relicIndex}");

        return await ExecuteAsync(relicIndex);
    }

    /// <summary>
    ///     Executes the pick_relic command.
    ///     Must be called on the Godot main thread.
    /// </summary>
    /// <param name="relicIndex">0-based index of the relic to pick.</param>
    private static async Task<object> ExecuteAsync(int relicIndex)
    {
        try
        {
            // --- Guard: Check treasure room ---
            var treasureRoom = NRun.Instance?.TreasureRoom;
            if (treasureRoom == null || !treasureRoom.IsInsideTree())
                return new { ok = false, error = "NOT_IN_TREASURE_ROOM", message = "Not currently in a treasure room" };

            // --- Guard: Check relics available ---
            var synchronizer = RunManager.Instance.TreasureRoomRelicSynchronizer;
            var currentRelics = synchronizer.CurrentRelics;
            if (currentRelics == null || currentRelics.Count == 0)
                return new { ok = false, error = "NO_RELICS_AVAILABLE", message = "No relics available to pick (chest not opened or already picked)" };

            // --- Guard: Check relic index ---
            if (relicIndex < 0 || relicIndex >= currentRelics.Count)
                return new
                {
                    ok = false,
                    error = "INVALID_RELIC_INDEX",
                    message = $"Relic index {relicIndex} is out of range (0-{currentRelics.Count - 1})"
                };

            // --- Pick the relic ---
            var relicModel = currentRelics[relicIndex];
            var relicId = relicModel.Id.Entry;
            Logger.Info($"Picking relic at index {relicIndex}: {relicId}");

            synchronizer.PickRelicLocally(relicIndex);

            // --- Poll for proceed button to become enabled ---
            // After picking, the game animates the relic pickup, then enables the proceed button.
            await ActionUtils.PollUntilAsync(() =>
            {
                if (treasureRoom.ProceedButton is { IsEnabled: true })
                    return true;

                return false;
            }, ActionUtils.UiTimeoutMs);

            // --- Return result ---
            var screen = StateHandler.DetectCurrentScreen();
            Logger.Info($"After picking relic, detected screen: {screen}");

            return new
            {
                ok = true,
                data = new
                {
                    action = "PICK_RELIC",
                    relic_id = relicId,
                    relic_index = relicIndex,
                    screen
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to pick relic: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }
}
