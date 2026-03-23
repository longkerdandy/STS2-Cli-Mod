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
        var typeOption = new Option<string>("--type",
            () => "card",
            "Reward type (only 'card' is supported)");

        // --nth (optional - which card reward if multiple)
        var nthOption = new Option<int>("--nth",
            () => 0,
            "N-th card reward when multiple exist (0-based). Optional, defaults to 0.");

        var command = new Command(name, description);
        command.AddOption(typeOption);
        command.AddOption(nthOption);

        command.SetHandler(async context =>
        {
            var type = context.ParseResult.GetValueForOption(typeOption);
            var nth = context.ParseResult.GetValueForOption(nthOption);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);

            context.ExitCode = await CommandExecutor.ExecuteAsync(
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