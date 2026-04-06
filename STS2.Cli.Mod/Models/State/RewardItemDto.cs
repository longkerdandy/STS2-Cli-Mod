using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Individual reward DTO representing a single reward on the reward screen.
///     Type-specific fields are null when not applicable to the reward type.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class RewardItemDto
{
    /// <summary>
    ///     Position in the reward list (0-based). Used as the index for claim_reward/choose_card commands.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Reward type: "Gold", "Potion", "Relic", "Card", "SpecialCard", "CardRemoval", "LinkedRewardSet".
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    ///     Localized description of the reward (e.g., "25 Gold", "Fire Potion", "Add a card to your deck").
    /// </summary>
    public required string Description { get; set; }

    // --- Gold-specific ---

    /// <summary>
    ///     Gold amount for Gold rewards. Null for other reward types.
    /// </summary>
    public int? GoldAmount { get; set; }

    // --- Potion-specific ---

    /// <summary>
    ///     Potion model ID (e.g., "FIRE_POTION"). Null for non-Potion rewards.
    /// </summary>
    public string? PotionId { get; set; }

    /// <summary>
    ///     Potion display name. Null for non-Potion rewards.
    /// </summary>
    public string? PotionName { get; set; }

    /// <summary>
    ///     Potion rarity tier: Common, Uncommon, Rare. Null for non-Potion rewards.
    /// </summary>
    public string? PotionRarity { get; set; }

    // --- Relic-specific ---

    /// <summary>
    ///     Relic model ID (e.g., "BURNING_BLOOD"). Null for non-Relic rewards.
    ///     Uses reflection to access the private <c>_relic</c> field since <c>ClaimedRelic</c>
    ///     is only set after claim.
    /// </summary>
    public string? RelicId { get; set; }

    /// <summary>
    ///     Relic display name. Null for non-Relic rewards.
    /// </summary>
    public string? RelicName { get; set; }

    /// <summary>
    ///     Relic effect description. Null for non-Relic rewards.
    /// </summary>
    public string? RelicDescription { get; set; }

    /// <summary>
    ///     Relic rarity tier: Common, Uncommon, Rare, Shop, Event. Null for non-Relic rewards.
    /// </summary>
    public string? RelicRarity { get; set; }

    // --- Card-specific (for CardReward: the card choices) ---

    /// <summary>
    ///     Card choices for Card rewards (typically 3 options). Null for non-Card rewards.
    /// </summary>
    public List<CardChoiceDto>? CardChoices { get; set; }

    // --- SpecialCard-specific ---

    /// <summary>
    ///     Card model ID for SpecialCard rewards. Null for non-SpecialCard rewards.
    /// </summary>
    public string? CardId { get; set; }

    /// <summary>
    ///     Card display name for SpecialCard rewards. Null for non-SpecialCard rewards.
    /// </summary>
    public string? CardName { get; set; }
}
