using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the choose_rest_option command for selecting a rest site (campfire) option.
/// </summary>
internal static class ChooseRestOptionCommand
{
    /// <summary>
    ///     Creates the choose_rest_option command.
    /// </summary>
    public static Command Create()
    {
        var command = new Command("choose_rest_option", "Choose a rest site option (e.g., HEAL, SMITH)");
        var optionIdArg = new Argument<string>("option_id")
        {
            Description = "Rest site option ID (e.g., HEAL, SMITH, MEND, LIFT, DIG, HATCH, COOK, CLONE)"
        };
        command.Arguments.Add(optionIdArg);

        command.SetAction(parseResult =>
        {
            var optionId = parseResult.GetValue(optionIdArg)!;
            var pretty = CommandExecutor.IsPretty(parseResult);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "choose_rest_option",
                    Id = optionId
                },
                pretty);
        });

        return command;
    }
}
