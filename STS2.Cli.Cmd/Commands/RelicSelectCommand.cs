using System.CommandLine;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the relic_select command for selecting a relic from the boss/event relic selection screen.
/// </summary>
internal static class RelicSelectCommand
{
    /// <summary>
    ///     Creates the relic_select command.
    /// </summary>
    public static Command Create()
    {
        return IndexedCommand.Create(
            "relic_select", "Select a relic from the boss/event relic selection screen",
            new Argument<int>("index") { Description = "Relic index (0-based)" });
    }
}
