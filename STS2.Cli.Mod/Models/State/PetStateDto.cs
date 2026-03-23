using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.State;

/// <summary>
///     Pet creature state DTO (e.g., Necrobinder's Osty, Byrdpip).
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class PetStateDto
{
    /// <summary>
    ///     Monster model ID (e.g., "OSTY", "BYRDPIP").
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     Localized display name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///     Whether the pet is currently alive.
    /// </summary>
    public bool IsAlive { get; set; }

    /// <summary>
    ///     Current HP.
    /// </summary>
    public int Hp { get; set; }

    /// <summary>
    ///     Maximum HP.
    /// </summary>
    public int MaxHp { get; set; }

    /// <summary>
    ///     Current block.
    /// </summary>
    public int Block { get; set; }

    /// <summary>
    ///     Active powers on the pet.
    /// </summary>
    public List<PowerStateDto> Powers { get; set; } = [];
}
