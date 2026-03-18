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
    public static string StripBbCode(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return BbCodeRegex.Replace(text, "").Trim();
    }

    /// <summary>
    ///     Strips rich text tags (Unity/Godot style: &lt;color&gt;, &lt;b&gt;, etc.)
    /// </summary>
    public static string StripRichText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return RichTextRegex.Replace(text, "").Trim();
    }

    /// <summary>
    ///     Cleans game text by removing BBCode and rich text tags.
    /// </summary>
    public static string CleanGameText(string? text)
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
