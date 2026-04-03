using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the choose_game_mode command for selecting a game mode from the singleplayer submenu.
/// </summary>
internal static class ChooseGameModeCommand
{
    /// <summary>
    ///     Creates the choose_game_mode command.
    /// </summary>
    public static Command Create()
    {
        var command = new Command("choose_game_mode",
            "Select a game mode from the singleplayer submenu (standard, daily, custom)");
        var modeArg = new Argument<string>("mode")
        {
            Description = "Game mode to select (standard, daily, custom)"
        };
        command.Arguments.Add(modeArg);

        command.SetAction(parseResult =>
        {
            var mode = parseResult.GetValue(modeArg)!;
            var pretty = CommandExecutor.IsPretty(parseResult);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "choose_game_mode",
                    Id = mode
                },
                pretty);
        });

        return command;
    }
}
