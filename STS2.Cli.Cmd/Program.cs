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

        // Global --pretty option inherited by all subcommands automatically
        rootCommand.Options.Add(CommandExecutor.PrettyOption);

        // Simple commands (no arguments)
        rootCommand.Subcommands.Add(SimpleCommand.Create("ping", "Test connection to the mod"));
        rootCommand.Subcommands.Add(StateCommand.Create());
        rootCommand.Subcommands.Add(SimpleCommand.Create("end_turn", "End the current turn"));
        rootCommand.Subcommands.Add(SimpleCommand.Create("proceed",
            "Leave current screen and proceed to map (reward screen or FakeMerchant event)"));
        rootCommand.Subcommands.Add(SimpleCommand.Create("embark", "Start the game from character select"));

        // ID-based commands with an optional target
        var targetOption = IdBasedCommand.CreateTargetOption("Target enemy combat ID (for targeted cards)");
        rootCommand.Subcommands.Add(IdBasedCommand.Create(
            "play_card", "Play a card from hand",
            new Argument<string>("card_id") { Description = "Card ID (e.g., STRIKE_IRONCLAD, DEFEND_SILENT)" },
            new Option<int>("--nth")
                { Description = "N-th occurrence when multiple copies exist (0-based)", DefaultValueFactory = _ => 0 },
            targetOption));
        rootCommand.Subcommands.Add(IdBasedCommand.Create(
            "use_potion", "Use a potion",
            new Argument<string>("potion_id") { Description = "Potion ID (e.g., FIRE_POTION, ENTROPIC_BREW)" },
            new Option<int>("--nth")
                { Description = "N-th occurrence when multiple copies exist (0-based)", DefaultValueFactory = _ => 0 },
            targetOption));

        // Reward commands
        rootCommand.Subcommands.Add(RewardClaimCommand.Create(
            "reward_claim", "Claim a reward by type (gold, potion, relic, special_card)"));
        rootCommand.Subcommands.Add(RewardChooseCardCommand.Create(
            "reward_choose_card", "Pick a card from a card reward"));
        rootCommand.Subcommands.Add(RewardSkipCardCommand.Create(
            "reward_skip_card", "Skip a card reward"));

        // Event commands
        rootCommand.Subcommands.Add(IndexedCommand.Create(
            "choose_event", "Choose an option in an event room",
            new Argument<int>("index") { Description = "Option index (0-based)" }));
        rootCommand.Subcommands.Add(AdvanceDialogueCommand.Create(
            "advance_dialogue", "Advance dialogue in an Ancient event"));

        // Selection commands
        rootCommand.Subcommands.Add(TriSelectCardCommand.Create());
        rootCommand.Subcommands.Add(TriSelectSkipCommand.Create());
        rootCommand.Subcommands.Add(GridSelectCardCommand.Create());
        rootCommand.Subcommands.Add(GridSelectSkipCommand.Create());
        rootCommand.Subcommands.Add(HandSelectCardCommand.Create());
        rootCommand.Subcommands.Add(HandConfirmSelectionCommand.Create());

        // Character select commands
        rootCommand.Subcommands.Add(SelectCharacterCommand.Create());
        rootCommand.Subcommands.Add(SetAscensionCommand.Create());

        // Map commands
        rootCommand.Subcommands.Add(ChooseMapNodeCommand.Create());

        // Rest site commands
        rootCommand.Subcommands.Add(ChooseRestOptionCommand.Create());

        // Treasure room commands
        rootCommand.Subcommands.Add(OpenChestCommand.Create());
        rootCommand.Subcommands.Add(PickRelicCommand.Create());

        // Relic selection commands (boss/event relic choice)
        rootCommand.Subcommands.Add(RelicSelectCommand.Create());
        rootCommand.Subcommands.Add(SimpleCommand.Create("relic_skip",
            "Skip relic selection on the boss/event relic selection screen"));

        // Bundle selection commands (Scroll Boxes relic)
        rootCommand.Subcommands.Add(BundleSelectCommand.Create());
        rootCommand.Subcommands.Add(SimpleCommand.Create("bundle_confirm",
            "Confirm the previewed bundle selection"));
        rootCommand.Subcommands.Add(SimpleCommand.Create("bundle_cancel",
            "Cancel bundle preview and return to selection"));

        // Shop commands
        rootCommand.Subcommands.Add(ShopBuyCardCommand.Create());
        rootCommand.Subcommands.Add(ShopBuyRelicCommand.Create());
        rootCommand.Subcommands.Add(ShopBuyPotionCommand.Create());
        rootCommand.Subcommands.Add(ShopRemoveCardCommand.Create());

        // Crystal Sphere mini-game commands
        rootCommand.Subcommands.Add(CrystalSetToolCommand.Create());
        rootCommand.Subcommands.Add(CrystalClickCellCommand.Create());
        rootCommand.Subcommands.Add(SimpleCommand.Create("crystal_proceed",
            "Leave the Crystal Sphere mini-game after divinations are complete"));

        return rootCommand.Parse(args).Invoke();
    }
}
