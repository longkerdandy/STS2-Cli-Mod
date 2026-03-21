using System.Text.RegularExpressions;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Text utility methods for cleaning and formatting game text.
/// </summary>
public static partial class TextUtils
{
    // Standard BBCode pattern: matches everything between [ and ] (non-greedy)
    [GeneratedRegex(@"\[.*?\]")]
    private static partial Regex BbCodeRegex();

    // Rich text pattern: matches everything between < and >
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex RichTextRegex();

    // Whitespace normalization pattern: one or more whitespace chars
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    /// <summary>
    ///     Removes BBCode tags from text (e.g., [gold], [/gold], [color=red], etc.)
    /// </summary>
    private static string StripBbCode(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return BbCodeRegex().Replace(text, "").Trim();
    }

    /// <summary>
    ///     Strips rich text tags (Unity/Godot style tags like <c>&lt;color&gt;</c>, <c>&lt;b&gt;</c>, etc.)
    /// </summary>
    private static string StripRichText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return RichTextRegex().Replace(text, "").Trim();
    }

    /// <summary>
    ///     Strips game tags like BBCode and rich text.
    /// </summary>
    public static string StripGameTags(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var cleaned = StripBbCode(text);
        cleaned = StripRichText(cleaned);

        // Normalize whitespace
        cleaned = WhitespaceRegex().Replace(cleaned, " ");

        return cleaned.Trim();
    }
}
