using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the shop_remove_card command for buying the card removal service.
///     This is a simple command with no arguments — the service opens a deck
///     card selection screen where the player picks a card to remove.
/// </summary>
internal static class ShopRemoveCardCommand
{
    /// <summary>
    ///     Creates the shop_remove_card command.
    /// </summary>
    public static Command Create(Option<bool> prettyOption)
    {
        var command = new Command("shop_remove_card", "Buy card removal service from the shop");
        command.Options.Add(prettyOption);

        command.SetAction(parseResult =>
        {
            var pretty = parseResult.GetValue(prettyOption);
            return CommandExecutor.ExecuteAsync(
                () => new Request { Cmd = "shop_remove_card" },
                pretty);
        });

        return command;
    }
}
