using STS2.Cli.Mod.Models.Messages;
using STS2.Cli.Mod.State;
using STS2.Cli.Mod.Utils;

namespace STS2.Cli.Mod.Actions;

/// <summary>
///     Handles the 'state' command by extracting the current game state.
/// </summary>
public static class StateHandler
{
    private static readonly ModLogger Logger = new("StateHandler");

    /// <summary>
    ///     Handles the state request by delegating to <see cref="GameStateExtractor.GetState" />.
    /// </summary>
    /// <param name="request">The parsed request object.</param>
    /// <returns>Response containing the game state DTO, or an error if extraction failed.</returns>
    public static object HandleRequest(Request request)
    {
        Logger.Info("Requested game state");
        return Execute();
    }

    /// <summary>
    ///     Extracts the current game state and wraps it in a standard response envelope.
    /// </summary>
    /// <returns>Response object with <c>ok</c> and <c>data</c> or <c>error</c> fields.</returns>
    private static object Execute()
    {
        var state = GameStateExtractor.GetState();

        if (state.Error != null)
            return new { ok = false, error = "STATE_EXTRACTION_ERROR", message = state.Error };

        return new { ok = true, data = state };
    }
}
