using System.Text.Json;
using STS2.Cli.Cmd.Utils;

namespace STS2.Cli.Cmd.Services;

/// <summary>
///     Handles command execution and output formatting.
/// </summary>
public static class CommandRunner
{
    // Exit codes following CLI specification (AGENTS.md)
    private const int ExitSuccess = 0;
    private const int ExitConnectionError = 1;
    private const int ExitInvalidState = 2;
    private const int ExitInvalidParam = 3;
    private const int ExitTimeout = 4;

    /// <summary>
    ///     Executes a command and handles the full lifecycle.
    /// </summary>
    /// <param name="cmd">Command name to execute</param>
    /// <param name="args">Optional integer arguments</param>
    /// <param name="target">Optional target combat ID</param>
    /// <param name="pretty">Whether to format JSON output with indentation</param>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    public static async Task<int> ExecuteAsync(
        string cmd,
        int[]? args = null,
        int? target = null,
        bool pretty = false,
        int timeoutMs = 5000)
    {
        using var client = new PipeClient();

        // Select JSON options based on the pretty flag
        var jsonOptions = pretty ? JsonOptions.Pretty : JsonOptions.Default;

        // Try to connect
        if (!await client.ConnectAsync(timeoutMs))
        {
            WriteError("CONNECTION_ERROR", "Game not running or mod not loaded", jsonOptions);
            return ExitConnectionError;
        }

        // Send command
        var response = await client.SendCommandAsync(cmd, args, target);
        if (response == null)
        {
            WriteError("CONNECTION_ERROR", "Failed to communicate with mod", jsonOptions);
            return ExitConnectionError;
        }

        // Output result
        if (response.Ok)
        {
            WriteSuccess(response.Data, jsonOptions);
            return ExitSuccess;
        }

        WriteError(response.Error ?? "UNKNOWN_ERROR", response.Message ?? "Unknown error", jsonOptions);
        return MapErrorToExitCode(response.Error);
    }

    /// <summary>
    ///     Maps a Mod error code to a CLI exit code.
    /// </summary>
    private static int MapErrorToExitCode(string? error) => error switch
    {
        // Invalid state — combat phase, creature state, or screen state prevents the action
        "NOT_IN_COMBAT" or "COMBAT_ENDING" or "NOT_PLAYER_TURN" or
        "ACTIONS_DISABLED" or "NO_PLAYER" or "PLAYER_DEAD" or
        "CANNOT_PLAY_CARD" or "ACTION_CANCELLED" or
        "POTION_ALREADY_QUEUED" or "POTION_NOT_USABLE" or
        "NOT_ON_REWARD_SCREEN" or "POTION_BELT_FULL" or
        "NOT_SUPPORTED" or "CLAIM_FAILED" => ExitInvalidState,

        // Invalid parameter — caller provided wrong arguments
        "INVALID_REQUEST" or "UNKNOWN_COMMAND" or "MISSING_ARGUMENT" or
        "INVALID_CARD_INDEX" or "TARGET_REQUIRED" or "TARGET_NOT_FOUND" or
        "TARGET_NOT_ALLOWED" or "INVALID_POTION_SLOT" or
        "EMPTY_POTION_SLOT" or "INVALID_REWARD_INDEX" or
        "NOT_CARD_REWARD" or "USE_CHOOSE_CARD" => ExitInvalidParam,

        // Timeout
        "TIMEOUT" => ExitTimeout,

        // Internal errors, state extraction failures, and unknown errors
        _ => ExitConnectionError
    };

    private static void WriteSuccess(object? data, JsonSerializerOptions options)
    {
        var response = new { ok = true, data };
        Console.WriteLine(JsonSerializer.Serialize(response, options));
    }

    private static void WriteError(string error, string message, JsonSerializerOptions options)
    {
        var response = new { ok = false, error, message };
        Console.Error.WriteLine(JsonSerializer.Serialize(response, options));
    }
}