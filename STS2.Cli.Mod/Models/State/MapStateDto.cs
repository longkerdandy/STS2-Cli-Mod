using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Map screen state DTO containing the map grid, current position, and travelable nodes.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class MapStateDto
{
    /// <summary>
    ///     Current act number (0-indexed).
    /// </summary>
    public int ActIndex { get; set; }

    /// <summary>
    ///     Current floor within the act (1-indexed).
    /// </summary>
    public int ActFloor { get; set; }

    /// <summary>
    ///     Total floors visited across all acts.
    /// </summary>
    public int TotalFloor { get; set; }

    /// <summary>
    ///     Number of columns in the map grid.
    /// </summary>
    public int Columns { get; set; }

    /// <summary>
    ///     Number of rows in the map grid (excludes boss/starting rows).
    /// </summary>
    public int Rows { get; set; }

    /// <summary>
    ///     Current map position (last visited coordinate), null if at the start before any travel.
    /// </summary>
    public MapCoordDto? CurrentCoord { get; set; }

    /// <summary>
    ///     All map nodes including grid nodes, starting point, boss, and optional second boss.
    /// </summary>
    public List<MapNodeDto> Nodes { get; set; } = [];

    /// <summary>
    ///     Coordinates of nodes the player can currently travel to.
    ///     Convenience field derived from nodes with state "TRAVELABLE".
    /// </summary>
    public List<MapCoordDto> TravelableCoords { get; set; } = [];
}

/// <summary>
///     Coordinate pair identifying a map node position.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class MapCoordDto
{
    /// <summary>
    ///     Column index (0-based).
    /// </summary>
    public int Col { get; set; }

    /// <summary>
    ///     Row index. Grid rows are 1..N, row 0 is the starting point,
    ///     row N is the boss, row N+1 is the optional second boss.
    /// </summary>
    public int Row { get; set; }
}
