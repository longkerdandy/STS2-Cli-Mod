using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates commands for buying items from the shop by ID with optional nth.
///     Similar to <see cref="IdBasedCommand" /> but without the --target option.
/// </summary>
internal static class ShopBuyCardCommand
{
    /// <summary>
    ///     Creates the shop_buy_card command.
    /// </summary>
    public static Command Create()
    {
        var cardIdArg = new Argument<string>("card_id")
        {
            Description = "Card ID to buy (e.g., STRIKE_IRONCLAD, DEFEND_SILENT)"
        };
        var nthOption = new Option<int>("--nth")
        {
            Description = "N-th occurrence when multiple copies exist (0-based)",
            DefaultValueFactory = _ => 0
        };

        var command = new Command("shop_buy_card", "Buy a card from the shop");
        command.Arguments.Add(cardIdArg);
        command.Options.Add(nthOption);

        command.SetAction(parseResult =>
        {
            var cardId = parseResult.GetValue(cardIdArg)!;
            var nth = parseResult.GetValue(nthOption);
            var pretty = CommandExecutor.IsPretty(parseResult);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "shop_buy_card",
                    Id = cardId,
                    Nth = nth
                },
                pretty);
        });

        return command;
    }
}
