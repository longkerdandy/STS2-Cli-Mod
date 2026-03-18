using System.Text.RegularExpressions;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Text utility methods for cleaning and formatting game text.
/// </summary>
public static partial class TextUtils
{
    // Known BBCode tags in STS2
    private static readonly string[] BbCodeTags = new[]
    {
        "gold", "color", "b", "i", "u", "s", "sub", "sup", 
        "center", "left", "right", "indent", "code", "quote",
        "url", "img", "list", "table", "tr", "td", "th",
        "player", "enemy", "block", "damage", "keyword"
    };

    /// <summary>
    ///     Escapes a string for use in a regular expression.
    /// </summary>
    private static string EscapeRegex(string text)
    {
        return Regex.Escape(text);
    }

    /// <summary>
    ///     Removes BBCode tags from text (e.g., [gold], [/gold], [color=red], etc.)
    /// </summary>
    public static string StripBbCode(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var result = text;
        
        // First, handle paired tags [tag]content[/tag]
        foreach (var tag in BbCodeTags)
        {
            var escapedTag = EscapeRegex(tag);
            // Remove paired tags with any attributes
            result = Regex.Replace(result, $"\\[{escapedTag}(?:=[^\\]]+)?\\](.*?)\\[/{escapedTag}\\]", "$1", RegexOptions.IgnoreCase);
        }
        
        // Then remove any remaining standalone opening/closing tags
        foreach (var tag in BbCodeTags)
        {
            var escapedTag = EscapeRegex(tag);
            result = Regex.Replace(result, $"\\[/?{escapedTag}(?:=[^\\]]+)?\\]", "", RegexOptions.IgnoreCase);
        }
        
        // Catch-all for any other BBCode-like tags (including [*] for list items)
        result = Regex.Replace(result, @"\[\*(?:=[^\]]+)?\]", "");
        result = Regex.Replace(result, @"\[\w+(?:=[^\]]+)?\]", "");
        result = Regex.Replace(result, @"\[/\w+\]", "");
        
        return result.Trim();
    }

    /// <summary>
    ///     Strips rich text tags (Unity/Godot style)
    /// </summary>
    public static string StripRichText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Remove rich text tags: <color>, <b>, <i>, etc.
        return RichTextRegex().Replace(text, "").Trim();
    }

    /// <summary>
    ///     Cleans dynamic variables like {Block:diff()}, {Damage:diff()} to placeholders.
    /// </summary>
    public static string CleanDynamicVars(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Replace {VariableName:diff()} with [X] (unknown value)
        // These are SmartDescription dynamic variables that need game context to resolve
        var result = DynamicVarRegex().Replace(text, "[X]");
        
        // Clean up any remaining {var} patterns
        result = SimpleVarRegex().Replace(result, "[X]");
        
        return result;
    }

    /// <summary>
    ///     Cleans game text by removing BBCode, rich text tags, and simplifying dynamic vars.
    /// </summary>
    public static string CleanGameText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var cleaned = StripBbCode(text);
        cleaned = StripRichText(cleaned);
        cleaned = CleanDynamicVars(cleaned);
        
        // Normalize whitespace
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        
        return cleaned.Trim();
    }

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex RichTextRegex();

    [GeneratedRegex(@"\{\w+:diff\(\)\}", RegexOptions.Compiled)]
    private static partial Regex DynamicVarRegex();

    [GeneratedRegex(@"\{\w+\}", RegexOptions.Compiled)]
    private static partial Regex SimpleVarRegex();
}
