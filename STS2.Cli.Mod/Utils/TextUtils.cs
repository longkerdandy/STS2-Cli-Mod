using System.Text.RegularExpressions;

namespace STS2.Cli.Mod.Utils;

/// <summary>
///     Text utility methods for cleaning and formatting game text.
/// </summary>
public static class TextUtils
{
    // Standard BBCode pattern: matches everything between [ and ] (non-greedy)
    private static readonly Regex BbCodeRegex = new(@"\[.*?\]", RegexOptions.Compiled);
    
    // Rich text pattern: matches everything between < and >
    private static readonly Regex RichTextRegex = new("<[^>]+>", RegexOptions.Compiled);

    /// <summary>
    ///     Removes BBCode tags from text (e.g., [gold], [/gold], [color=red], etc.)
    /// </summary>
    private static string StripBbCode(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return BbCodeRegex.Replace(text, "").Trim();
    }

    /// <summary>
    ///     Strips rich text tags (Unity/Godot style tags like <c>&lt;color&gt;</c>, <c>&lt;b&gt;</c>, etc.)
    /// </summary>
    private static string StripRichText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return RichTextRegex.Replace(text, "").Trim();
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
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        
        return cleaned.Trim();
    }
}
