using System.CommandLine;
using STS2.Cli.Cmd.Services;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates simple commands with no arguments (e.g., ping, state, end_turn, embark, reward_proceed).
/// </summary>
internal static class SimpleCommand
{
    /// <summary>
    ///     Creates a simple command.
    /// </summary>
    public static Command Create(string name, string description, Option<bool> prettyOption)
    {
        var command = new Command(name, description);
        command.SetHandler(async context =>
        {
            var pretty = context.ParseResult.GetValueForOption(prettyOption);
            context.ExitCode = await CommandRunner.ExecuteAsync(name, pretty: pretty);
        });
        return command;
    }
}