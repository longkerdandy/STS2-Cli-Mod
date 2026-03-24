using System.Collections;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     JSON serialization options for the mod.
/// </summary>
public static class JsonOptions
{
    /// <summary>
    ///     Default JSON serializer options with snake_case naming and Unicode support.
    ///     Ignores null values and empty collections for cleaner output.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { IgnoreEmptyCollections }
            }
        };

        return options;
    }

    /// <summary>
    ///     Modifier that suppresses serialization of null or empty collections.
    ///     Applies to any property whose type implements <see cref="ICollection" />.
    /// </summary>
    private static void IgnoreEmptyCollections(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (typeof(ICollection).IsAssignableFrom(property.PropertyType))
            {
                property.ShouldSerialize = (_, value) =>
                    value is ICollection { Count: > 0 };
            }
        }
    }
}
