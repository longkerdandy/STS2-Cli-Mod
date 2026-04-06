using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Card choice DTO representing a single card option within a card reward.
///     Used by <see cref="RewardItemDto.CardChoices" /> to list the pickable cards.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class CardChoiceDto
{
    /// <summary>
    ///     Card index within the card reward (0-based). Used as the card_index for choose_card command.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Card model ID (e.g., "INFLAME", "SHRUG_IT_OFF").
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     Localized display name (includes "+" suffix if upgraded).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Card description with dynamic values resolved and formatting tags stripped.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    ///     Card type: Attack, Skill, Power, Status, Curse.
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    ///     Card rarity: Basic, Common, Uncommon, Rare, Ancient.
    /// </summary>
    public required string Rarity { get; set; }

    /// <summary>
    ///     Energy cost to play (-1 for X-cost cards).
    /// </summary>
    public int Cost { get; set; }

    /// <summary>
    ///     True if the card is upgraded.
    /// </summary>
    public bool IsUpgraded { get; set; }
}
