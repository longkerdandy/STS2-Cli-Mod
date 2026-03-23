using System.CommandLine;
using STS2.Cli.Cmd.Models.Message;

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
        var cardIdsArg = new Argument<string[]>("card_ids",
            "Card ID(s) to select (e.g., STRIKE_IRONCLAD)") { Arity = ArgumentArity.ZeroOrMore };

        // --nth option for specifying which copy of each card
        var nthOption = new Option<int[]>("--nth",
                "N-th occurrence for each card ID (0-based). If not specified for a card, defaults to 0.")
            { Arity = ArgumentArity.ZeroOrMore };

        // --skip flag
        var skipOption = new Option<bool>("--skip",
            () => false,
            "Skip this selection (if allowed by the potion)");

        command.AddArgument(cardIdsArg);
        command.AddOption(nthOption);
        command.AddOption(skipOption);

        command.SetHandler(async context =>
        {
            var cardIds = context.ParseResult.GetValueForArgument(cardIdsArg);
            var nthValues = context.ParseResult.GetValueForOption(nthOption);
            var skip = context.ParseResult.GetValueForOption(skipOption);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);

            if (skip)
                // Skip selection
                context.ExitCode = await CommandExecutor.ExecuteAsync(
                    () => new Request
                    {
                        Cmd = "potion_select_skip",
                        Skip = true
                    },
                    pretty);
            else if (cardIds.Length == 0)
                // No cards specified and not skipping - error
                context.ExitCode = await CommandExecutor.ExecuteErrorAsync(
                    "MISSING_ARGUMENT",
                    "Either specify card ID(s) or use --skip",
                    pretty);
            else
                // Select specified cards
                context.ExitCode = await CommandExecutor.ExecuteAsync(
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