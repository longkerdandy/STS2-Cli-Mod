using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Shop (merchant room) state DTO containing all available items, prices, and player gold.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class ShopStateDto
{
    /// <summary>
    ///     Cards available for purchase (character + colorless combined).
    /// </summary>
    public List<ShopCardDto> Cards { get; set; } = [];

    /// <summary>
    ///     Relics available for purchase.
    /// </summary>
    public List<ShopRelicDto> Relics { get; set; } = [];

    /// <summary>
    ///     Potions available for purchase.
    /// </summary>
    public List<ShopPotionDto> Potions { get; set; } = [];

    /// <summary>
    ///     Card removal service info, or null if not available in this shop.
    /// </summary>
    public ShopCardRemovalDto? CardRemoval { get; set; }

    /// <summary>
    ///     The player's current gold.
    /// </summary>
    public int PlayerGold { get; set; }

    /// <summary>
    ///     Whether the proceed button is enabled (player can leave the shop).
    /// </summary>
    public bool CanProceed { get; set; }
}

/// <summary>
///     A card for sale in the shop.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class ShopCardDto
{
    /// <summary>
    ///     0-based index in the combined card entries list.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Card model ID (e.g., "STRIKE_IRONCLAD").
    /// </summary>
    public required string CardId { get; set; }

    /// <summary>
    ///     Localized display name.
    /// </summary>
    public required string CardName { get; set; }

    /// <summary>
    ///     Card effect description with dynamic values resolved.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    ///     Card type: Attack, Skill, Power, Status, Curse.
    /// </summary>
    public required string CardType { get; set; }

    /// <summary>
    ///     Rarity: Common, Uncommon, Rare.
    /// </summary>
    public required string Rarity { get; set; }

    /// <summary>
    ///     Gold cost after price modifiers.
    /// </summary>
    public int Cost { get; set; }

    /// <summary>
    ///     Whether this card is on sale (half price).
    /// </summary>
    public bool IsOnSale { get; set; }

    /// <summary>
    ///     Whether this card is still in stock (not yet purchased).
    /// </summary>
    public bool IsStocked { get; set; }
}

/// <summary>
///     A relic for sale in the shop.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class ShopRelicDto
{
    /// <summary>
    ///     0-based index in the relic entries list.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Relic model ID (e.g., "VAJRA").
    /// </summary>
    public required string RelicId { get; set; }

    /// <summary>
    ///     Localized display name.
    /// </summary>
    public required string RelicName { get; set; }

    /// <summary>
    ///     Effect description with dynamic values resolved.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    ///     Rarity tier: Common, Uncommon, Rare, Shop.
    /// </summary>
    public required string Rarity { get; set; }

    /// <summary>
    ///     Gold cost after price modifiers.
    /// </summary>
    public int Cost { get; set; }

    /// <summary>
    ///     Whether this relic is still in stock (not yet purchased).
    /// </summary>
    public bool IsStocked { get; set; }
}

/// <summary>
///     A potion for sale in the shop.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class ShopPotionDto
{
    /// <summary>
    ///     0-based index in the potion entries list.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    ///     Potion model ID (e.g., "FIRE_POTION").
    /// </summary>
    public required string PotionId { get; set; }

    /// <summary>
    ///     Localized display name.
    /// </summary>
    public required string PotionName { get; set; }

    /// <summary>
    ///     Effect description with dynamic values resolved.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    ///     Rarity: Common, Uncommon, Rare.
    /// </summary>
    public required string Rarity { get; set; }

    /// <summary>
    ///     Gold cost after price modifiers.
    /// </summary>
    public int Cost { get; set; }

    /// <summary>
    ///     Whether this potion is still in stock (not yet purchased).
    /// </summary>
    public bool IsStocked { get; set; }
}

/// <summary>
///     Card removal service info in the shop.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class ShopCardRemovalDto
{
    /// <summary>
    ///     Gold cost for card removal.
    /// </summary>
    public int Cost { get; set; }

    /// <summary>
    ///     Whether the card removal service has already been used this visit.
    /// </summary>
    public bool IsUsed { get; set; }
}
