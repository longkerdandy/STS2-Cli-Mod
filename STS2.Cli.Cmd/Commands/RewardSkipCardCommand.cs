using System.CommandLine;
using STS2.Cli.Cmd.Models.Message;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the reward_skip_card command for skipping a card reward.
/// </summary>
internal static class RewardSkipCardCommand
{
    /// <summary>
    ///     Creates the skip_card command for skipping a card reward.
    /// </summary>
    public static Command Create(string name, string description, Option<bool> prettyOption)
    {
        // --type card (only card rewards can be skipped)
        var typeOption = new Option<string>("--type")
        {
            Description = "Reward type (only 'card' is supported)",
            DefaultValueFactory = _ => "card"
        };

        // --nth (optional - which card reward if multiple)
        var nthOption = new Option<int>("--nth")
        {
            Description = "N-th card reward when multiple exist (0-based). Optional, defaults to 0.",
            DefaultValueFactory = _ => 0
        };

        var command = new Command(name, description);
        command.Options.Add(typeOption);
        command.Options.Add(nthOption);
        command.Options.Add(prettyOption);

        command.SetAction(parseResult =>
        {
            var type = parseResult.GetValue(typeOption)!;
            var nth = parseResult.GetValue(nthOption);
            var pretty = parseResult.GetValue(prettyOption);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "skip_card",
                    RewardType = type,
                    Nth = nth
                },
                pretty);
        });

        return command;
    }
}