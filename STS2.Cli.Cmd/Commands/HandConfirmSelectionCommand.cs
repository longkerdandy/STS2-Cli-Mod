using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the hand_confirm_selection command for confirming the hand card selection.
/// </summary>
internal static class HandConfirmSelectionCommand
{
    /// <summary>
    ///     Creates the hand_confirm_selection command.
    /// </summary>
    public static Command Create()
    {
        var command = new Command("hand_confirm_selection",
            "Confirm the current hand card selection");

        command.SetAction(parseResult =>
        {
            var pretty = CommandExecutor.IsPretty(parseResult);

            return CommandExecutor.ExecuteAsync(
                () => new Request { Cmd = "hand_confirm_selection" },
                pretty,
                10000);
        });

        return command;
    }
}
