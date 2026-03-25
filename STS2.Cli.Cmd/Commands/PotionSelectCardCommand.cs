using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the potion_select_card command for selecting cards from potion selection screens.
/// </summary>
internal static class PotionSelectCardCommand
{
    /// <summary>
    ///     Creates the potion_select_card command for selecting cards from the potion-opened card selection screen.
    /// </summary>
    public static Command Create(Option<bool> prettyOption)
    {
        var command = new Command("potion_select_card",
            "Select cards from a potion-opened card selection screen");

        // Card IDs (one or more)
        var cardIdsArg = new Argument<string[]>("card_ids")
        {
            Description = "Card ID(s) to select (e.g., STRIKE_IRONCLAD)",
            Arity = ArgumentArity.ZeroOrMore
        };

        // --nth option for specifying which copy of each card
        var nthOption = new Option<int[]>("--nth")
        {
            Description = "N-th occurrence for each card ID (0-based). If not specified for a card, defaults to 0.",
            Arity = ArgumentArity.ZeroOrMore
        };

        // --skip flag
        var skipOption = new Option<bool>("--skip")
        {
            Description = "Skip this selection (if allowed by the potion)",
            DefaultValueFactory = _ => false
        };

        command.Arguments.Add(cardIdsArg);
        command.Options.Add(nthOption);
        command.Options.Add(skipOption);
        command.Options.Add(prettyOption);

        command.SetAction(parseResult =>
        {
            var cardIds = parseResult.GetValue(cardIdsArg)!;
            var nthValues = parseResult.GetValue(nthOption);
            var skip = parseResult.GetValue(skipOption);
            var pretty = parseResult.GetValue(prettyOption);

            if (skip)
                // Skip selection
                return CommandExecutor.ExecuteAsync(
                    () => new Request
                    {
                        Cmd = "potion_select_skip",
                        Skip = true
                    },
                    pretty);

            if (cardIds.Length == 0)
                // No cards specified and not skipping - error
                return CommandExecutor.ExecuteErrorAsync(
                    "MISSING_ARGUMENT",
                    "Either specify card ID(s) or use --skip",
                    pretty);

            // Select specified cards
            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "potion_select_card",
                    CardIds = cardIds,
                    NthValues = nthValues
                },
                pretty);
        });

        return command;
    }
}