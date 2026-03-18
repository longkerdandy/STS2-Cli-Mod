using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.Dto;

/// <summary>
///     Intent state DTO representing an enemy's upcoming action.
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class IntentStateDto
{
    /// <summary>
    ///     Intent type (Attack, Defend, Buff, Debuff, etc.).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    ///     Human-readable description of the intent.
    /// </summary>
    public string? Description { get; set; }
}
