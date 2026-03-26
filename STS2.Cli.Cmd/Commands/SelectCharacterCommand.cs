using System.CommandLine;
using STS2.Cli.Cmd.Models.Messages;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the select_character command.
/// </summary>
internal static class SelectCharacterCommand
{
    /// <summary>
    ///     Creates the select_character command.
    /// </summary>
    public static Command Create()
    {
        var command = new Command("select_character", "Select a character on the character select screen");
        var characterIdArg = new Argument<string>("character_id")
        {
            Description = "Character identifier (e.g., ironclad, silent)"
        };
        command.Arguments.Add(characterIdArg);

        command.SetAction(parseResult =>
        {
            var characterId = parseResult.GetValue(characterIdArg)!;
            var pretty = CommandExecutor.IsPretty(parseResult);

            return CommandExecutor.ExecuteAsync(
                () => new Request
                {
                    Cmd = "select_character",
                    Id = characterId
                },
                pretty);
        });

        return command;
    }
}
