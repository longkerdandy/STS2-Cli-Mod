using System.CommandLine;
using STS2.Cli.Cmd.Models.Message;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates commands with a single positional index argument (e.g., choose_event).
/// </summary>
internal static class IndexedCommand
{
    /// <summary>
    ///     Creates an indexed command.
    /// </summary>
    public static Command Create(
        string name, string description,
        Argument<int> indexArg,
        Option<bool> prettyOption)
    {
        var command = new Command(name, description);
        command.Arguments.Add(indexArg);
        command.Options.Add(prettyOption);

        command.SetAction(parseResult =>
        {
            var index = parseResult.GetValue(indexArg);
            var pretty = parseResult.GetValue(prettyOption);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = name,
                    Args = [index]
                },
                pretty);
        });

        return command;
    }
}
