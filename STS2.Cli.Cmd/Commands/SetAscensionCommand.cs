using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the set_ascension command.
/// </summary>
internal static class SetAscensionCommand
{
    /// <summary>
    ///     Creates the set_ascension command.
    /// </summary>
    public static Command Create(Option<bool> prettyOption)
    {
        var command = new Command("set_ascension", "Set ascension level on the character select screen");
        var levelArg = new Argument<int>("level")
        {
            Description = "Ascension level (0-20)"
        };
        command.Arguments.Add(levelArg);
        command.Options.Add(prettyOption);

        command.SetAction(parseResult =>
        {
            var level = parseResult.GetValue(levelArg);
            var pretty = parseResult.GetValue(prettyOption);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "set_ascension",
                    Args = [level]
                },
                pretty);
        });

        return command;
    }
}