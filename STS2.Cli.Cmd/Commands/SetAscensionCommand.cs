using System.CommandLine;
using STS2.Cli.Cmd.Models.Message;

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
        var levelArg = new Argument<int>("level", "Ascension level (0-20)");
        command.AddArgument(levelArg);

        command.SetHandler(async context =>
        {
            var level = context.ParseResult.GetValueForArgument(levelArg);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);

            context.ExitCode = await CommandExecutor.ExecuteAsync(
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