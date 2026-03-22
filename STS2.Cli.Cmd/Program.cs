using System.CommandLine;
using STS2.Cli.Cmd.Services;

namespace STS2.Cli.Cmd;

/// <summary>
///     Entry point for the STS2 CLI application.
///     Uses the System.CommandLine library with the subcommand pattern (similar to git, docker).
///     Each subcommand corresponds to a game action, naming consistent with the pipe protocol.
/// </summary>
internal static class Program
{
    /// <summary>
    ///     Application entry point.
    /// </summary>
    /// <param name="args">Command line arguments, e.g.: ["ping"], ["play_card", "0", "--target", "2"]</param>
    /// <returns>Exit code: 0=success, 1=connection error, 2=invalid state, 3=invalid parameter, 4=timeout</returns>
    private static async Task<int> Main(string[] args)
    {
        // Force UTF-8 encoding for proper Chinese character display in WSL
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        var rootCommand = new RootCommand("STS2 CLI - Control Slay the Spire 2 via command line");

        // Global --pretty option for formatted JSON output
        var prettyOption = new Option<bool>(
            ["--pretty", "-p"],
            description: "Format JSON output with indentation for readability",
            getDefaultValue: () => false);
        rootCommand.AddGlobalOption(prettyOption);

        // sts2 ping — test connection to the mod
        rootCommand.AddCommand(CreateSimpleCommand("ping", "Test connection to the mod", prettyOption));

        // sts2 state — retrieve current game state
        rootCommand.AddCommand(CreateSimpleCommand("state", "Get current game state", prettyOption));

        // sts2 end_turn — end the current turn
        rootCommand.AddCommand(CreateSimpleCommand("end_turn", "End the current turn", prettyOption));

        // sts2 play_card <card_id> [--nth] [--target] — play a card from hand
        rootCommand.AddCommand(CreateIdBasedCommand(
            "play_card", "Play a card from hand",
            new Argument<string>("card_id", "Card ID (e.g., STRIKE_IRONCLAD, DEFEND_SILENT)"),
            new Option<int>("--nth", () => 0, "N-th occurrence when multiple copies exist (0-based)"),
            CreateTargetOption("Target enemy combat ID (for targeted cards)"),
            prettyOption));

        // sts2 use_potion <potion_id> [--nth] [--target] — use a potion
        rootCommand.AddCommand(CreateIdBasedCommand(
            "use_potion", "Use a potion",
            new Argument<string>("potion_id", "Potion ID (e.g., FIRE_POTION, ENTROPIC_BREW)"),
            new Option<int>("--nth", () => 0, "N-th occurrence when multiple copies exist (0-based)"),
            CreateTargetOption("Target enemy combat ID (for targeted potions)"),
            prettyOption));

        // sts2 claim_reward <index> — claim a non-card reward (gold, potion, relic)
        rootCommand.AddCommand(CreateIndexedCommand(
            "claim_reward", "Claim a reward (gold, potion, relic)",
            new Argument<int>("index", "Reward index in the reward list (0-based)"),
            prettyOption));

        // sts2 choose_card <reward_index> <card_index> — pick a card from a card reward
        rootCommand.AddCommand(CreateTwoArgCommand(
            "choose_card", "Pick a card from a card reward",
            new Argument<int>("reward_index", "Reward index in the reward list (0-based)"),
            new Argument<int>("card_index", "Card index within the card reward choices (0-based)"),
            prettyOption));

        // sts2 skip_card <reward_index> — skip a card reward (take nothing)
        rootCommand.AddCommand(CreateIndexedCommand(
            "skip_card", "Skip a card reward",
            new Argument<int>("reward_index", "Reward index in the reward list (0-based)"),
            prettyOption));

        // sts2 proceed — leave the reward screen and proceed to the map
        rootCommand.AddCommand(CreateSimpleCommand("proceed", "Leave reward screen and proceed to map", prettyOption));

        return await rootCommand.InvokeAsync(args);

        // Shared --target option factory for targeted commands
        Option<int?> CreateTargetOption(string description) =>
            new("--target", description);
    }

    /// <summary>
    ///     Creates a command with ID-based arguments (e.g., play_card, use_potion).
    /// </summary>
    private static Command CreateIdBasedCommand(
        string name, string description,
        Argument<string> idArg,
        Option<int> nthOption,
        Option<int?> targetOption,
        Option<bool> prettyOption)
    {
        var command = new Command(name, description);
        command.AddArgument(idArg);
        command.AddOption(nthOption);
        command.AddOption(targetOption);
        command.SetHandler(async context =>
        {
            var id = context.ParseResult.GetValueForArgument(idArg);
            var nth = context.ParseResult.GetValueForOption(nthOption);
            var target = context.ParseResult.GetValueForOption(targetOption);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);
            context.ExitCode = await CommandRunner.ExecuteAsync(name, id, nth, target, pretty);
        });
        return command;
    }

    /// <summary>
    ///     Creates a simple command with no arguments (e.g., ping, state, end_turn).
    /// </summary>
    private static Command CreateSimpleCommand(string name, string description, Option<bool> prettyOption)
    {
        var command = new Command(name, description);
        command.SetHandler(async context =>
        {
            var pretty = context.ParseResult.GetValueForOption(prettyOption);
            context.ExitCode = await CommandRunner.ExecuteAsync(name, pretty: pretty);
        });
        return command;
    }

    /// <summary>
    ///     Creates a command with a single positional argument and no target (e.g., claim_reward, skip_card).
    /// </summary>
    private static Command CreateIndexedCommand(
        string name, string description,
        Argument<int> indexArg,
        Option<bool> prettyOption)
    {
        var command = new Command(name, description);
        command.AddArgument(indexArg);
        command.SetHandler(async context =>
        {
            var index = context.ParseResult.GetValueForArgument(indexArg);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);
            context.ExitCode = await CommandRunner.ExecuteAsync(name, [index], pretty: pretty);
        });
        return command;
    }

    /// <summary>
    ///     Creates a command with two positional arguments (e.g., choose_card reward_index card_index).
    /// </summary>
    private static Command CreateTwoArgCommand(
        string name, string description,
        Argument<int> firstArg, Argument<int> secondArg,
        Option<bool> prettyOption)
    {
        var command = new Command(name, description);
        command.AddArgument(firstArg);
        command.AddArgument(secondArg);
        command.SetHandler(async context =>
        {
            var first = context.ParseResult.GetValueForArgument(firstArg);
            var second = context.ParseResult.GetValueForArgument(secondArg);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);
            context.ExitCode = await CommandRunner.ExecuteAsync(name, [first, second], pretty: pretty);
        });
        return command;
    }
}