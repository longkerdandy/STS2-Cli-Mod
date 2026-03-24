using System.CommandLine;
using STS2.Cli.Cmd.Models.Message;

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
        var autoOption = new Option<bool>("--auto")
        {
            Description = "Auto-advance all dialogue lines until options appear",
            DefaultValueFactory = _ => false
        };

        var command = new Command(name, description);
        command.Options.Add(autoOption);
        command.Options.Add(prettyOption);

        command.SetAction(parseResult =>
        {
            var auto = parseResult.GetValue(autoOption);
            var pretty = parseResult.GetValue(prettyOption);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = name,
                    Args = [auto ? 1 : 0]
                },
                pretty);
        });

        return command;
    }
}