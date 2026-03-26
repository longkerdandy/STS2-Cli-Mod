using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the choose_map_node command with col and row arguments.
/// </summary>
internal static class ChooseMapNodeCommand
{
    /// <summary>
    ///     Creates the choose_map_node subcommand.
    /// </summary>
    public static Command Create()
    {
        var colArg = new Argument<int>("col") { Description = "Column index of the target node (0-based)" };
        var rowArg = new Argument<int>("row")
        {
            Description = "Row index of the target node (0 = starting, 1..N = grid, N+1 = boss)"
        };

        var command = new Command("choose_map_node", "Select a map node to travel to");
        command.Arguments.Add(colArg);
        command.Arguments.Add(rowArg);

        command.SetAction(parseResult =>
        {
            var col = parseResult.GetValue(colArg);
            var row = parseResult.GetValue(rowArg);
            var pretty = CommandExecutor.IsPretty(parseResult);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "choose_map_node",
                    Args = [col, row]
                },
                pretty);
        });

        return command;
    }
}
