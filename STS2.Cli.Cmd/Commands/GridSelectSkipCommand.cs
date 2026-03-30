using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the grid_select_skip command for cancelling a grid card selection.
/// </summary>
internal static class GridSelectSkipCommand
{
    /// <summary>
    ///     Creates the grid_select_skip command for cancelling a grid card selection.
    /// </summary>
    public static Command Create()
    {
        var command = new Command("grid_select_skip",
            "Cancel/skip a grid card selection (if allowed)");

        command.SetAction(parseResult =>
        {
            var pretty = CommandExecutor.IsPretty(parseResult);

            return CommandExecutor.ExecuteAsync(
                () => new Request { Cmd = "grid_select_skip" },
                pretty,
                10000);
        });

        return command;
    }
}
