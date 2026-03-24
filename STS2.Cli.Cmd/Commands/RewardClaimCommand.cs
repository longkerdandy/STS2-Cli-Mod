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
        var typeOption = new Option<string>("--type")
        {
            Description = "Reward type: gold, potion, relic, special_card",
            Required = true,
            CustomParser = result =>
            {
                var value = result.Tokens.Single().Value.ToLower();
                if (value is "gold" or "potion" or "relic" or "special_card")
                    return value;
                result.AddError($"Invalid reward type '{value}'. Must be one of: gold, potion, relic, special_card");
                return null!;
            }
        };

        var idOption = new Option<string>("--id")
        {
            Description = "Item ID (potion_id, relic_id, or card_id). Required for potion, relic, and special_card."
        };

        var nthOption = new Option<int>("--nth")
        {
            Description =
                "N-th occurrence when multiple rewards of same type exist (0-based). Optional, defaults to 0.",
            DefaultValueFactory = _ => 0
        };

        var command = new Command(name, description);
        command.Options.Add(typeOption);
        command.Options.Add(idOption);
        command.Options.Add(nthOption);
        command.Options.Add(prettyOption);

        command.SetAction(parseResult =>
        {
            var type = parseResult.GetValue(typeOption)!;
            var id = parseResult.GetValue(idOption);
            var nth = parseResult.GetValue(nthOption);
            var pretty = parseResult.GetValue(prettyOption);

            // Validate: potion, relic, special_card require --id
            if (type is "potion" or "relic" or "special_card" && string.IsNullOrEmpty(id))
                return CommandExecutor.ExecuteErrorAsync(
                    "MISSING_ARGUMENT",
                    $"Reward type '{type}' requires --id parameter",
                    pretty);

            return CommandExecutor.ExecuteAsync(
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