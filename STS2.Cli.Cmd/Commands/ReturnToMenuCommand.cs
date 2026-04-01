using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the return_to_menu command for returning to main menu from game over screen.
/// </summary>
internal static class ReturnToMenuCommand
{
    /// <summary>
    ///     Creates the return_to_menu command.
    /// </summary>
    public static Command Create()
    {
        var command = new Command("return_to_menu", "Return to main menu from the game over screen");

        command.SetAction(parseResult =>
        {
            var pretty = CommandExecutor.IsPretty(parseResult);

            return CommandExecutor.ExecuteAsync(
                () => new Request { Cmd = "return_to_menu" },
                pretty);
        });

        return command;
    }
}
