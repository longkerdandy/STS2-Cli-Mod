using System.CommandLine;
using STS2.Cli.Cmd.Models.Message;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the reward_choose_card command for selecting a card from a card reward.
/// </summary>
internal static class RewardChooseCardCommand
{
    /// <summary>
    ///     Creates the choose_card command for selecting a card from a card reward.
    /// </summary>
    public static Command Create(string name, string description, Option<bool> prettyOption)
    {
        // --type card (only card rewards are supported)
        var typeOption = new Option<string>("--type")
        {
            Description = "Reward type (only 'card' is supported)",
            DefaultValueFactory = _ => "card"
        };

        // --card_id (required - which card to pick)
        var cardIdOption = new Option<string>("--card_id")
        {
            Description = "Card ID to select (e.g., STRIKE_IRONCLAD)",
            Required = true
        };

        // --nth (optional - which card reward if multiple)
        var nthOption = new Option<int>("--nth")
        {
            Description = "N-th card reward when multiple exist (0-based). Optional, defaults to 0.",
            DefaultValueFactory = _ => 0
        };

        var command = new Command(name, description);
        command.Options.Add(typeOption);
        command.Options.Add(cardIdOption);
        command.Options.Add(nthOption);
        command.Options.Add(prettyOption);

        command.SetAction(parseResult =>
        {
            var type = parseResult.GetValue(typeOption)!;
            var cardId = parseResult.GetValue(cardIdOption)!;
            var nth = parseResult.GetValue(nthOption);
            var pretty = parseResult.GetValue(prettyOption);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "choose_card",
                    RewardType = type,
                    CardId = cardId,
                    Nth = nth
                },
                pretty);
        });

        return command;
    }
}
