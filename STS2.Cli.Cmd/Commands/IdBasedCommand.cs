using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

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
        Option<int?> targetOption)
    {
        var command = new Command(name, description);
        command.Arguments.Add(idArg);
        command.Options.Add(nthOption);
        command.Options.Add(targetOption);

        command.SetAction(parseResult =>
        {
            var id = parseResult.GetValue(idArg)!;
            var nth = parseResult.GetValue(nthOption);
            var target = parseResult.GetValue(targetOption);
            var pretty = CommandExecutor.IsPretty(parseResult);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = name,
                    Id = id,
                    Nth = nth,
                    Target = target
                },
                pretty);
        });

        return command;
    }

    /// <summary>
    ///     Creates the shared --target option for targeted commands.
    /// </summary>
    public static Option<int?> CreateTargetOption(string description)
    {
        return new Option<int?>("--target")
        {
            Description = description
        };
    }
}
