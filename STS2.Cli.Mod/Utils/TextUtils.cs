using System.Text.RegularExpressions;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Text utility methods for cleaning and formatting game text.
/// </summary>
public static partial class TextUtils
{
    /// <summary>
    ///     Removes BBCode tags from text (e.g., [gold], [/gold], [color], etc.)
    /// </summary>
    public static string StripBbCode(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Remove BBCode tags: [tag]content[/tag] -> content, [tag] -> ""
        // Handle self-closing and nested tags
        var result = BbCodeRegex().Replace(text, "$1");
        
        // Remove any remaining standalone tags
        result = StandaloneBbCodeRegex().Replace(result, "");
        
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
    ///     Cleans game text by removing both BBCode and rich text tags.
    /// </summary>
    public static string CleanGameText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var cleaned = StripBbCode(text);
        cleaned = StripRichText(cleaned);
        
        return cleaned;
    }

    [GeneratedRegex(@"\[(\w+)(?:=[^\]]+)?\](.*?)\[/\1\]", RegexOptions.Compiled)]
    private static partial Regex BbCodeRegex();

    [GeneratedRegex(@"\[\w+(?:=[^\]]+)?\]", RegexOptions.Compiled)]
    private static partial Regex StandaloneBbCodeRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex RichTextRegex();
}
