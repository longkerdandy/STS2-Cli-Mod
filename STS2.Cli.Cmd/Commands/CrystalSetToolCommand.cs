using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the crystal_set_tool command for switching the divination tool
///     in the Crystal Sphere mini-game.
/// </summary>
internal static class CrystalSetToolCommand
{
    /// <summary>
    ///     Creates the crystal_set_tool subcommand.
    /// </summary>
    public static Command Create()
    {
        var toolArg = new Argument<string>("tool")
        {
            Description = "Tool type: 'big' (3x3 area) or 'small' (1 cell)"
        };

        var command = new Command("crystal_set_tool",
            "Set the divination tool in the Crystal Sphere mini-game");
        command.Arguments.Add(toolArg);

        command.SetAction(parseResult =>
        {
            var tool = parseResult.GetValue(toolArg)!;
            var pretty = CommandExecutor.IsPretty(parseResult);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "crystal_set_tool",
                    Id = tool
                },
                pretty);
        });

        return command;
    }
}
