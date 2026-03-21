using System.Text.Json;
using STS2.Cli.Cmd.Utils;

namespace STS2.Cli.Cmd.Services;

/// <summary>
///     Handles command execution and output formatting.
/// </summary>
public static class CommandRunner
{
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
            return ExitCodes.ConnectionError;
        }

        // Send command
        var response = await client.SendCommandAsync(cmd, args, target);
        if (response == null)
        {
            WriteError("CONNECTION_ERROR", "Failed to communicate with mod", jsonOptions);
            return ExitCodes.ConnectionError;
        }

        // Output result
        if (response.Ok)
        {
            WriteSuccess(response.Data, jsonOptions);
            return ExitCodes.Success;
        }

        WriteError(response.Error ?? "UNKNOWN_ERROR", response.Message ?? "Unknown error", jsonOptions);

        // Map error to exit code
        return response.Error?.ToUpper() switch
        {
            "INVALID_STATE" => ExitCodes.InvalidState,
            "INVALID_PARAM" or "MISSING_ARGUMENT" => ExitCodes.InvalidParameter,
            "TIMEOUT" => ExitCodes.Timeout,
            _ => ExitCodes.ConnectionError
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

    /// <summary>
    ///     Exit codes following CLI specification.
    /// </summary>
    private static class ExitCodes
    {
        public const int Success = 0;
        public const int ConnectionError = 1;
        public const int InvalidState = 2;
        public const int InvalidParameter = 3;
        public const int Timeout = 4;
    }
}