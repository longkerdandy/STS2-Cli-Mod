namespace STS2.Cli.Mod.State.Dto;

/// <summary>
///     Buff/Power state DTO representing a status effect on a creature.
/// </summary>
public class BuffStateDto
{
    /// <summary>
    ///     Buff ID (e.g., "Strength", "Dexterity", "Vulnerable").
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    ///     Buff display name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     Buff stack amount.
    /// </summary>
    public int Amount { get; set; }

    /// <summary>
    ///     Buff type (Buff, Debuff, etc.).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    ///     Buff description text.
    /// </summary>
    public string? Description { get; set; }
}
