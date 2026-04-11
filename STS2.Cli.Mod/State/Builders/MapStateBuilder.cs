using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using STS2.Cli.Mod.Models.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds <see cref="MapStateDto" /> from the current <see cref="NMapScreen" />
///     and <see cref="RunState" /> map data.
/// </summary>
public static class MapStateBuilder
{
    private static readonly ModLogger Logger = new("MapStateBuilder");

    /// <summary>
    ///     Builds the map state DTO from the current game state.
    ///     Only includes potentially reachable nodes: TRAVELED nodes (path history),
    ///     TRAVELABLE nodes (immediate next step), and all forward-reachable nodes
    ///     via BFS along Children edges from TRAVELABLE nodes.
    ///     Returns null if the map screen is not open or run state is unavailable.
    /// </summary>
    public static MapStateDto? Build()
    {
        try
        {
            var runState = RunManager.Instance.DebugOnlyGetState();
            if (runState == null)
            {
                Logger.Warning("RunState is null");
                return null;
            }

            var map = runState.Map;
            var mapScreen = NMapScreen.Instance;
            if (mapScreen is not { IsOpen: true })
            {
                Logger.Warning("NMapScreen is not open");
                return null;
            }

            // Get the NMapPoint lookup dictionary via reflection for travelability state
            var pointStateMap = GetMapPointStateDictionary(mapScreen);

            var result = new MapStateDto
            {
                ActIndex = runState.CurrentActIndex,
                ActFloor = runState.ActFloor,
                TotalFloor = runState.TotalFloor,
                Columns = map.GetColumnCount(),
                Rows = map.GetRowCount()
            };

            // Current position
            var currentCoord = runState.CurrentMapCoord;
            if (currentCoord.HasValue)
                result.CurrentCoord = new MapCoordDto
                {
                    Col = currentCoord.Value.col,
                    Row = currentCoord.Value.row
                };

            // Collect all map points into a coord-indexed lookup for BFS
            var allPoints = new Dictionary<MapCoord, MapPoint>();
            foreach (var point in map.GetAllMapPoints())
                allPoints[point.coord] = point;
            allPoints[map.StartingMapPoint.coord] = map.StartingMapPoint;
            allPoints[map.BossMapPoint.coord] = map.BossMapPoint;
            if (map.SecondBossMapPoint != null)
                allPoints[map.SecondBossMapPoint.coord] = map.SecondBossMapPoint;

            // Partition nodes by state: TRAVELED and TRAVELABLE
            var reachableCoords = new HashSet<MapCoord>();
            var travelableCoords = new List<MapCoordDto>();
            var bfsQueue = new Queue<MapPoint>();

            foreach (var (coord, point) in allPoints)
            {
                var state = GetNodeState(coord, pointStateMap);
                if (state == "TRAVELED")
                {
                    reachableCoords.Add(coord);
                }
                else if (state == "TRAVELABLE")
                {
                    reachableCoords.Add(coord);
                    travelableCoords.Add(new MapCoordDto { Col = coord.col, Row = coord.row });
                    bfsQueue.Enqueue(point);
                }
            }

            // Forward BFS from TRAVELABLE nodes along Children edges
            while (bfsQueue.Count > 0)
            {
                var current = bfsQueue.Dequeue();
                foreach (var child in current.Children)
                {
                    if (reachableCoords.Add(child.coord))
                        bfsQueue.Enqueue(child);
                }
            }

            // Build DTOs only for reachable nodes
            var nodes = new List<MapNodeDto>(reachableCoords.Count);
            foreach (var coord in reachableCoords)
            {
                if (allPoints.TryGetValue(coord, out var point))
                    nodes.Add(BuildNode(point, pointStateMap));
            }

            // Sort nodes by row then col for deterministic output
            nodes.Sort((a, b) =>
            {
                var rowCmp = a.Row.CompareTo(b.Row);
                return rowCmp != 0 ? rowCmp : a.Col.CompareTo(b.Col);
            });

            result.Nodes = nodes;
            result.TravelableCoords = travelableCoords;

            Logger.Info($"Built map state: act={result.ActIndex}, floor={result.ActFloor}, " +
                        $"nodes={nodes.Count}, travelable={travelableCoords.Count}");
            return result;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to build map state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Builds a <see cref="MapNodeDto" /> from a <see cref="MapPoint" /> data model,
    ///     looking up travelability state from the UI node dictionary.
    /// </summary>
    private static MapNodeDto BuildNode(MapPoint point, Dictionary<MapCoord, MapPointState>? pointStateMap)
    {
        var node = new MapNodeDto
        {
            Col = point.coord.col,
            Row = point.coord.row,
            Type = FormatPointType(point.PointType),
            State = GetNodeState(point.coord, pointStateMap)
        };

        // Build children edges
        foreach (var child in point.Children)
            node.Children.Add(new MapCoordDto { Col = child.coord.col, Row = child.coord.row });

        return node;
    }

    /// <summary>
    ///     Gets the travelability state string for a map coordinate.
    ///     Falls back to "UNKNOWN" if the UI node lookup is unavailable.
    /// </summary>
    private static string GetNodeState(MapCoord coord, Dictionary<MapCoord, MapPointState>? pointStateMap)
    {
        if (pointStateMap == null)
            return "UNKNOWN";

        if (!pointStateMap.TryGetValue(coord, out var state))
            return "UNKNOWN";

        return state switch
        {
            MapPointState.Travelable => "TRAVELABLE",
            MapPointState.Traveled => "TRAVELED",
            MapPointState.Untravelable => "UNTRAVELABLE",
            MapPointState.None => "NONE",
            _ => "UNKNOWN"
        };
    }

    /// <summary>
    ///     Formats a <see cref="MapPointType" /> to a consistent SCREAMING_SNAKE_CASE string.
    /// </summary>
    private static string FormatPointType(MapPointType type)
    {
        return type switch
        {
            MapPointType.Monster => "MONSTER",
            MapPointType.Elite => "ELITE",
            MapPointType.Boss => "BOSS",
            MapPointType.Shop => "SHOP",
            MapPointType.RestSite => "REST_SITE",
            MapPointType.Treasure => "TREASURE",
            MapPointType.Unknown => "UNKNOWN",
            MapPointType.Ancient => "ANCIENT",
            MapPointType.Unassigned => "UNASSIGNED",
            _ => type.ToString().ToUpperInvariant()
        };
    }

    /// <summary>
    ///     Extracts the map point state dictionary from <see cref="NMapScreen" /> via reflection.
    ///     Returns a coord-to-state mapping, or null if reflection fails.
    /// </summary>
    /// <remarks>
    ///     The private <c>_mapPointDictionary</c> maps <see cref="MapCoord" /> to <see cref="NMapPoint" />.
    ///     We read each <see cref="NMapPoint.State" /> to get the travelability computed by the game.
    /// </remarks>
    private static Dictionary<MapCoord, MapPointState>? GetMapPointStateDictionary(NMapScreen mapScreen)
    {
        var dict = UiUtils.GetPrivateField<Dictionary<MapCoord, NMapPoint>>(mapScreen, "_mapPointDictionary");
        if (dict == null)
        {
            Logger.Warning("_mapPointDictionary field not found or not Dictionary<MapCoord, NMapPoint>");
            return null;
        }

        var result = new Dictionary<MapCoord, MapPointState>(dict.Count);
        foreach (var kvp in dict) result[kvp.Key] = kvp.Value.State;

        return result;
    }
}