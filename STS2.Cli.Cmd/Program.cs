using System.CommandLine;
using STS2.Cli.Cmd.Services;

namespace STS2.Cli.Cmd;

/// <summary>
///     Entry point for the STS2 CLI application.
///     Design notes:
///     - Uses the System.CommandLine library for CLI infrastructure
///     - Follows the subcommand pattern (similar to git status, docker ps)
///     - RootCommand acts as a router, dispatching to subcommand handlers
///     - Each command is defined independently for maintainability and extensibility
/// </summary>
internal static class Program
{
    /// <summary>
    ///     Application entry point.
    /// </summary>
    /// <param name="args">Command line arguments, e.g.: ["ping"], ["play_card", "0", "--target", "jaw_worm"]</param>
    /// <returns>Exit code: 0=success, 1=connection error, 2=invalid state, 3=invalid parameter, 4=timeout</returns>
    private static async Task<int> Main(string[] args)
    {
        // Create root command (represents the program itself)
        // When a user runs the program without subcommands, displays this description and available commands
        var rootCommand = new RootCommand("STS2 CLI - Control Slay the Spire 2 via command line");

        // Add a global --pretty option for formatted JSON output
        var prettyOption = new Option<bool>(
            ["--pretty", "-p"],
            description: "Format JSON output with indentation for readability",
            getDefaultValue: () => false);
        rootCommand.AddGlobalOption(prettyOption);

        // Register all subcommands
        // Each subcommand corresponds to a game action, naming consistent with the protocol
        rootCommand.AddCommand(CreatePingCommand(prettyOption)); // sts2 ping
        rootCommand.AddCommand(CreateStateCommand(prettyOption)); // sts2 state
        rootCommand.AddCommand(CreatePlayCardCommand(prettyOption)); // sts2 play_card <index> [--target]
        rootCommand.AddCommand(CreateEndTurnCommand(prettyOption)); // sts2 end_turn
        rootCommand.AddCommand(CreateUsePotionCommand(prettyOption)); // sts2 use_potion <slot> [--target]

        // Parse and execute the command
        // InvokeAsync will:
        // 1. Parse args array
        // 2. Match the appropriate subcommand
        // 3. Execute that command's handler
        // 4. Return the exit code
        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    ///     Creates the ping command: tests connection to the mod.
    /// </summary>
    /// <example>
    ///     $ STS2.Cli.Cmd ping
    ///     {"ok":true,"data":{"connected":true}}
    ///     $ STS2.Cli.Cmd --pretty ping
    ///     {
    ///     "ok": true,
    ///     "data": {
    ///     "connected": true
    ///     }
    ///     }
    /// </example>
    private static Command CreatePingCommand(Option<bool> prettyOption)
    {
        var command = new Command("ping", "Test connection to the mod");

        // Set command handler
        // context contains parsed arguments and options
        command.SetHandler(async context =>
        {
            var pretty = context.ParseResult.GetValueForOption(prettyOption);
            var exitCode = await CommandRunner.ExecuteAsync("ping", pretty: pretty);
            context.ExitCode = exitCode;
        });

        return command;
    }

    /// <summary>
    ///     Creates the state command: retrieves current game state.
    /// </summary>
    /// <example>
    ///     $ STS2.Cli.Cmd state
    ///     {"ok":true,"data":{"screen":"COMBAT","player": {...}}}
    ///     $ STS2.Cli.Cmd --pretty state
    ///     {
    ///     "ok": true,
    ///     "data": {
    ///     "screen": "COMBAT",
    ///     "player": { ... }
    ///     }
    ///     }
    /// </example>
    private static Command CreateStateCommand(Option<bool> prettyOption)
    {
        var command = new Command("state", "Get current game state");

        command.SetHandler(async context =>
        {
            var pretty = context.ParseResult.GetValueForOption(prettyOption);
            var exitCode = await CommandRunner.ExecuteAsync("state", pretty: pretty);
            context.ExitCode = exitCode;
        });

        return command;
    }

    /// <summary>
    ///     Creates the play_card command: plays a card from hand.
    /// </summary>
    /// <remarks>
    ///     Arguments:
    ///     - index: Index in hand (0-based, from state command response)
    ///     - --target: Target enemy ID (only for cards that require a target)
    /// </remarks>
    /// <example>
    ///     $ STS2.Cli.Cmd play_card 0
    ///     $ STS2.Cli.Cmd play_card 0 --target jaw_worm_0
    ///     $ STS2.Cli.Cmd --pretty play_card 0
    /// </example>
    private static Command CreatePlayCardCommand(Option<bool> prettyOption)
    {
        var command = new Command("play_card", "Play a card from hand");

        // Define positional argument: card index
        var indexArg = new Argument<int>("index", "Card index in hand (0-based)");
        command.AddArgument(indexArg);

        // Define optional argument: target entity ID
        var targetOption = new Option<string?>("--target", "Target enemy entity ID (for targeted cards)");
        command.AddOption(targetOption);

        command.SetHandler(async context =>
        {
            // Get argument values from parse result
            var index = context.ParseResult.GetValueForArgument(indexArg);
            var target = context.ParseResult.GetValueForOption(targetOption);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);

            // Send command to mod, command name matches CLI command name
            var exitCode = await CommandRunner.ExecuteAsync("play_card", [index], target, pretty);
            context.ExitCode = exitCode;
        });

        return command;
    }

    /// <summary>
    ///     Creates the end_turn command: ends the current turn.
    /// </summary>
    /// <example>
    ///     $ STS2.Cli.Cmd end_turn
    ///     {"ok":true,"data":{"action":"end_turn"}}
    ///     $ STS2.Cli.Cmd --pretty end_turn
    /// </example>
    private static Command CreateEndTurnCommand(Option<bool> prettyOption)
    {
        var command = new Command("end_turn", "End the current turn");

        command.SetHandler(async context =>
        {
            var pretty = context.ParseResult.GetValueForOption(prettyOption);
            var exitCode = await CommandRunner.ExecuteAsync("end_turn", pretty: pretty);
            context.ExitCode = exitCode;
        });

        return command;
    }

    /// <summary>
    ///     Creates the use_potion command: uses a potion.
    /// </summary>
    /// <remarks>
    ///     Potion slots are typically 0-2, depending on the player's potion slots.
    /// </remarks>
    /// <example>
    ///     $ STS2.Cli.Cmd use_potion 0
    ///     $ STS2.Cli.Cmd use_potion 0 --target jaw_worm_0
    ///     $ STS2.Cli.Cmd --pretty use_potion 0
    /// </example>
    private static Command CreateUsePotionCommand(Option<bool> prettyOption)
    {
        var command = new Command("use_potion", "Use a potion");

        // Define positional argument: potion slot
        var slotArg = new Argument<int>("slot", "Potion slot index (0-2)");
        command.AddArgument(slotArg);

        // Define optional argument: target entity ID
        var targetOption = new Option<string?>("--target", "Target enemy entity ID (for targeted potions)");
        command.AddOption(targetOption);

        command.SetHandler(async context =>
        {
            var slot = context.ParseResult.GetValueForArgument(slotArg);
            var target = context.ParseResult.GetValueForOption(targetOption);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);
            var exitCode = await CommandRunner.ExecuteAsync("use_potion", [slot], target, pretty);
            context.ExitCode = exitCode;
        });

        return command;
    }
}