using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the hand_select_card command for selecting cards from hand during combat selection mode
///     (e.g., discard, exhaust, upgrade prompts).
/// </summary>
internal static class HandSelectCardCommand
{
    /// <summary>
    ///     Creates the hand_select_card command.
    /// </summary>
    public static Command Create()
    {
        var command = new Command("hand_select_card",
            "Select cards from hand during combat selection (discard, exhaust, upgrade)");

        // Card IDs (one or more)
        var cardIdsArg = new Argument<string[]>("card_ids")
        {
            Description = "Card ID(s) to select (e.g., STRIKE_IRONCLAD)",
            Arity = ArgumentArity.OneOrMore
        };

        // --nth option for specifying which copy of each card
        var nthOption = new Option<int[]>("--nth")
        {
            Description = "N-th occurrence for each card ID (0-based). If not specified for a card, defaults to 0.",
            Arity = ArgumentArity.ZeroOrMore
        };

        command.Arguments.Add(cardIdsArg);
        command.Options.Add(nthOption);

        command.SetAction(parseResult =>
        {
            var cardIds = parseResult.GetValue(cardIdsArg)!;
            var nthValues = parseResult.GetValue(nthOption);
            var pretty = CommandExecutor.IsPretty(parseResult);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "hand_select_card",
                    CardIds = cardIds,
                    NthValues = nthValues
                },
                pretty,
                10000);
        });

        return command;
    }
}
