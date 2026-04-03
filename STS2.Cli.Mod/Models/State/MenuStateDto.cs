using System.Diagnostics.CodeAnalysis;

namespace STS2.Cli.Mod.Models.State;

/// <summary>
///     Main menu state DTO.
///     Indicates whether a saved run exists, which determines available actions
///     (continue_run / abandon_run vs new_run).
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class MenuStateDto
{
    /// <summary>
    ///     Whether a saved run exists (current_run.save).
    ///     When true: continue_run and abandon_run are available.
    ///     When false: new_run is available.
    /// </summary>
    public bool HasRunSave { get; set; }
}
