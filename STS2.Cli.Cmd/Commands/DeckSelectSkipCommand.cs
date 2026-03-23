using System.CommandLine;
using STS2.Cli.Cmd.Services;

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

        command.SetHandler(async context =>
        {
            var pretty = context.ParseResult.GetValueForOption(prettyOption);
            context.ExitCode = await CommandRunner.ExecuteDeckSelectSkipAsync(pretty);
        });

        return command;
    }
}