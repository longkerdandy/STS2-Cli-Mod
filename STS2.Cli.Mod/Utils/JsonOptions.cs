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
    ///     Default JSON serializer options with Unicode support.
    ///     Ignores null values and empty collections for cleaner output.
    /// </summary>
    public static JsonSerializerOptions Default { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            // Allow Unicode characters without escaping (fixes Chinese text)
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            // Do not write null values
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // Do not write indented (compact format)
            WriteIndented = false
        };

        // Add modifier to ignore empty collections
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { IgnoreEmptyCollections }
        };

        return options;
    }

    /// <summary>
    ///     Modifier to ignore empty collections during serialization.
    /// </summary>
    private static void IgnoreEmptyCollections(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            // Check if property is a collection type
            if (property.PropertyType.IsGenericType)
            {
                var genericTypeDef = property.PropertyType.GetGenericTypeDefinition();
                if (genericTypeDef == typeof(List<>) ||
                    genericTypeDef == typeof(IList<>) ||
                    genericTypeDef == typeof(IEnumerable<>) ||
                    genericTypeDef == typeof(ICollection<>) ||
                    genericTypeDef == typeof(Dictionary<,>))
                {
                    // Set custom should serialize predicate
                    property.ShouldSerialize = (_, value) =>
                    {
                        if (value == null) return false;
                        if (value is ICollection collection) return collection.Count > 0;
                        if (value is IEnumerable enumerable)
                        {
                            var enumerator = enumerable.GetEnumerator();
                            return enumerator.MoveNext();
                        }
                        return true;
                    };
                }
            }
        }
    }
}
