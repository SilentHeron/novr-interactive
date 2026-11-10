using System;
using System.Collections.Generic;
using System.Text;

namespace NOVR.Compatibility;

// One place for the things a player needs to know when the mod starts in a
// degraded state: a different game version, patches that could not be
// installed, a VR start that failed. The log gets every line; the wrist menu
// and the desktop notice show the summary.
internal static class StartupDiagnostics
{
    // The game build this version's patches were written and checked against.
    internal const string SupportedGameVersion = "0.34.6";

    private static readonly List<string> Messages = new();
    internal static Action<string>? Log { get; set; }

    internal static IReadOnlyList<string> Warnings => Messages;

    internal static string Summary
    {
        get
        {
            if (Messages.Count == 0)
                return string.Empty;
            var text = new StringBuilder("NOVR warnings:");
            foreach (var message in Messages)
                text.Append("\n• ").Append(message);
            return text.ToString();
        }
    }

    internal static void Warn(string message)
    {
        if (Messages.Contains(message))
            return;
        Messages.Add(message);
        Log?.Invoke("[NOVR] " + message);
    }

    internal static void Reset() => Messages.Clear();

    // Other mods append their own tag to the reported version
    // ("0.34.6_com.example.mod-v1"); compare the leading number only.
    internal static string NumericVersion(string? reported)
    {
        if (reported == null)
            return string.Empty;
        var end = 0;
        while (end < reported.Length && (char.IsDigit(reported[end]) || reported[end] == '.'))
            end++;
        return reported.Substring(0, end).TrimEnd('.');
    }

    internal static bool IsSupportedGame(string? reported) =>
        NumericVersion(reported) == SupportedGameVersion;

    internal static void CheckGame(string? reportedVersion)
    {
        if (IsSupportedGame(reportedVersion))
            return;
        var actual = NumericVersion(reportedVersion);
        Warn("Built for Nuclear Option " + SupportedGameVersion + "; this game reports " +
             (actual.Length == 0 ? "an unknown version" : actual) +
             ". VR adjustments may be missing or wrong until NOVR is updated.");
    }

    // The mod draws its own UI on fixed layers. A named layer there belongs to
    // the game or to another mod, whose cameras and objects would then mix
    // with the VR interface.
    internal static void CheckLayers(IEnumerable<int> layers, Func<int, string?> layerName)
    {
        foreach (var layer in layers)
        {
            string? name;
            try
            {
                name = layerName(layer);
            }
            catch (Exception)
            {
                continue;
            }
            if (!string.IsNullOrEmpty(name))
                Warn("Layer " + layer + " is already named \"" + name + "\"; NOVR also draws its VR interface on it, so some objects may appear in the wrong view.");
        }
    }

    internal static void ReportPatches(PatchInstallReport report)
    {
        if (report.Skipped.Count == 0)
        {
            Log?.Invoke("[NOVR] Installed " + report.Applied + " patch classes.");
            return;
        }
        foreach (var skipped in report.Skipped)
            Log?.Invoke("[NOVR] Patch " + skipped.Key + " was not installed (" + skipped.Value + ").");
        Warn(report.Skipped.Count + " of " + (report.Applied + report.Skipped.Count) +
             " VR adjustments could not be installed for this game version; the log lists them.");
    }
}
