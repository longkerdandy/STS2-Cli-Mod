using System.Text.Json;

namespace STS2.Cli.Cmd.Services;

/// <summary>
/// Handles command execution and output formatting.
/// </summary>
public static class CommandRunner
{
    /// <summary>
    /// Exit codes following CLI specification.
    /// </summary>
    private static class ExitCodes
    {
        public const int Success = 0;
        public const int ConnectionError = 1;
        public const int InvalidState = 2;
        public const int InvalidParameter = 3;
        public const int Timeout = 4;
    }

    /// <summary>
    /// Executes a command and handles the full lifecycle.
    /// </summary>
    public static async Task<int> ExecuteAsync(
        string cmd, 
        int[]? args = null, 
        string? target = null,
        int timeoutMs = 5000)
    {
        using var client = new PipeClient();
        
        // Try to connect
        if (!await client.ConnectAsync(timeoutMs))
        {
            WriteError("CONNECTION_ERROR", "Game not running or mod not loaded");
            return ExitCodes.ConnectionError;
        }

        // Send command
        var response = await client.SendCommandAsync(cmd, args, target);
        if (response == null)
        {
            WriteError("CONNECTION_ERROR", "Failed to communicate with mod");
            return ExitCodes.ConnectionError;
        }

        // Output result
        if (response.Ok)
        {
            WriteSuccess(response.Data);
            return ExitCodes.Success;
        }
        else
        {
            WriteError(response.Error ?? "UNKNOWN_ERROR", response.Message ?? "Unknown error");
            
            // Map error to exit code
            return (response.Error?.ToUpper()) switch
            {
                "INVALID_STATE" => ExitCodes.InvalidState,
                "INVALID_PARAM" or "MISSING_ARGUMENT" => ExitCodes.InvalidParameter,
                "TIMEOUT" => ExitCodes.Timeout,
                _ => ExitCodes.ConnectionError
            };
        }
    }

    private static void WriteSuccess(object? data)
    {
        var response = new { ok = true, data };
        Console.WriteLine(JsonSerializer.Serialize(response));
    }

    private static void WriteError(string error, string message)
    {
        var response = new { ok = false, error, message };
        Console.Error.WriteLine(JsonSerializer.Serialize(response));
    }
}
