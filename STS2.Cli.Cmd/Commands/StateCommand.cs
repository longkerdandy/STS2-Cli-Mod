using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     State command with optional --include-pile-details flag.
/// </summary>
internal static class StateCommand
{
    /// <summary>
    ///     Creates the state command.
    /// </summary>
    public static Command Create()
    {
        var command = new Command("state", "Get current game state");

        var includePileDetailsOption = new Option<bool>("--include-pile-details")
        {
            Description = "Include full card descriptions in draw/discard/exhaust pile listings",
            DefaultValueFactory = _ => false
        };

        command.Options.Add(includePileDetailsOption);

        command.SetAction(parseResult =>
        {
            var pretty = CommandExecutor.IsPretty(parseResult);
            var includePileDetails = parseResult.GetValue(includePileDetailsOption);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "state",
                    IncludePileDetails = includePileDetails
                },
                pretty);
        });

        return command;
    }
}
