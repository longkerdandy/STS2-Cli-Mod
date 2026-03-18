using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.Dto;

/// <summary>
///     Potion state DTO representing a potion in the player's inventory.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class PotionStateDto
{
    /// <summary>
    ///     Potion slot index (0-2).
    /// </summary>
    public int Slot { get; set; }

    /// <summary>
    ///     Potion ID (e.g., "FirePotion").
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    ///     Display name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    ///     Effect description with dynamic values resolved.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     Whether potion can be used in current context.
    /// </summary>
    public bool CanUseInCombat { get; set; }

    /// <summary>
    ///     Target type: Self, AnyEnemy, etc.
    /// </summary>
    public string? TargetType { get; set; }
}
