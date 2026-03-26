using System.CommandLine;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the open_chest command for opening a treasure room chest.
/// </summary>
internal static class OpenChestCommand
{
    /// <summary>
    ///     Creates the open_chest command.
    /// </summary>
    public static Command Create()
    {
        return SimpleCommand.Create("open_chest", "Open the treasure chest in a treasure room");
    }
}
