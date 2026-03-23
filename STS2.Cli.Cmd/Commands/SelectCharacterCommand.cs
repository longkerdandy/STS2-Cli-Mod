using System.CommandLine;
using STS2.Cli.Cmd.Models.Message;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the select_character command.
/// </summary>
internal static class SelectCharacterCommand
{
    /// <summary>
    ///     Creates the select_character command.
    /// </summary>
    public static Command Create(Option<bool> prettyOption)
    {
        var command = new Command("select_character", "Select a character on the character select screen");
        var characterIdArg = new Argument<string>("character_id", "Character identifier (e.g., ironclad, silent)");
        command.AddArgument(characterIdArg);

        command.SetHandler(async context =>
        {
            var characterId = context.ParseResult.GetValueForArgument(characterIdArg);
            var pretty = context.ParseResult.GetValueForOption(prettyOption);

            context.ExitCode = await CommandExecutor.ExecuteAsync(
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
