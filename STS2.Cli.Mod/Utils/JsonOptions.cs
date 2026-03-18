using System.Text.Encodings.Web;
using System.Text.Json;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     JSON serialization options for the mod.
/// </summary>
public static class JsonOptions
{
    /// <summary>
    ///     Default JSON serializer options with Unicode support.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        // Allow Unicode characters without escaping (fixes Chinese text)
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // Pretty print for debugging (can be removed for production)
        WriteIndented = false
    };
}
