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
        var typeOption = new Option<string>("--type",
            () => "card",
            "Reward type (only 'card' is supported)");

        // --card_id (required - which card to pick)
        var cardIdOption = new Option<string>("--card_id",
            "Card ID to select (e.g., STRIKE_IRONCLAD)")
        {
            IsRequired = true
        };

        // --nth (optional - which card reward if multiple)
        var nthOption = new Option<int>("--nth",
            () => 0,
            "N-th card reward when multiple exist (0-based). Optional, defaults to 0.");

        var command = new Command(name, description);
        command.AddOption(typeOption);
        command.AddOption(cardIdOption);
        command.AddOption(nthOption);

        command.SetHandler(async context =>
        {
            var type = context.ParseResult.GetValueForOption(typeOption);
            var cardId = context.ParseResult.GetValueForOption(cardIdOption);
            var nth = context.ParseResult.GetValueForOption(nthOption);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);

            context.ExitCode = await CommandExecutor.ExecuteAsync(
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