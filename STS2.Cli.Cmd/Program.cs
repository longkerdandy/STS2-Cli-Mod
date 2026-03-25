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
    private static int Main(string[] args)
    {
        // Force UTF-8 encoding for proper Chinese character display in WSL
        Console.OutputEncoding = Encoding.UTF8;

        var rootCommand = new RootCommand("STS2 CLI - Control Slay the Spire 2 via command line");

        // Global --pretty option for formatted JSON output
        var prettyOption = new Option<bool>("--pretty", "-p")
        {
            Description = "Format JSON output with indentation for readability",
            DefaultValueFactory = _ => false
        };
        rootCommand.Options.Add(prettyOption);

        // Simple commands (no arguments)
        rootCommand.Subcommands.Add(SimpleCommand.Create("ping", "Test connection to the mod", prettyOption));
        rootCommand.Subcommands.Add(SimpleCommand.Create("state", "Get current game state", prettyOption));
        rootCommand.Subcommands.Add(SimpleCommand.Create("end_turn", "End the current turn", prettyOption));
        rootCommand.Subcommands.Add(SimpleCommand.Create("proceed", "Leave current screen and proceed to map (reward screen or FakeMerchant event)",
            prettyOption));
        rootCommand.Subcommands.Add(
            SimpleCommand.Create("embark", "Start the game from character select", prettyOption));

        // ID-based commands with an optional target
        var targetOption = IdBasedCommand.CreateTargetOption("Target enemy combat ID (for targeted cards)");
        rootCommand.Subcommands.Add(IdBasedCommand.Create(
            "play_card", "Play a card from hand",
            new Argument<string>("card_id") { Description = "Card ID (e.g., STRIKE_IRONCLAD, DEFEND_SILENT)" },
            new Option<int>("--nth")
                { Description = "N-th occurrence when multiple copies exist (0-based)", DefaultValueFactory = _ => 0 },
            targetOption,
            prettyOption));
        rootCommand.Subcommands.Add(IdBasedCommand.Create(
            "use_potion", "Use a potion",
            new Argument<string>("potion_id") { Description = "Potion ID (e.g., FIRE_POTION, ENTROPIC_BREW)" },
            new Option<int>("--nth")
                { Description = "N-th occurrence when multiple copies exist (0-based)", DefaultValueFactory = _ => 0 },
            targetOption,
            prettyOption));

        // Reward commands
        rootCommand.Subcommands.Add(RewardClaimCommand.Create(
            "reward_claim", "Claim a reward by type (gold, potion, relic, special_card)", prettyOption));
        rootCommand.Subcommands.Add(RewardChooseCardCommand.Create(
            "reward_choose_card", "Pick a card from a card reward", prettyOption));
        rootCommand.Subcommands.Add(RewardSkipCardCommand.Create(
            "reward_skip_card", "Skip a card reward", prettyOption));

        // Event commands
        rootCommand.Subcommands.Add(IndexedCommand.Create(
            "choose_event", "Choose an option in an event room",
            new Argument<int>("index") { Description = "Option index (0-based)" },
            prettyOption));
        rootCommand.Subcommands.Add(AdvanceDialogueCommand.Create(
            "advance_dialogue", "Advance dialogue in an Ancient event", prettyOption));

        // Selection commands
        rootCommand.Subcommands.Add(PotionSelectCardCommand.Create(prettyOption));
        rootCommand.Subcommands.Add(DeckSelectCardCommand.Create(prettyOption));
        rootCommand.Subcommands.Add(DeckSelectSkipCommand.Create(prettyOption));

        // Character select commands
        rootCommand.Subcommands.Add(SelectCharacterCommand.Create(prettyOption));
        rootCommand.Subcommands.Add(SetAscensionCommand.Create(prettyOption));

        // Map commands
        rootCommand.Subcommands.Add(ChooseMapNodeCommand.Create(prettyOption));

        // Rest site commands
        rootCommand.Subcommands.Add(ChooseRestOptionCommand.Create(prettyOption));

        return rootCommand.Parse(args).Invoke();
    }
}