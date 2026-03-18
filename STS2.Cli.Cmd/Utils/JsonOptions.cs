using System.Text.Encodings.Web;
using System.Text.Json;

namespace STS2.Cli.Cmd.Utils;

/// <summary>
///     JSON serialization options for the CLI.
/// </summary>
public static class JsonOptions
{
    /// <summary>
    ///     Default JSON serializer options with Unicode support.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Allow Unicode characters without escaping (fixes Chinese text)
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };
}
