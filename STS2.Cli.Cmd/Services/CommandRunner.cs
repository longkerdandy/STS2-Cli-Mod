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
    ///     Executes a command with ID-based parameters and handles the full lifecycle.
    /// </summary>
    /// <param name="cmd">Command name to execute (e.g., "play_card", "use_potion")</param>
    /// <param name="id">Card or potion ID</param>
    /// <param name="nth">N-th occurrence when multiple copies exist (0-based)</param>
    /// <param name="target">Optional target combat ID</param>
    /// <param name="pretty">Whether to format JSON output with indentation</param>
    /// <param name="timeoutMs">Timeout in milliseconds</param>
    public static async Task<int> ExecuteAsync(
        string cmd,
        string id,
        int nth = 0,
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

        // Send command with ID-based parameters
        var response = await client.SendCommandAsync(cmd, id, nth, target);

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
    ///     Executes a reward command with type, ID, and nth parameters.
    /// </summary>
    public static async Task<int> ExecuteRewardAsync(
        string cmd,
        string rewardType,
        string? itemId,
        int nth,
        bool pretty = false,
        int timeoutMs = 5000)
    {
        using var client = new PipeClient();
        var jsonOptions = pretty ? JsonOptions.Pretty : JsonOptions.Default;

        if (!await client.ConnectAsync(timeoutMs))
        {
            WriteError("CONNECTION_ERROR", "Game not running or mod not loaded", jsonOptions);
            return ExitConnectionError;
        }

        var response = await client.SendRewardCommandAsync(cmd, rewardType, itemId, nth);

        if (response == null)
        {
            WriteError("CONNECTION_ERROR", "Failed to communicate with mod", jsonOptions);
            return ExitConnectionError;
        }

        if (response.Ok)
        {
            WriteSuccess(response.Data, jsonOptions);
            return ExitSuccess;
        }

        WriteError(response.Error ?? "UNKNOWN_ERROR", response.Message ?? "Unknown error", jsonOptions);
        return MapErrorToExitCode(response.Error);
    }

    /// <summary>
    ///     Executes a choose_card command with type, card_id, and nth parameters.
    /// </summary>
    public static async Task<int> ExecuteChooseCardAsync(
        string rewardType,
        string cardId,
        int nth,
        bool pretty = false,
        int timeoutMs = 5000)
    {
        using var client = new PipeClient();
        var jsonOptions = pretty ? JsonOptions.Pretty : JsonOptions.Default;

        if (!await client.ConnectAsync(timeoutMs))
        {
            WriteError("CONNECTION_ERROR", "Game not running or mod not loaded", jsonOptions);
            return ExitConnectionError;
        }

        var response = await client.SendChooseCardCommandAsync(rewardType, cardId, nth);

        if (response == null)
        {
            WriteError("CONNECTION_ERROR", "Failed to communicate with mod", jsonOptions);
            return ExitConnectionError;
        }

        if (response.Ok)
        {
            WriteSuccess(response.Data, jsonOptions);
            return ExitSuccess;
        }

        WriteError(response.Error ?? "UNKNOWN_ERROR", response.Message ?? "Unknown error", jsonOptions);
        return MapErrorToExitCode(response.Error);
    }

    /// <summary>
    ///     Executes a skip_card command with type and nth parameters.
    /// </summary>
    public static async Task<int> ExecuteSkipCardAsync(
        string rewardType,
        int nth,
        bool pretty = false,
        int timeoutMs = 5000)
    {
        using var client = new PipeClient();
        var jsonOptions = pretty ? JsonOptions.Pretty : JsonOptions.Default;

        if (!await client.ConnectAsync(timeoutMs))
        {
            WriteError("CONNECTION_ERROR", "Game not running or mod not loaded", jsonOptions);
            return ExitConnectionError;
        }

        var response = await client.SendSkipCardCommandAsync(rewardType, nth);

        if (response == null)
        {
            WriteError("CONNECTION_ERROR", "Failed to communicate with mod", jsonOptions);
            return ExitConnectionError;
        }

        if (response.Ok)
        {
            WriteSuccess(response.Data, jsonOptions);
            return ExitSuccess;
        }

        WriteError(response.Error ?? "UNKNOWN_ERROR", response.Message ?? "Unknown error", jsonOptions);
        return MapErrorToExitCode(response.Error);
    }

    /// <summary>
    ///     Executes an advance_dialogue command for Ancient events.
    /// </summary>
    public static async Task<int> ExecuteAdvanceDialogueAsync(
        bool auto,
        bool pretty = false,
        int timeoutMs = 5000)
    {
        using var client = new PipeClient();
        var jsonOptions = pretty ? JsonOptions.Pretty : JsonOptions.Default;

        if (!await client.ConnectAsync(timeoutMs))
        {
            WriteError("CONNECTION_ERROR", "Game not running or mod not loaded", jsonOptions);
            return ExitConnectionError;
        }

        var response = await client.SendAdvanceDialogueCommandAsync(auto);

        if (response == null)
        {
            WriteError("CONNECTION_ERROR", "Failed to communicate with mod", jsonOptions);
            return ExitConnectionError;
        }

        if (response.Ok)
        {
            WriteSuccess(response.Data, jsonOptions);
            return ExitSuccess;
        }

        WriteError(response.Error ?? "UNKNOWN_ERROR", response.Message ?? "Unknown error", jsonOptions);
        return MapErrorToExitCode(response.Error);
    }

    /// <summary>
    ///     Directly outputs an error and returns exit code (for parameter validation failures).
    /// </summary>
    public static async Task<int> ExecuteAsync(
        string cmd,
        string error,
        string message,
        bool pretty = false)
    {
        var jsonOptions = pretty ? JsonOptions.Pretty : JsonOptions.Default;
        WriteError(error, message, jsonOptions);
        return MapErrorToExitCode(error);
    }

    /// <summary>
    ///     Executes a potion_select_card command with card IDs and optional nth values.
    /// </summary>
    public static async Task<int> ExecutePotionSelectCardAsync(
        string[] cardIds,
        int[]? nthValues,
        bool pretty = false,
        int timeoutMs = 5000)
    {
        using var client = new PipeClient();
        var jsonOptions = pretty ? JsonOptions.Pretty : JsonOptions.Default;

        if (!await client.ConnectAsync(timeoutMs))
        {
            WriteError("CONNECTION_ERROR", "Game not running or mod not loaded", jsonOptions);
            return ExitConnectionError;
        }

        var response = await client.SendPotionSelectCardCommandAsync(cardIds, nthValues);

        if (response == null)
        {
            WriteError("CONNECTION_ERROR", "Failed to communicate with mod", jsonOptions);
            return ExitConnectionError;
        }

        if (response.Ok)
        {
            WriteSuccess(response.Data, jsonOptions);
            return ExitSuccess;
        }

        WriteError(response.Error ?? "UNKNOWN_ERROR", response.Message ?? "Unknown error", jsonOptions);
        return MapErrorToExitCode(response.Error);
    }

    /// <summary>
    ///     Executes a potion_select_skip command.
    /// </summary>
    public static async Task<int> ExecutePotionSelectSkipAsync(
        bool pretty = false,
        int timeoutMs = 5000)
    {
        using var client = new PipeClient();
        var jsonOptions = pretty ? JsonOptions.Pretty : JsonOptions.Default;

        if (!await client.ConnectAsync(timeoutMs))
        {
            WriteError("CONNECTION_ERROR", "Game not running or mod not loaded", jsonOptions);
            return ExitConnectionError;
        }

        var response = await client.SendPotionSelectSkipCommandAsync();

        if (response == null)
        {
            WriteError("CONNECTION_ERROR", "Failed to communicate with mod", jsonOptions);
            return ExitConnectionError;
        }

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
"NOT_SUPPORTED" or "CLAIM_FAILED" or
"NOT_IN_EVENT" or "NO_EVENT_LAYOUT" or
"OPTION_LOCKED" or "OPTION_BUTTON_NOT_FOUND" or
"NOT_ANCIENT_EVENT" or "NOT_IN_DIALOGUE" or "DIALOGUE_HITBOX_NOT_FOUND" or
"NOT_IN_POTION_SELECTION" or "CANNOT_SKIP" or
"SKIP_BUTTON_NOT_FOUND" or "NO_CARDS_AVAILABLE" => ExitInvalidState,

        // Invalid parameter — caller provided wrong arguments
        "INVALID_REQUEST" or "UNKNOWN_COMMAND" or "MISSING_ARGUMENT" or
        "INVALID_CARD_INDEX" or "INVALID_OPTION_INDEX" or
        "TARGET_REQUIRED" or "TARGET_NOT_FOUND" or
        "TARGET_NOT_ALLOWED" or "INVALID_POTION_SLOT" or
        "EMPTY_POTION_SLOT" or "INVALID_REWARD_INDEX" or
        "NOT_CARD_REWARD" or "USE_CHOOSE_CARD" or
        "CARD_NOT_FOUND" or "POTION_NOT_FOUND" or "AMBIGUOUS_ID" or
        "INVALID_REWARD_TYPE" or "REWARD_NOT_FOUND" or "ID_MISMATCH" or
        "AMBIGUOUS_REWARD" or "INVALID_SELECTION_COUNT" or
        "DUPLICATE_SELECTION" => ExitInvalidParam,

        // Timeout
        "TIMEOUT" or "EVENT_TIMEOUT" => ExitTimeout,

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
