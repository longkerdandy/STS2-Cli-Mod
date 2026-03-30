using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the tri_select_skip command for skipping a three-choose-one card selection.
/// </summary>
internal static class TriSelectSkipCommand
{
    /// <summary>
    ///     Creates the tri_select_skip command for skipping the NChooseACardSelectionScreen
    ///     (only if the screen allows skipping).
    /// </summary>
    public static Command Create()
    {
        var command = new Command("tri_select_skip",
            "Skip a three-choose-one card selection (if allowed)");

        command.SetAction(parseResult =>
        {
            var pretty = CommandExecutor.IsPretty(parseResult);

            return CommandExecutor.ExecuteAsync(
                () => new Request { Cmd = "tri_select_skip" },
                pretty,
                10000);
        });

        return command;
    }
}
