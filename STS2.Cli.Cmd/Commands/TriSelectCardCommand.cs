using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the tri_select_card command for selecting a card from a three-choose-one card selection screen.
/// </summary>
internal static class TriSelectCardCommand
{
    /// <summary>
    ///     Creates the tri_select_card command for selecting a card from the NChooseACardSelectionScreen
    ///     (triggered by potions, cards like Discovery/Quasar/Splash, relics, and monsters).
    /// </summary>
    public static Command Create()
    {
        var command = new Command("tri_select_card",
            "Select a card from a three-choose-one card selection screen");

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

            // Select specified cards
            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "tri_select_card",
                    CardIds = cardIds,
                    NthValues = nthValues
                },
                pretty);
        });

        return command;
    }
}
