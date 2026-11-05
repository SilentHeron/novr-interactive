using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class HudWidgetTextSizeChecks
{
    internal static void Run(string directory, Action<bool, string> check)
    {
        foreach (var id in HudWidgetProfiles.Ids.Append("unknown"))
            check(HudWidgetProfiles.SupportsTextSize(id) == (id == "feed" || id == "objectives"),
                "Text size is available only for Notifications and Instructions: " + id);
        foreach (var pose in HudWidgetProfiles.FactoryDefaults.Values)
            check(pose.TextScale == 1, "Factory text size starts at the existing presentation baseline");
        RoundTrip(directory, check);
        LegacyAndInvalid(directory, check);
    }

    private static void RoundTrip(string directory, Action<bool, string> check)
    {
        var path = Path.Combine(directory, "text-size.cfg");
        var config = Open(path);
        var profiles = new HudWidgetProfiles(config);
        var expected = new Dictionary<(string aircraft, int slot), Dictionary<string, HudWidgetPose>>();
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            foreach (var aircraft in new[] { "FS-20", "modauthor/custom-plane" })
                for (var slot = 1; slot <= 3; slot++)
                {
                    var layout = profiles.Load(aircraft, slot);
                    foreach (var id in new[] { "feed", "objectives" })
                    {
                        var pose = layout[id];
                        pose.Position = new Vector3(.23f, -.31f, .84f);
                        pose.Euler = new Vector3(15, -28, 0);
                        pose.Opacity = .61f;
                        pose.Background = .27f;
                        pose.Visible = slot != 3;
                        pose.TextScale = id == "feed" ? .68f + slot * .1f : 1.1f + slot * .1f;
                        if (slot != 1)
                        {
                            pose.Anchor = slot == 2 ? HudWidgetAnchor.LeftWrist : HudWidgetAnchor.RightWrist;
                            pose.WristPosition = new Vector3(.03f, .02f, .05f);
                            pose.WristEuler = new Vector3(35, 20, -15);
                            pose.WristScale = .53f;
                        }
                        layout[id] = pose;
                    }
                    profiles.Save(aircraft, layout, slot);
                    expected[(aircraft, slot)] = layout;
                    foreach (var id in new[] { "feed", "objectives" })
                    {
                        var encoded = Entry(config, aircraft, slot, id).Value;
                        check(encoded.Split('|').Length == 20, "Custom text size appends to the complete cockpit/wrist pose");
                        check(!encoded.Contains(','), "Text scale serializes with invariant decimals");
                    }
                }
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            profiles = new HudWidgetProfiles(Open(path));
            foreach (var entry in expected)
                foreach (var id in HudWidgetProfiles.Ids)
                    check(profiles.Load(entry.Key.aircraft, entry.Key.slot)[id].Equals(entry.Value[id]),
                        "Reload preserves every field independently per aircraft/slot: " + id);

            var original = profiles.Load("FS-20", 2);
            var preview = new Dictionary<string, HudWidgetPose>(original);
            var edited = preview["feed"];
            edited.TextScale = 1.45f;
            preview["feed"] = edited;
            check(original["feed"].TextScale != edited.TextScale &&
                profiles.Load("FS-20", 2)["feed"].Equals(original["feed"]),
                "Editing a copied layout then canceling cannot change saved text size or attachment");
            profiles.Save("FS-20", new() { ["feed"] = edited }, 2);
            var reloaded = new HudWidgetProfiles(Open(path));
            check(reloaded.Load("FS-20", 2)["feed"].Equals(edited), "Partial text edit persists without altering its wrist or cockpit pose");
            foreach (var id in HudWidgetProfiles.Ids.Where(id => id != "feed"))
                check(reloaded.Load("FS-20", 2)[id].Equals(original[id]), "Partial text edit preserves sibling widget: " + id);
            check(reloaded.Load("FS-20", 1)["feed"].Equals(expected[("FS-20", 1)]["feed"]) &&
                reloaded.Load("modauthor/custom-plane", 2)["feed"].Equals(expected[("modauthor/custom-plane", 2)]["feed"]),
                "Changing text size does not affect another slot or aircraft");
            reloaded.ResetAircraft("FS-20", 2);
            var reset = new HudWidgetProfiles(Open(path));
            check(reset.Load("FS-20", 2)["feed"].TextScale == 1 && !reset.HasSavedLayout("FS-20", 2),
                "Reset restores selected slot's default text size");
            check(reset.Load("FS-20", 3)["feed"].Equals(expected[("FS-20", 3)]["feed"]),
                "Text-size reset preserves another slot's attachment and appearance");
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }

    private static void LegacyAndInvalid(string directory, Action<bool, string> check)
    {
        var path = Path.Combine(directory, "text-size-legacy.cfg");
        var config = Open(path);
        const string aircraft = "text-size-legacy";
        config.Bind(Section(aircraft, 1), "Use aircraft layout", true).Value = true;
        config.Bind(Section(aircraft, 1), "Compact reachable defaults applied", true).Value = true;
        const string cockpit = "0.23|-0.31|0.84|15|-28|0|1.2|0.61|0.27|0";
        const string wrist = cockpit + "|1|2|0.03|0.02|0.05|35|20|-15|0.53";
        foreach (var encoded in new[] { cockpit, cockpit + "|1", wrist })
            foreach (var id in new[] { "feed", "objectives" })
            {
                Entry(config, aircraft, 1, id).Value = encoded;
                config.Save();
                var reloadConfig = Open(path);
                var profiles = new HudWidgetProfiles(reloadConfig);
                var pose = profiles.Load(aircraft)[id];
                check(pose.TextScale == 1 && pose.Position.Equals(new Vector3(.23f, -.31f, .84f)) &&
                    pose.Euler.Equals(new Vector3(15, -28, 0)) && pose.Scale == 1.2f && pose.Opacity == .61f &&
                    pose.Background == .27f && !pose.Visible, "Old 10/11/19-field saves retain every cockpit field with default text size");
                check(pose.Anchor == (encoded == wrist ? HudWidgetAnchor.RightWrist : HudWidgetAnchor.Cockpit),
                    "Old wrist attachment remains selected when adding text-size support");
                check(Entry(reloadConfig, aircraft, 1, id).Value == encoded, "Reading a legacy profile never rewrites its placement");
                profiles.Save(aircraft, new() { [id] = pose });
                check(Entry(reloadConfig, aircraft, 1, id).Value == (encoded == wrist ? wrist : cockpit),
                    "Default text scale keeps existing compact cockpit/wrist encoding");
            }
        foreach (var (token, expected) in new[]
        {
            ("NaN", 1f), ("Infinity", 1f), ("-Infinity", 1f), ("invalid", 1f),
            ("-4", .65f), ("0", .65f), ("0.65", .65f), ("1.5", 1.5f), ("20", 1.5f)
        })
        {
            Entry(config, aircraft, 1, "feed").Value = wrist + "|" + token;
            config.Save();
            var actual = new HudWidgetProfiles(Open(path)).Load(aircraft)["feed"];
            check(actual.TextScale == expected, "Invalid or extreme saved text size recovers safely: " + token);
            check(actual.Position.Equals(new Vector3(.23f, -.31f, .84f)) && actual.Anchor == HudWidgetAnchor.RightWrist &&
                actual.WristPosition.Equals(new Vector3(.03f, .02f, .05f)) && actual.WristScale == .53f,
                "Malformed text size never discards the valid cockpit and wrist placement");
        }
        foreach (var id in HudWidgetProfiles.Ids)
            foreach (var scale in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, .8f, 5f })
            {
                var pose = HudWidgetProfiles.FactoryDefaults[id];
                pose.TextScale = scale;
                var safe = HudWidgetProfiles.Sanitize(id, pose);
                var expected = HudWidgetProfiles.SupportsTextSize(id)
                    ? !float.IsFinite(scale) ? 1 : Math.Clamp(scale, .65f, 1.5f) : 1;
                check(safe.TextScale == expected, "Save sanitization clamps only text-only widgets: " + id);
                safe.TextScale = 1;
                check(safe.Equals(HudWidgetProfiles.FactoryDefaults[id]), "Text-size sanitization leaves every other pose field alone: " + id);
            }
        foreach (var id in HudWidgetProfiles.Ids.Where(id => !HudWidgetProfiles.SupportsTextSize(id)))
        {
            Entry(config, aircraft, 1, id).Value = wrist + "|1.4";
            config.Save();
            check(new HudWidgetProfiles(Open(path)).Load(aircraft)[id].TextScale == 1,
                "Non-text widget ignores a hand-edited text-size field: " + id);
        }
    }

    private static ConfigFile Open(string path) => new(path, false) { SaveOnConfigSet = false };
    private static ConfigEntry<string> Entry(ConfigFile config, string aircraft, int slot, string id) => config.Bind(Section(aircraft, slot), id, "");
    private static string Section(string aircraft, int slot) => "VR HUD Widgets " +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(aircraft))).Substring(0, 32) + (slot == 1 ? "" : " Slot " + slot);
}
