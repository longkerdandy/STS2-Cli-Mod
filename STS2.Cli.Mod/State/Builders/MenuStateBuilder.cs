using MegaCrit.Sts2.Core.Saves;
using STS2.Cli.Mod.Models.State;

namespace STS2.Cli.Mod.State.Builders;

/// <summary>
///     Builds <see cref="MenuStateDto" /> from the current main menu state.
/// </summary>
public static class MenuStateBuilder
{
    /// <summary>
    ///     Builds the main menu state indicating whether a saved run exists.
    /// </summary>
    public static MenuStateDto Build()
    {
        return new MenuStateDto
        {
            HasRunSave = SaveManager.Instance.HasRunSave
        };
    }
}