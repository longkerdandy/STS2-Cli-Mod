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

        // sts2 reward_claim --type <type> [--id <id>] [--nth <n>] — claim a non-card reward
        rootCommand.AddCommand(CreateRewardCommand(
            "reward_claim", "Claim a reward by type (gold, potion, relic, special_card)",
            prettyOption));

        // sts2 reward_choose_card --type card [--nth <n>] --card_id <card_id> — pick a card from a card reward
        rootCommand.AddCommand(CreateChooseCardCommand(
            "reward_choose_card", "Pick a card from a card reward",
            prettyOption));

        // sts2 reward_skip_card --type card [--nth <n>] — skip a card reward
        rootCommand.AddCommand(CreateSkipCardCommand(
            "reward_skip_card", "Skip a card reward",
            prettyOption));

        // sts2 choose_event <index> — choose an option in an event
        rootCommand.AddCommand(CreateIndexedCommand(
            "choose_event", "Choose an option in an event room",
            new Argument<int>("index", "Option index (0-based)"),
            prettyOption));

        // sts2 advance_dialogue [--auto] — advance Ancient event dialogue
        rootCommand.AddCommand(CreateAdvanceDialogueCommand(
            "advance_dialogue", "Advance dialogue in an Ancient event",
            prettyOption));

        // sts2 reward_proceed — leave the reward screen and proceed to the map
        rootCommand.AddCommand(CreateSimpleCommand("reward_proceed", "Leave reward screen and proceed to map", prettyOption));

        // sts2 potion_select_card <card_id> [<card_id>...] [--nth <n>...] [--skip] — select cards from potion selection screen
        rootCommand.AddCommand(CreatePotionSelectCardCommand(prettyOption));

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

    /// <summary>
    ///     Creates a reward claim command with type, optional ID, and optional nth.
    /// </summary>
    private static Command CreateRewardCommand(
        string name, string description,
        Option<bool> prettyOption)
    {
        var typeOption = new Option<string>("--type",
            description: "Reward type: gold, potion, relic, special_card",
            parseArgument: result =>
            {
                var value = result.Tokens.Single().Value.ToLower();
                if (value is "gold" or "potion" or "relic" or "special_card")
                    return value;
                result.ErrorMessage = $"Invalid reward type '{value}'. Must be one of: gold, potion, relic, special_card";
                return null!;
            });
        typeOption.IsRequired = true;

        var idOption = new Option<string>("--id",
            description: "Item ID (potion_id, relic_id, or card_id). Required for potion, relic, and special_card.");

        var nthOption = new Option<int>("--nth",
            () => 0,
            description: "N-th occurrence when multiple rewards of same type exist (0-based). Optional, defaults to 0.");

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
                context.ExitCode = await CommandRunner.ExecuteAsync(name, 
                    error: $"MISSING_ARGUMENT", 
                    message: $"Reward type '{type}' requires --id parameter",
                    pretty: pretty);
                return;
            }

            context.ExitCode = await CommandRunner.ExecuteRewardAsync(name, type, id, nth, pretty);
        });

        return command;
    }

    /// <summary>
    ///     Creates a choose_card command for selecting a card from a card reward.
    /// </summary>
    private static Command CreateChooseCardCommand(
        string name, string description,
        Option<bool> prettyOption)
    {
        // --type card (only card rewards are supported)
        var typeOption = new Option<string>("--type",
            () => "card",
            description: "Reward type (only 'card' is supported)");

        // --card_id (required - which card to pick)
        var cardIdOption = new Option<string>("--card_id",
            description: "Card ID to select (e.g., STRIKE_IRONCLAD)");
        cardIdOption.IsRequired = true;

        // --nth (optional - which card reward if multiple)
        var nthOption = new Option<int>("--nth",
            () => 0,
            description: "N-th card reward when multiple exist (0-based). Optional, defaults to 0.");

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

            context.ExitCode = await CommandRunner.ExecuteChooseCardAsync(type, cardId, nth, pretty);
        });

        return command;
    }

    /// <summary>
    ///     Creates a skip_card command for skipping a card reward.
    /// </summary>
    private static Command CreateSkipCardCommand(
        string name, string description,
        Option<bool> prettyOption)
    {
        // --type card (only card rewards can be skipped)
        var typeOption = new Option<string>("--type",
            () => "card",
            description: "Reward type (only 'card' is supported)");

        // --nth (optional - which card reward if multiple)
        var nthOption = new Option<int>("--nth",
            () => 0,
            description: "N-th card reward when multiple exist (0-based). Optional, defaults to 0.");

        var command = new Command(name, description);
        command.AddOption(typeOption);
        command.AddOption(nthOption);

        command.SetHandler(async context =>
        {
            var type = context.ParseResult.GetValueForOption(typeOption);
            var nth = context.ParseResult.GetValueForOption(nthOption);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);

            context.ExitCode = await CommandRunner.ExecuteSkipCardAsync(type, nth, pretty);
        });

        return command;
    }

    /// <summary>
    ///     Creates an advance_dialogue command for Ancient events.
    /// </summary>
    private static Command CreateAdvanceDialogueCommand(
        string name, string description,
        Option<bool> prettyOption)
    {
        // --auto (optional - auto-advance all dialogue lines)
        var autoOption = new Option<bool>("--auto",
            () => false,
            description: "Auto-advance all dialogue lines until options appear");

        var command = new Command(name, description);
        command.AddOption(autoOption);

        command.SetHandler(async context =>
        {
            var auto = context.ParseResult.GetValueForOption(autoOption);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);

            context.ExitCode = await CommandRunner.ExecuteAdvanceDialogueAsync(auto, pretty);
        });

        return command;
    }

    /// <summary>
    ///     Creates the potion_select_card command for selecting cards from potion selection screens.
    /// </summary>
    private static Command CreatePotionSelectCardCommand(Option<bool> prettyOption)
    {
        var command = new Command("potion_select_card",
            "Select cards from a potion-opened card selection screen");

        // Card IDs (one or more)
        var cardIdsArg = new Argument<string[]>("card_ids",
            description: "Card ID(s) to select (e.g., STRIKE_IRONCLAD)") { Arity = ArgumentArity.ZeroOrMore };

        // --nth option for specifying which copy of each card
        var nthOption = new Option<int[]>("--nth",
            description: "N-th occurrence for each card ID (0-based). If not specified for a card, defaults to 0.") { Arity = ArgumentArity.ZeroOrMore };

        // --skip flag
        var skipOption = new Option<bool>("--skip",
            () => false,
            description: "Skip this selection (if allowed by the potion)");

        command.AddArgument(cardIdsArg);
        command.AddOption(nthOption);
        command.AddOption(skipOption);

        command.SetHandler(async context =>
        {
            var cardIds = context.ParseResult.GetValueForArgument(cardIdsArg);
            var nthValues = context.ParseResult.GetValueForOption(nthOption);
            var skip = context.ParseResult.GetValueForOption(skipOption);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);

            if (skip)
            {
                // Skip selection
                context.ExitCode = await CommandRunner.ExecutePotionSelectSkipAsync(pretty);
            }
            else if (cardIds.Length == 0)
            {
                // No cards specified and not skipping - error
                context.ExitCode = await CommandRunner.ExecuteAsync("potion_select_card",
                    error: "MISSING_ARGUMENT",
                    message: "Either specify card ID(s) or use --skip",
                    pretty: pretty);
            }
            else
            {
                // Select specified cards
                context.ExitCode = await CommandRunner.ExecutePotionSelectCardAsync(cardIds, nthValues, pretty);
            }
        });

        return command;
    }
}