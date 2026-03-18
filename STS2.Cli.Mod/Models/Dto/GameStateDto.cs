using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.Dto;

/// <summary>
///     Root game state DTO containing overall game information.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public class GameStateDto
{
    /// <summary>
    ///     Current screen/phase (COMBAT, MENU, MAP, SHOP, etc.)
    /// </summary>
    public string Screen { get; set; } = "UNKNOWN";

    /// <summary>
    ///     Unix timestamp of state extraction.
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    ///     Error message if state extraction failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    ///     Combat state if in combat, null otherwise.
    /// </summary>
    public CombatStateDto? Combat { get; set; }
}
