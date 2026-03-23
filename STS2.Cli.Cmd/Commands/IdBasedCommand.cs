using System.CommandLine;
using STS2.Cli.Cmd.Services;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates commands with ID-based arguments (e.g., play_card, use_potion).
/// </summary>
internal static class IdBasedCommand
{
    /// <summary>
    ///     Creates an ID-based command with an optional target.
    /// </summary>
    public static Command Create(
        string name, string description,
        Argument<string> idArg,
        Option<int> nthOption,
        Option<int?> targetOption,
        Option<bool> prettyOption)
    {
        var command = new Command(name, description);
        command.AddArgument(idArg);
        command.AddOption(nthOption);
        command.AddOption(targetOption);
        command.SetHandler(async context =>
        {
            var id = context.ParseResult.GetValueForArgument(idArg);
            var nth = context.ParseResult.GetValueForOption(nthOption);
            var target = context.ParseResult.GetValueForOption(targetOption);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);
            context.ExitCode = await CommandRunner.ExecuteAsync(name, id, nth, target, pretty);
        });
        return command;
    }

    /// <summary>
    ///     Creates the shared --target option for targeted commands.
    /// </summary>
    public static Option<int?> CreateTargetOption(string description)
    {
        return new Option<int?>("--target", description);
    }
}