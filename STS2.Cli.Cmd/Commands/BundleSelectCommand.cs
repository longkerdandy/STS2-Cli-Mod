using System.CommandLine;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the bundle_select command for previewing a bundle on the bundle selection screen.
/// </summary>
internal static class BundleSelectCommand
{
    /// <summary>
    ///     Creates the bundle_select command.
    /// </summary>
    public static Command Create()
    {
        return IndexedCommand.Create(
            "bundle_select", "Preview a bundle on the bundle selection screen (Scroll Boxes relic)",
            new Argument<int>("index") { Description = "Bundle index (0-based)" });
    }
}
