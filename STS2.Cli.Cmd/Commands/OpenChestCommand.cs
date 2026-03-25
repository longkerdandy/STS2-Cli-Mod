using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the open_chest command for opening a treasure room chest.
/// </summary>
internal static class OpenChestCommand
{
    /// <summary>
    ///     Creates the open_chest command.
    /// </summary>
    public static Command Create(Option<bool> prettyOption)
    {
        return SimpleCommand.Create("open_chest", "Open the treasure chest in a treasure room", prettyOption);
    }
}
