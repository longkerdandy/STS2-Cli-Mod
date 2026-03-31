using System.Reflection;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the <c>choose_map_node</c> CLI command.
///     Travels to a selected map node by column and row coordinates.
///     Validates that the map screen is open, the target node exists, and is travelable,
///     then calls <see cref="NMapScreen.TravelToMapCoord" /> for full UI travel with animations.
/// </summary>
/// <remarks>
///     <para><b>CLI command:</b> <c>sts2 choose_map_node &lt;col&gt; &lt;row&gt;</c></para>
///     <para><b>Scene:</b> Map screen, when the player needs to choose the next node to travel to.</para>
/// </remarks>
public static class ChooseMapNodeHandler
{
    private static readonly ModLogger Logger = new("ChooseMapNodeHandler");

    /// <summary>
    ///     Reflection accessor for the private <c>_mapPointDictionary</c> field on <see cref="NMapScreen" />.
    /// </summary>
    private static readonly FieldInfo? MapPointDictionaryField =
        typeof(NMapScreen).GetField("_mapPointDictionary", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>
    ///     Handles the choose_map_node request.
    ///     Expects <c>Args[0]</c> = col, <c>Args[1]</c> = row.
    /// </summary>
    public static async Task<object> HandleRequestAsync(Request request)
    {
        // --- Parse arguments ---
        if (request.Args == null || request.Args.Length < 2)
        {
            return new
            {
                ok = false,
                error = "MISSING_ARGUMENT",
                message = "choose_map_node requires col and row arguments (args[0]=col, args[1]=row)"
            };
        }

        var col = request.Args[0];
        var row = request.Args[1];
        Logger.Info($"Requested to choose map node at ({col}, {row})");

        return await ExecuteAsync(col, row);
    }

    /// <summary>
    ///     Executes the map node selection.
    ///     Must be called on the Godot main thread (via <see cref="MainThreadExecutor" />).
    /// </summary>
    private static async Task<object> ExecuteAsync(int col, int row)
    {
        try
        {
            // --- Validate map screen is open ---
            var mapScreen = NMapScreen.Instance;
            if (mapScreen is not { IsOpen: true })
            {
                return new
                {
                    ok = false,
                    error = "NOT_ON_MAP",
                    message = "Map screen is not open"
                };
            }

            // --- Validate run state ---
            var runState = RunManager.Instance.DebugOnlyGetState();
            if (runState == null)
            {
                return new
                {
                    ok = false,
                    error = "NO_RUN_STATE",
                    message = "No active run"
                };
            }

            // --- Validate target node exists ---
            var targetCoord = new MapCoord(col, row);
            var map = runState.Map;
            var targetPoint = map.GetPoint(targetCoord);
            if (targetPoint == null)
            {
                return new
                {
                    ok = false,
                    error = "NODE_NOT_FOUND",
                    message = $"No map node at ({col}, {row})"
                };
            }

            // --- Validate node is travelable via UI state ---
            var nodeState = GetMapPointState(mapScreen, targetCoord);
            if (nodeState != MapPointState.Travelable)
            {
                var stateStr = nodeState?.ToString() ?? "UNKNOWN";
                return new
                {
                    ok = false,
                    error = "NOT_TRAVELABLE",
                    message = $"Node at ({col}, {row}) is not travelable (state: {stateStr})"
                };
            }

            // --- Check travel is enabled ---
            if (!mapScreen.IsTravelEnabled)
            {
                return new
                {
                    ok = false,
                    error = "TRAVEL_DISABLED",
                    message = "Map travel is currently disabled (already traveling or animation in progress)"
                };
            }

            // --- Execute travel ---
            Logger.Info($"Traveling to map node at ({col}, {row}), type={targetPoint.PointType}");
            await mapScreen.TravelToMapCoord(targetCoord);

            // After TravelToMapCoord completes, the room has been entered and fade-in started.
            // Wait a bit for the screen to fully transition.
            await ActionUtils.PollUntilAsync(
                () => NMapScreen.Instance is not { IsOpen: true },
                ActionUtils.UiTimeoutMs);

            Logger.Info($"Travel to ({col}, {row}) completed");

            return new
            {
                ok = true,
                data = new
                {
                    col,
                    row,
                    type = targetPoint.PointType.ToString().ToUpperInvariant(),
                    action = "TRAVEL"
                }
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to choose map node: {ex.Message}");
            return new { ok = false, error = "INTERNAL_ERROR", message = ex.Message };
        }
    }

    /// <summary>
    ///     Gets the <see cref="MapPointState" /> for a specific coordinate from the map screen's
    ///     private <c>_mapPointDictionary</c> via reflection.
    /// </summary>
    private static MapPointState? GetMapPointState(NMapScreen mapScreen, MapCoord coord)
    {
        try
        {
            if (MapPointDictionaryField == null)
            {
                Logger.Warning("_mapPointDictionary field not found on NMapScreen");
                return null;
            }

            var dict = MapPointDictionaryField.GetValue(mapScreen);
            if (dict is not Dictionary<MapCoord, NMapPoint> mapPointDict)
            {
                Logger.Warning("_mapPointDictionary is not Dictionary<MapCoord, NMapPoint>");
                return null;
            }

            if (mapPointDict.TryGetValue(coord, out var nMapPoint))
                return nMapPoint.State;

            return null;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to get map point state: {ex.Message}");
            return null;
        }
    }
}
