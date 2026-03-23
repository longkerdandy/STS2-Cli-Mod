using System.CommandLine;
using STS2.Cli.Cmd.Models.Message;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the deck_select_card command for selecting cards from deck grid selection screens.
/// </summary>
internal static class DeckSelectCardCommand
{
    /// <summary>
    ///     Creates the deck_select_card command for selecting cards from deck card selection screen.
    /// </summary>
    public static Command Create(Option<bool> prettyOption)
    {
        var command = new Command("deck_select_card",
            "Select cards from a deck card selection screen (remove, upgrade, transform, enchant)");

        // Card IDs (one or more)
        var cardIdsArg = new Argument<string[]>("card_ids",
            description: "Card ID(s) to select (e.g., STRIKE_IRONCLAD)") { Arity = ArgumentArity.OneOrMore };

        // --nth option for specifying which copy of each card
        var nthOption = new Option<int[]>("--nth",
            description: "N-th occurrence for each card ID (0-based). If not specified for a card, defaults to 0.") { Arity = ArgumentArity.ZeroOrMore };

        command.AddArgument(cardIdsArg);
        command.AddOption(nthOption);

        command.SetHandler(async context =>
        {
            var cardIds = context.ParseResult.GetValueForArgument(cardIdsArg);
            var nthValues = context.ParseResult.GetValueForOption(nthOption);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);

            context.ExitCode = await CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "deck_select_card",
                    CardIds = cardIds,
                    NthValues = nthValues
                },
                pretty,
                timeoutMs: 10000);
        });

        return command;
    }
}
