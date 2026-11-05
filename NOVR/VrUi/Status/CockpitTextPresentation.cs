using System;
using System.Text.RegularExpressions;

namespace NOVR.VrUi.SpecialBehavior;

// Text sizing changes the contents of a widget, never its saved physical size.
// Leave room for the font's ascenders/descenders and show fewer rows when large.
internal static class CockpitTextPresentation
{
    internal const int FeedFontSize = 22;
    private static readonly Regex Tags = new("<[^>]*>", RegexOptions.Compiled);
    internal static float FontSize(int baseline, float scale) => baseline * HudWidgetProfiles.SanitizeTextScale(scale);
    internal static float LineHeight(float fontSize) => (float)Math.Ceiling(fontSize * 1.2f);
    internal static int Lines(float height, float fontSize, int limit) =>
        Math.Max(1, Math.Min(limit, (int)(height / LineHeight(fontSize))));

    // Retain native emphasis and colors, but not native font/size/offset tags
    // that could defeat the slider or push a line outside its assigned region.
    internal static string InstructionText(string value) => Tags.Replace(value, KeepStyle);
    private static string KeepStyle(Match match)
    {
        var tag = match.Value;
        if (tag.Equals("<br>", StringComparison.OrdinalIgnoreCase) || tag.Equals("<br/>", StringComparison.OrdinalIgnoreCase))
            return "\n";
        if (tag.StartsWith("<color=", StringComparison.OrdinalIgnoreCase) || tag.Equals("</color>", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("<b>", StringComparison.OrdinalIgnoreCase) || tag.Equals("</b>", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("<i>", StringComparison.OrdinalIgnoreCase) || tag.Equals("</i>", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("<u>", StringComparison.OrdinalIgnoreCase) || tag.Equals("</u>", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("<s>", StringComparison.OrdinalIgnoreCase) || tag.Equals("</s>", StringComparison.OrdinalIgnoreCase))
            return tag;
        return "";
    }
}
