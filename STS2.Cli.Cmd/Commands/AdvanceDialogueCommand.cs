using System.CommandLine;
using STS2.Cli.Cmd.Services;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the advance_dialogue command for Ancient events.
/// </summary>
internal static class AdvanceDialogueCommand
{
    /// <summary>
    ///     Creates an advance_dialogue command for Ancient events.
    /// </summary>
    public static Command Create(string name, string description, Option<bool> prettyOption)
    {
        // --auto (optional - auto-advance all dialogue lines)
        var autoOption = new Option<bool>("--auto",
            () => false,
            "Auto-advance all dialogue lines until options appear");

        var command = new Command(name, description);
        command.AddOption(autoOption);

        command.SetHandler(async context =>
        {
            var auto = context.ParseResult.GetValueForOption(autoOption);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);

            context.ExitCode = await CommandRunner.ExecuteAdvanceDialogueAsync(auto, pretty);
        });

        return command;
    }
}