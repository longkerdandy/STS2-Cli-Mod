using System.CommandLine;
using STS2.Cli.Cmd.Models.Message;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the deck_select_skip command for cancelling a deck card selection.
/// </summary>
internal static class DeckSelectSkipCommand
{
    /// <summary>
    ///     Creates the deck_select_skip command for cancelling a deck card selection.
    /// </summary>
    public static Command Create(Option<bool> prettyOption)
    {
        var command = new Command("deck_select_skip",
            "Cancel/skip a deck card selection (if allowed)");

        command.Options.Add(prettyOption);

        command.SetAction(parseResult =>
        {
            var pretty = parseResult.GetValue(prettyOption);

            return CommandExecutor.ExecuteAsync(
                () => new Request { Cmd = "deck_select_skip" },
                pretty,
                10000);
        });

        return command;
    }
}