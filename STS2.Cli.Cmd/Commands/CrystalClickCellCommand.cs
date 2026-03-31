using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the crystal_click_cell command for clicking a grid cell
///     in the Crystal Sphere mini-game.
/// </summary>
internal static class CrystalClickCellCommand
{
    /// <summary>
    ///     Creates the crystal_click_cell subcommand.
    /// </summary>
    public static Command Create()
    {
        var xArg = new Argument<int>("x") { Description = "Cell X coordinate (0-based, range 0..10)" };
        var yArg = new Argument<int>("y") { Description = "Cell Y coordinate (0-based, range 0..10)" };

        var command = new Command("crystal_click_cell",
            "Click a cell in the Crystal Sphere mini-game to clear fog");
        command.Arguments.Add(xArg);
        command.Arguments.Add(yArg);

        command.SetAction(parseResult =>
        {
            var x = parseResult.GetValue(xArg);
            var y = parseResult.GetValue(yArg);
            var pretty = CommandExecutor.IsPretty(parseResult);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "crystal_click_cell",
                    Args = [x, y]
                },
                pretty);
        });

        return command;
    }
}
