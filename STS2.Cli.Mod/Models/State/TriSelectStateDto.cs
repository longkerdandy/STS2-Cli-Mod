using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Tri-select (choose-a-card) screen state DTO.
///     Contains information about cards available for selection when a
///     <c>NChooseACardSelectionScreen</c> is open. This screen displays up to 3 generated
///     cards and is triggered by potions, cards (Discovery, Quasar, Splash),
///     relics (Toolbox, MassiveScroll, etc.), and monsters (KnowledgeDemon).
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class TriSelectStateDto
{
    /// <summary>
    ///     Type of selection: choose_from_pool_attack, choose_from_pool_skill, choose_from_pool_power,
    ///     choose_from_pool, etc.
    /// </summary>
    public string SelectionType { get; set; } = "unknown";

    /// <summary>
    ///     Minimum number of cards that must be selected (0 if skippable, 1 if forced).
    /// </summary>
    public int MinSelect { get; set; }

    /// <summary>
    ///     Maximum number of cards that can be selected (always 1 for this screen type).
    /// </summary>
    public int MaxSelect { get; set; }

    /// <summary>
    ///     Whether the player can skip this selection (select 0 cards).
    /// </summary>
    public bool CanSkip { get; set; }

    /// <summary>
    ///     List of cards available for selection (up to 3).
    /// </summary>
    public List<SelectableCardDto> Cards { get; set; } = [];
}
