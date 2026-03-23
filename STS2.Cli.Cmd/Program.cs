using System.CommandLine;
using System.Text;
using STS2.Cli.Cmd.Commands;

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
        Console.OutputEncoding = Encoding.UTF8;

        var rootCommand = new RootCommand("STS2 CLI - Control Slay the Spire 2 via command line");

        // Global --pretty option for formatted JSON output
        var prettyOption = new Option<bool>(
            ["--pretty", "-p"],
            description: "Format JSON output with indentation for readability",
            getDefaultValue: () => false);
        rootCommand.AddGlobalOption(prettyOption);

        // Simple commands (no arguments)
        rootCommand.AddCommand(SimpleCommand.Create("ping", "Test connection to the mod", prettyOption));
        rootCommand.AddCommand(SimpleCommand.Create("state", "Get current game state", prettyOption));
        rootCommand.AddCommand(SimpleCommand.Create("end_turn", "End the current turn", prettyOption));
        rootCommand.AddCommand(SimpleCommand.Create("reward_proceed", "Leave reward screen and proceed to map",
            prettyOption));
        rootCommand.AddCommand(SimpleCommand.Create("embark", "Start the game from character select", prettyOption));

        // ID-based commands with an optional target
        var targetOption = IdBasedCommand.CreateTargetOption("Target enemy combat ID (for targeted cards)");
        rootCommand.AddCommand(IdBasedCommand.Create(
            "play_card", "Play a card from hand",
            new Argument<string>("card_id", "Card ID (e.g., STRIKE_IRONCLAD, DEFEND_SILENT)"),
            new Option<int>("--nth", () => 0, "N-th occurrence when multiple copies exist (0-based)"),
            targetOption,
            prettyOption));
        rootCommand.AddCommand(IdBasedCommand.Create(
            "use_potion", "Use a potion",
            new Argument<string>("potion_id", "Potion ID (e.g., FIRE_POTION, ENTROPIC_BREW)"),
            new Option<int>("--nth", () => 0, "N-th occurrence when multiple copies exist (0-based)"),
            targetOption,
            prettyOption));

        // Reward commands
        rootCommand.AddCommand(RewardClaimCommand.Create(
            "reward_claim", "Claim a reward by type (gold, potion, relic, special_card)", prettyOption));
        rootCommand.AddCommand(RewardChooseCardCommand.Create(
            "reward_choose_card", "Pick a card from a card reward", prettyOption));
        rootCommand.AddCommand(RewardSkipCardCommand.Create(
            "reward_skip_card", "Skip a card reward", prettyOption));

        // Event commands
        rootCommand.AddCommand(IndexedCommand.Create(
            "choose_event", "Choose an option in an event room",
            new Argument<int>("index", "Option index (0-based)"),
            prettyOption));
        rootCommand.AddCommand(AdvanceDialogueCommand.Create(
            "advance_dialogue", "Advance dialogue in an Ancient event", prettyOption));

        // Selection commands
        rootCommand.AddCommand(PotionSelectCardCommand.Create(prettyOption));
        rootCommand.AddCommand(DeckSelectCardCommand.Create(prettyOption));
        rootCommand.AddCommand(DeckSelectSkipCommand.Create(prettyOption));

        // Character select commands
        rootCommand.AddCommand(SelectCharacterCommand.Create(prettyOption));
        rootCommand.AddCommand(SetAscensionCommand.Create(prettyOption));

        return await rootCommand.InvokeAsync(args);
    }
}