using System.CommandLine;
using STS2.Cli.Cmd.Models.Message;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the reward_claim command for claiming non-card rewards.
/// </summary>
internal static class RewardClaimCommand
{
    /// <summary>
    ///     Creates the reward claim command with type, optional ID, and optional nth.
    /// </summary>
    public static Command Create(string name, string description, Option<bool> prettyOption)
    {
        var typeOption = new Option<string>("--type",
            description: "Reward type: gold, potion, relic, special_card",
            parseArgument: result =>
            {
                var value = result.Tokens.Single().Value.ToLower();
                if (value is "gold" or "potion" or "relic" or "special_card")
                    return value;
                result.ErrorMessage =
                    $"Invalid reward type '{value}'. Must be one of: gold, potion, relic, special_card";
                return null!;
            })
        {
            IsRequired = true
        };

        var idOption = new Option<string>("--id",
            "Item ID (potion_id, relic_id, or card_id). Required for potion, relic, and special_card.");

        var nthOption = new Option<int>("--nth",
            () => 0,
            "N-th occurrence when multiple rewards of same type exist (0-based). Optional, defaults to 0.");

        var command = new Command(name, description);
        command.AddOption(typeOption);
        command.AddOption(idOption);
        command.AddOption(nthOption);

        command.SetHandler(async context =>
        {
            var type = context.ParseResult.GetValueForOption(typeOption);
            var id = context.ParseResult.GetValueForOption(idOption);
            var nth = context.ParseResult.GetValueForOption(nthOption);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);

            // Validate: potion, relic, special_card require --id
            if (type is "potion" or "relic" or "special_card" && string.IsNullOrEmpty(id))
            {
                context.ExitCode = await CommandExecutor.ExecuteErrorAsync(
                    "MISSING_ARGUMENT",
                    $"Reward type '{type}' requires --id parameter",
                    pretty);
                return;
            }

            context.ExitCode = await CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = name,
                    RewardType = type,
                    Id = id,
                    Nth = nth
                },
                pretty);
        });

        return command;
    }
}