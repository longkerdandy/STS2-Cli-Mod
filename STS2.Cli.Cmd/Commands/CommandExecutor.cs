using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using STS2.Cli.Cmd.Client;
using STS2.Cli.Cmd.Models.Messages;
using STS2.Cli.Cmd.Utils;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Provides common execution logic for all commands.
/// </summary>
internal static class CommandExecutor
{
    // Exit codes following CLI specification (AGENTS.md)
    private const int ExitSuccess = 0;
    private const int ExitConnectionError = 1;
    private const int ExitInvalidState = 2;
    private const int ExitInvalidParam = 3;
    private const int ExitTimeout = 4;

    /// <summary>
    ///     Global --pretty option shared by all commands.
    ///     Registered as a global option on the root command so subcommands inherit it automatically.
    /// </summary>
    internal static readonly Option<bool> PrettyOption = new("--pretty", "-p")
    {
        Description = "Format JSON output with indentation for readability",
        DefaultValueFactory = _ => false
    };

    /// <summary>
    ///     Resolves the pretty flag from a <see cref="ParseResult" />.
    /// </summary>
    internal static bool IsPretty(ParseResult parseResult)
    {
        return parseResult.GetValue(PrettyOption);
    }

    /// <summary>
    ///     Executes a command with the given request factory and returns exit code.
    /// </summary>
    public static async Task<int> ExecuteAsync(
        Func<Request> requestFactory,
        bool pretty = false,
        int timeoutMs = 5000)
    {
        using var client = new PipeClient();
        var jsonOptions = pretty ? JsonOptions.Pretty : JsonOptions.Default;

        // Try to connect
        if (!await client.ConnectAsync(timeoutMs))
        {
            WriteError("CONNECTION_ERROR", "Game not running or mod not loaded", jsonOptions);
            return ExitConnectionError;
        }

        // Build request and send
        var request = requestFactory();
        var response = await client.SendAsync(request);

        return HandleResponse(response, jsonOptions);
    }

    /// <summary>
    ///     Executes a command that returns an error without making a request (for validation failures).
    /// </summary>
    public static Task<int> ExecuteErrorAsync(
        string error,
        string message,
        bool pretty = false)
    {
        var jsonOptions = pretty ? JsonOptions.Pretty : JsonOptions.Default;
        WriteError(error, message, jsonOptions);
        return Task.FromResult(MapErrorToExitCode(error));
    }

    /// <summary>
    ///     Handles the response from the mod and outputs the result.
    /// </summary>
    private static int HandleResponse(Response? response, JsonSerializerOptions jsonOptions)
    {
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
    private static int MapErrorToExitCode(string? error)
    {
        return error switch
        {
            // Invalid state — combat phase, creature state, or screen state prevents the action
            "NOT_IN_COMBAT" or "COMBAT_ENDING" or "NOT_PLAYER_TURN" or
                "ACTIONS_DISABLED" or "NO_PLAYER" or "PLAYER_DEAD" or
                "CANNOT_PLAY_CARD" or "ACTION_CANCELLED" or
                "POTION_ALREADY_QUEUED" or "POTION_NOT_USABLE" or
                "NO_PROCEED_AVAILABLE" or "UNSUPPORTED_EVENT" or "PROCEED_BUTTON_NOT_FOUND" or "PROCEED_NOT_VISIBLE" or "PROCEED_NOT_ENABLED" or "POTION_BELT_FULL" or
                "NOT_SUPPORTED" or "CLAIM_FAILED" or
                "NOT_IN_EVENT" or "NO_EVENT_LAYOUT" or
                "OPTION_LOCKED" or "OPTION_BUTTON_NOT_FOUND" or
                "NOT_ANCIENT_EVENT" or "NOT_IN_DIALOGUE" or "DIALOGUE_HITBOX_NOT_FOUND" or
                "NOT_IN_POTION_SELECTION" or "CANNOT_SKIP" or
                "SKIP_BUTTON_NOT_FOUND" or "NO_CARDS_AVAILABLE" or
                "NOT_IN_CHARACTER_SELECT" or "CHARACTER_LOCKED" or
                "NO_CHARACTER_SELECTED" or "EMBARK_BUTTON_NOT_FOUND" or "EMBARK_NOT_AVAILABLE" or
                "NOT_IN_DECK_CARD_SELECT" or
                "NOT_IN_HAND_SELECT" or "CANNOT_CONFIRM" or
                "NOT_ON_MAP" or "NO_RUN_STATE" or "NOT_TRAVELABLE" or
                "TRAVEL_DISABLED" or
                "NOT_AT_REST_SITE" or "OPTION_DISABLED" or "OPTION_CANCELLED" or
                "NOT_IN_TREASURE_ROOM" or "CHEST_ALREADY_OPENED" or "NO_RELICS_AVAILABLE" or
                "NOT_IN_SHOP" or "ITEM_SOLD_OUT" or "NOT_ENOUGH_GOLD" or
                "CARD_REMOVAL_USED" or "PURCHASE_FAILED" or "POTION_BELT_FULL" => ExitInvalidState,

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
                "DUPLICATE_SELECTION" or "CHARACTER_NOT_FOUND" or
                "INVALID_ASCENSION_LEVEL" or "UI_NOT_FOUND" or
                "NODE_NOT_FOUND" or "OPTION_NOT_FOUND" or "INVALID_RELIC_INDEX" or
                "ITEM_NOT_FOUND" => ExitInvalidParam,

            // Timeout
            "TIMEOUT" or "EVENT_TIMEOUT" => ExitTimeout,

            // Internal errors, state extraction failures, and unknown errors
            _ => ExitConnectionError
        };
    }

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