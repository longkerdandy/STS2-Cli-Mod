using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the shop_buy_relic command for buying a relic from the shop.
/// </summary>
internal static class ShopBuyRelicCommand
{
    /// <summary>
    ///     Creates the shop_buy_relic command.
    /// </summary>
    public static Command Create(Option<bool> prettyOption)
    {
        var relicIdArg = new Argument<string>("relic_id")
        {
            Description = "Relic ID to buy (e.g., VAJRA, BURNING_BLOOD)"
        };
        var nthOption = new Option<int>("--nth")
        {
            Description = "N-th occurrence when multiple copies exist (0-based)",
            DefaultValueFactory = _ => 0
        };

        var command = new Command("shop_buy_relic", "Buy a relic from the shop");
        command.Arguments.Add(relicIdArg);
        command.Options.Add(nthOption);
        command.Options.Add(prettyOption);

        command.SetAction(parseResult =>
        {
            var relicId = parseResult.GetValue(relicIdArg)!;
            var nth = parseResult.GetValue(nthOption);
            var pretty = parseResult.GetValue(prettyOption);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "shop_buy_relic",
                    Id = relicId,
                    Nth = nth
                },
                pretty);
        });

        return command;
    }
}
