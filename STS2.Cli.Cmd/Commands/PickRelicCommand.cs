using System.CommandLine;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the pick_relic command for picking a relic from the treasure room.
/// </summary>
internal static class PickRelicCommand
{
    /// <summary>
    ///     Creates the pick_relic command.
    /// </summary>
    public static Command Create(Option<bool> prettyOption)
    {
        return IndexedCommand.Create(
            "pick_relic", "Pick a relic from the treasure chest",
            new Argument<int>("index") { Description = "Relic index (0-based)" },
            prettyOption);
    }
}
