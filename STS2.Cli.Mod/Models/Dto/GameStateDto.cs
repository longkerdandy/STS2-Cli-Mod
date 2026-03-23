using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.Dto;

/// <summary>
///     Root game state DTO containing overall game information.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
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

    /// <summary>
    ///     Reward state if on reward screen, null otherwise.
    /// </summary>
    public RewardStateDto? Rewards { get; set; }

    /// <summary>
    ///     Event state if at an event room, null otherwise.
    /// </summary>
    public EventStateDto? Event { get; set; }

    /// <summary>
    ///     Potion card selection state if a potion has opened a selection screen, null otherwise.
    /// </summary>
    public PotionSelectionStateDto? PotionSelection { get; set; }

    /// <summary>
    ///     Character selection state if on character select screen, null otherwise.
    /// </summary>
    public CharacterSelectStateDto? CharacterSelect { get; set; }
}
