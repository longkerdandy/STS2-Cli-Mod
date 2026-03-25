using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the shop_buy_potion command for buying a potion from the shop.
/// </summary>
internal static class ShopBuyPotionCommand
{
    /// <summary>
    ///     Creates the shop_buy_potion command.
    /// </summary>
    public static Command Create(Option<bool> prettyOption)
    {
        var potionIdArg = new Argument<string>("potion_id")
        {
            Description = "Potion ID to buy (e.g., FIRE_POTION, ENTROPIC_BREW)"
        };
        var nthOption = new Option<int>("--nth")
        {
            Description = "N-th occurrence when multiple copies exist (0-based)",
            DefaultValueFactory = _ => 0
        };

        var command = new Command("shop_buy_potion", "Buy a potion from the shop");
        command.Arguments.Add(potionIdArg);
        command.Options.Add(nthOption);
        command.Options.Add(prettyOption);

        command.SetAction(parseResult =>
        {
            var potionId = parseResult.GetValue(potionIdArg)!;
            var nth = parseResult.GetValue(nthOption);
            var pretty = parseResult.GetValue(prettyOption);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "shop_buy_potion",
                    Id = potionId,
                    Nth = nth
                },
                pretty);
        });

        return command;
    }
}
