using System.CommandLine;
using System.Text.Json;
using STS2.Cli.Cmd.Client;
using STS2.Cli.Cmd.Models.Messages;
using STS2.Cli.Cmd.Utils;

namespace STS2.Cli.Cmd.Commands;

/// <summary>
///     Creates the report_bug command for AI agents to report bugs with context.
///     This command saves a structured bug report as a local JSON file and optionally
///     captures a game state snapshot if the mod is reachable.
/// </summary>
internal static class ReportBugCommand
{
    /// <summary>
    ///     Directory name for bug reports, created alongside the CLI executable.
    /// </summary>
    private const string BugReportDir = "sts2-cli-bugs";

    /// <summary>
    ///     Timeout for the optional game state snapshot (kept short to avoid blocking the AI).
    /// </summary>
    private const int StateSnapshotTimeoutMs = 2000;

    /// <summary>
    ///     Creates the report_bug command with options for title, description, context, and metadata.
    /// </summary>
    public static Command Create()
    {
        var titleOption = new Option<string>("--title")
        {
            Description = "Short summary of the bug (required)",
            Required = true
        };

        var descriptionOption = new Option<string>("--description")
        {
            Description = "Detailed description of the bug: what happened, what was expected (required)",
            Required = true
        };

        var lastCommandOption = new Option<string>("--last-command")
        {
            Description = "The CLI command that triggered the bug (e.g., \"play_card STRIKE --nth 0\")"
        };

        var lastResponseOption = new Option<string>("--last-response")
        {
            Description = "The JSON response from the last command (copy-paste the stderr/stdout output)"
        };

        var severityOption = new Option<string>("--severity")
        {
            Description = "Bug severity: low, medium, high, critical",
            DefaultValueFactory = _ => "medium",
            CustomParser = result =>
            {
                var value = result.Tokens.Single().Value.ToLower();
                if (value is "low" or "medium" or "high" or "critical")
                    return value;
                result.AddError($"Invalid severity '{value}'. Must be one of: low, medium, high, critical");
                return null!;
            }
        };

        var labelsOption = new Option<string>("--labels")
        {
            Description = "Comma-separated labels for categorization (e.g., \"combat,play_card\")"
        };

        var command = new Command("report_bug",
            "Report a bug encountered during gameplay. Saves a structured report to a local file.");
        command.Options.Add(titleOption);
        command.Options.Add(descriptionOption);
        command.Options.Add(lastCommandOption);
        command.Options.Add(lastResponseOption);
        command.Options.Add(severityOption);
        command.Options.Add(labelsOption);

        command.SetAction(parseResult =>
        {
            var title = parseResult.GetValue(titleOption)!;
            var description = parseResult.GetValue(descriptionOption)!;
            var lastCommand = parseResult.GetValue(lastCommandOption);
            var lastResponse = parseResult.GetValue(lastResponseOption);
            var severity = parseResult.GetValue(severityOption)!;
            var labels = parseResult.GetValue(labelsOption);
            var pretty = CommandExecutor.IsPretty(parseResult);

            return ExecuteReportAsync(title, description, lastCommand, lastResponse, severity, labels, pretty);
        });

        return command;
    }

    /// <summary>
    ///     Executes the bug report: captures optional game state, assembles the report, and writes it to disk.
    /// </summary>
    private static async Task<int> ExecuteReportAsync(
        string title,
        string description,
        string? lastCommand,
        string? lastResponse,
        string severity,
        string? labels,
        bool pretty)
    {
        var jsonOptions = pretty ? JsonOptions.Pretty : JsonOptions.Default;

        // Generate bug ID from timestamp
        var now = DateTimeOffset.UtcNow;
        var bugId = $"BUG-{now:yyyyMMdd}-{now:HHmmss}-{now:fff}";

        // Parse last-response as JSON if valid, otherwise store as raw string
        object? parsedLastResponse = null;
        if (!string.IsNullOrEmpty(lastResponse))
        {
            try
            {
                parsedLastResponse = JsonDocument.Parse(lastResponse).RootElement.Clone();
            }
            catch (JsonException)
            {
                parsedLastResponse = lastResponse;
            }
        }

        // Parse labels into array
        string[]? labelArray = null;
        if (!string.IsNullOrEmpty(labels))
        {
            labelArray = labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // Try to capture game state snapshot (best-effort, don't fail if unavailable)
        object? gameStateSnapshot = await CaptureGameStateAsync();

        // Build the bug report object
        var bugReport = new
        {
            bug_id = bugId,
            timestamp = now,
            title,
            description,
            severity,
            labels = labelArray,
            last_command = lastCommand,
            last_response = parsedLastResponse,
            game_state_snapshot = gameStateSnapshot
        };

        // Determine output directory (alongside the CLI executable)
        var exeDir = AppContext.BaseDirectory;
        var bugDir = Path.Combine(exeDir, BugReportDir);

        try
        {
            Directory.CreateDirectory(bugDir);
        }
        catch (Exception ex)
        {
            WriteError("WRITE_FAILED", $"Cannot create bug report directory: {ex.Message}", jsonOptions);
            return 1;
        }

        // Write the bug report file
        var fileName = $"{bugId}.json";
        var filePath = Path.Combine(bugDir, fileName);

        try
        {
            // Always pretty-print the report file itself for human readability
            var reportJson = JsonSerializer.Serialize(bugReport, JsonOptions.Pretty);
            await File.WriteAllTextAsync(filePath, reportJson);
        }
        catch (Exception ex)
        {
            WriteError("WRITE_FAILED", $"Cannot write bug report file: {ex.Message}", jsonOptions);
            return 1;
        }

        // Output success to stdout
        var result = new
        {
            ok = true,
            data = new
            {
                bug_id = bugId,
                file = Path.Combine(BugReportDir, fileName),
                has_game_state = gameStateSnapshot != null
            }
        };
        Console.WriteLine(JsonSerializer.Serialize(result, jsonOptions));
        return 0;
    }

    /// <summary>
    ///     Attempts to capture the current game state by connecting to the mod's pipe.
    ///     Returns the state data on success, or null if the mod is unreachable.
    /// </summary>
    private static async Task<object?> CaptureGameStateAsync()
    {
        try
        {
            using var client = new PipeClient();
            if (!await client.ConnectAsync(StateSnapshotTimeoutMs))
                return null;

            var request = new Request { Cmd = "state", IncludePileDetails = true };
            var response = await client.SendAsync(request);

            if (response is { Ok: true, Data: not null })
                return response.Data;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteError(string error, string message, JsonSerializerOptions options)
    {
        var response = new { ok = false, error, message };
        Console.Error.WriteLine(JsonSerializer.Serialize(response, options));
    }
}
