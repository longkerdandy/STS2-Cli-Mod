using System.Text.Encodings.Web;
using System.Text.Json;

namespace STS2.Cli.Cmd.Utils;

/// <summary>
///     JSON serialization options for the CLI.
/// </summary>
public static class JsonOptions
{
    /// <summary>
    ///     Default JSON serializer options with Unicode support (compact format).
    /// </summary>
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    /// <summary>
    ///     Pretty-print JSON serializer options with indentation.
    /// </summary>
    public static JsonSerializerOptions Pretty { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };
}
