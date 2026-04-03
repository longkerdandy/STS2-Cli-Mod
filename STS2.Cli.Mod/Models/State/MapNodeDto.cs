using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Individual map node DTO representing a single point on the act map.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "CollectionNeverQueried.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class MapNodeDto
{
    /// <summary>
    ///     Column index of this node.
    /// </summary>
    public int Col { get; set; }

    /// <summary>
    ///     Row index of this node.
    /// </summary>
    public int Row { get; set; }

    /// <summary>
    ///     Node type: MONSTER, ELITE, BOSS, SHOP, REST_SITE, TREASURE, UNKNOWN, ANCIENT, UNASSIGNED.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///     Node travelability state: TRAVELABLE, TRAVELED, UNTRAVELABLE.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    ///     Coordinates of child nodes (outgoing edges in the map DAG).
    /// </summary>
    public List<MapCoordDto> Children { get; set; } = [];

    /// <summary>
    ///     Coordinates of parent nodes (incoming edges in the map DAG).
    /// </summary>
    public List<MapCoordDto> Parents { get; set; } = [];
}
