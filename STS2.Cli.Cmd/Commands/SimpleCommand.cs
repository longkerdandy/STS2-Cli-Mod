using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates simple commands with no arguments (e.g., ping, state, end_turn, embark, reward_proceed).
/// </summary>
internal static class SimpleCommand
{
    /// <summary>
    ///     Creates a simple command.
    /// </summary>
    public static Command Create(string name, string description)
    {
        var command = new Command(name, description);

        command.SetAction(parseResult =>
        {
            var pretty = CommandExecutor.IsPretty(parseResult);
            return CommandExecutor.ExecuteAsync(
                () => new Request { Cmd = name },
                pretty);
        });

        return command;
    }
}
