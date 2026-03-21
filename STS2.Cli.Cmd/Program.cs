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
        var rootCommand = new RootCommand("STS2 CLI - Control Slay the Spire 2 via command line");

        // Global --pretty option for formatted JSON output
        var prettyOption = new Option<bool>(
            ["--pretty", "-p"],
            description: "Format JSON output with indentation for readability",
            getDefaultValue: () => false);
        rootCommand.AddGlobalOption(prettyOption);

        // Shared --target option factory for targeted commands
        Option<int?> CreateTargetOption(string description) =>
            new("--target", description);

        // sts2 ping — test connection to the mod
        rootCommand.AddCommand(CreateSimpleCommand("ping", "Test connection to the mod", prettyOption));

        // sts2 state — retrieve current game state
        rootCommand.AddCommand(CreateSimpleCommand("state", "Get current game state", prettyOption));

        // sts2 end_turn — end the current turn
        rootCommand.AddCommand(CreateSimpleCommand("end_turn", "End the current turn", prettyOption));

        // sts2 play_card <index> [--target] — play a card from hand
        rootCommand.AddCommand(CreateTargetedCommand(
            "play_card", "Play a card from hand",
            new Argument<int>("index", "Card index in hand (0-based)"),
            CreateTargetOption("Target enemy combat ID (for targeted cards)"),
            prettyOption));

        // sts2 use_potion <slot> [--target] — use a potion
        rootCommand.AddCommand(CreateTargetedCommand(
            "use_potion", "Use a potion",
            new Argument<int>("slot", "Potion slot index (0-2)"),
            CreateTargetOption("Target enemy combat ID (for targeted potions)"),
            prettyOption));

        return await rootCommand.InvokeAsync(args);
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
    ///     Creates a command with a positional argument and optional --target (e.g., play_card, use_potion).
    /// </summary>
    private static Command CreateTargetedCommand(
        string name, string description,
        Argument<int> indexArg, Option<int?> targetOption,
        Option<bool> prettyOption)
    {
        var command = new Command(name, description);
        command.AddArgument(indexArg);
        command.AddOption(targetOption);
        command.SetHandler(async context =>
        {
            var index = context.ParseResult.GetValueForArgument(indexArg);
            var target = context.ParseResult.GetValueForOption(targetOption);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);
            context.ExitCode = await CommandRunner.ExecuteAsync(name, [index], target, pretty);
        });
        return command;
    }
}