using System;
using System.Text.RegularExpressions;

namespace NOVR.VrUi.SpecialBehavior;

internal static class CockpitAlertMath
{
    private static readonly Regex Tags = new("<[^>]*>", RegexOptions.Compiled);
    // Clock bearings are aircraft-relative, including aft threats; never head-relative.
    internal static int Clock(float right, float forward)
    {
        if (float.IsNaN(right) || float.IsNaN(forward) || float.IsInfinity(right) || float.IsInfinity(forward)) return 12;
        var sector = (int)Math.Floor(Math.Atan2(right, forward) * 6 / Math.PI + .5);
        sector = (sector % 12 + 12) % 12;
        return sector == 0 ? 12 : sector;
    }
    internal static (float x, float y) Scope(float right, float forward, float radius)
    {
        var length = Math.Sqrt((double)right * right + (double)forward * forward);
        if (double.IsNaN(length) || double.IsInfinity(length) || length < .001) return (0, 0);
        return ((float)(right / length * radius), (float)(forward / length * radius));
    }
    internal static string LatestFeed(string source, int count = 3)
    {
        // Feed text is presentation only. Strip rich tags before truncation so
        // a truncated opening tag or an injected size tag cannot enlarge it.
        var plain = Tags.Replace(source ?? "", "").Replace("\r", "");
        var lines = plain.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (count <= 0) return "";
        return string.Join("\n", lines, Math.Max(0, lines.Length - count), Math.Min(count, lines.Length));
    }
}
