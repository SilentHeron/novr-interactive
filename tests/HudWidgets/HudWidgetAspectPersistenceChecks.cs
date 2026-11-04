using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class HudWidgetAspectPersistenceChecks
{
    internal static void Run(string directory, Action<bool, string> check)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            var path = Path.Combine(directory, "map-aspect-slots.cfg");
            var config = Open(path);
            var profiles = new HudWidgetProfiles(config);
            var expected = new Dictionary<(string aircraft, int slot), Dictionary<string, HudWidgetPose>>();
            var aircraft = new[] { "FS-20", "VT-7", "modauthor/custom-plane[wide]" };
            var aspects = new[] { .5f, 1.25f, 2f, .75f, 1.5f, 3f, .25f, 1f, 4f };
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            for (var a = 0; a < aircraft.Length; a++)
                for (var slot = 1; slot <= 3; slot++)
                {
                    var layout = profiles.Load(aircraft[a], slot);
                    var map = layout["map"];
                    map.Position = new Vector3(.21f + a * .03f, -.19f - slot * .02f, .67f + slot * .04f);
                    map.Euler = new Vector3(12.5f, -18.75f + a, 0);
                    map.Scale = 1f + a * .05f;
                    map.Opacity = .45f + slot * .1f;
                    map.Background = .12f + a * .11f;
                    map.Visible = slot != 2;
                    map.Aspect = aspects[a * 3 + slot - 1];
                    layout["map"] = map;
                    var feed = layout["feed"];
                    feed.Background = .13f * slot;
                    feed.Opacity = .4f + a * .1f;
                    feed.Visible = a != 1;
                    layout["feed"] = feed;
                    profiles.Save(aircraft[a], layout, slot);
                    expected[(aircraft[a], slot)] = layout;
                    var encoded = Entry(config, aircraft[a], slot, "map").Value;
                    check(encoded.Split('|').Length == (map.Aspect == 1 ? 10 : 11), "Only reshaped maps append an aspect field");
                    check(!encoded.Contains(','), "Map dimensions serialize with invariant decimal separators");
                }
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            config = Open(path);
            profiles = new HudWidgetProfiles(config);
            foreach (var pair in expected)
            {
                var actual = profiles.Load(pair.Key.aircraft, pair.Key.slot);
                foreach (var id in HudWidgetProfiles.Ids)
                    Same(actual[id], pair.Value[id], check, "All aircraft/slot fields survive map aspect reload: " + id);
                check(profiles.HasSavedLayout(pair.Key.aircraft, pair.Key.slot), "Aspect roundtrip preserves saved-slot selection");
                Dimensions(actual["map"], check);
            }
            var precise = expected[("FS-20", 1)]["map"];
            precise.Aspect = 1.2345678f;
            profiles.Save("FS-20", new Dictionary<string, HudWidgetPose> { ["map"] = precise });
            var restored = new HudWidgetProfiles(Open(path));
            Same(restored.Load("FS-20")["map"], precise, check, "Precise aspect persists without changing position or appearance");
            foreach (var id in HudWidgetProfiles.Ids.Where(id => id != "map"))
                Same(restored.Load("FS-20")[id], expected[("FS-20", 1)][id], check, "Reshaping map leaves sibling widget unchanged: " + id);
            foreach (var pair in expected.Where(pair => pair.Key != ("FS-20", 1)))
                Same(restored.Load(pair.Key.aircraft, pair.Key.slot)["map"], pair.Value["map"], check,
                    "Editing one map does not reshape another aircraft or slot");
            restored.ResetAircraft("FS-20", 2);
            var reset = new HudWidgetProfiles(Open(path));
            check(reset.Load("FS-20", 2)["map"].Aspect == 1 && !reset.HasSavedLayout("FS-20", 2),
                "Reset restores only the selected slot to a square map");
            Same(reset.Load("FS-20", 3)["map"], expected[("FS-20", 3)]["map"], check, "Aspect reset leaves another slot intact");
            Same(reset.Load("VT-7", 2)["map"], expected[("VT-7", 2)]["map"], check, "Aspect reset leaves another aircraft intact");

            CheckLegacy(directory, check);
            CheckInvalidAndNonMap(directory, check);
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }

    private static void CheckLegacy(string directory, Action<bool, string> check)
    {
        var path = Path.Combine(directory, "map-aspect-legacy.cfg");
        var config = Open(path);
        const string aircraft = "legacy-map-aircraft";
        const string tenFields = "0.23|-0.31|0.84|15|-28|0|1.2|0.61|0.27|0";
        for (var slot = 1; slot <= 3; slot++)
        {
            config.Bind(Section(aircraft, slot), "Use aircraft layout", true).Value = true;
            config.Bind(Section(aircraft, slot), "Compact reachable defaults applied", true).Value = true;
            Entry(config, aircraft, slot, "map").Value = tenFields;
        }
        config.Save();
        config = Open(path);
        var profiles = new HudWidgetProfiles(config);
        for (var slot = 1; slot <= 3; slot++)
        {
            var actual = profiles.Load(aircraft, slot)["map"];
            var expected = new HudWidgetPose(new Vector3(.23f, -.31f, .84f), new Vector3(15, -28, 0), 1.2f, .61f, .27f, false);
            expected.Aspect = 1;
            Same(actual, expected, check, "Legacy ten-field custom map retains its entire layout and square aspect");
            check(Entry(config, aircraft, slot, "map").Value == tenFields, "Reading legacy shape never rewrites the saved value");
            profiles.Save(aircraft, new Dictionary<string, HudWidgetPose> { ["map"] = actual }, slot);
            check(Entry(config, aircraft, slot, "map").Value == tenFields, "Saving a square legacy map retains its ten-field format");
        }
    }

    private static void CheckInvalidAndNonMap(string directory, Action<bool, string> check)
    {
        var path = Path.Combine(directory, "map-aspect-invalid.cfg");
        var config = Open(path);
        var profiles = new HudWidgetProfiles(config);
        const string aircraft = "shape-validation";
        var original = new HudWidgetPose(new Vector3(.25f, -.33f, .73f), new Vector3(10, -20, 0), 1, .63f, .38f, false);
        foreach (var aspect in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -100f, 0f, .01f, .25f, 1f, 4f, 100f })
            foreach (var scale in new[] { .35f, 1f, 3f })
            {
                var candidate = original;
                candidate.Aspect = aspect;
                candidate.Scale = scale;
                profiles.Save(aircraft, new Dictionary<string, HudWidgetPose> { ["map"] = candidate });
                var loaded = new HudWidgetProfiles(Open(path)).Load(aircraft)["map"];
                check(float.IsFinite(loaded.Aspect) && loaded.Aspect >= .25f && loaded.Aspect <= 4f,
                    "Malformed/out-of-range aspect always reloads within supported bounds");
                if (!float.IsFinite(aspect) || aspect <= 0)
                    check(loaded.Aspect == 1, "Nonfinite/nonpositive map aspect falls back to square");
                if (float.IsFinite(aspect) && aspect > 4)
                    check(loaded.Aspect == 4, "Excessively wide map clamps to the widest supported shape");
                if (aspect > 0 && aspect < .25f)
                    check(loaded.Aspect == .25f, "Excessively tall map clamps to the tallest supported shape");
                Dimensions(loaded, check);
                var untouched = loaded;
                untouched.Scale = original.Scale;
                untouched.Aspect = original.Aspect;
                Same(untouched, original, check, "Shape sanitization preserves map position, angles, opacity, backing and visibility");
            }
        foreach (var id in HudWidgetProfiles.Ids.Where(id => id != "map"))
        {
            var attempted = original;
            attempted.Aspect = 2.7f;
            profiles.Save(aircraft, new Dictionary<string, HudWidgetPose> { [id] = attempted });
            var actual = new HudWidgetProfiles(Open(path)).Load(aircraft)[id];
            check(actual.Aspect == 1, "Non-map widget cannot acquire rectangular geometry: " + id);
            attempted.Aspect = 1;
            Same(actual, attempted, check, "Rejected non-map reshape preserves all other customization: " + id);
            check(Entry(config, aircraft, 1, id).Value.Split('|').Length == 10, "Non-map widgets retain the legacy format: " + id);
        }
        // Exercise decoding of an eleven-field entry directly, independently
        // from Save's sanitizer, through a fresh ConfigFile each time.
        const string validPrefix = "0.25|-0.33|0.73|10|-20|0|1|0.63|0.38|0|";
        foreach (var token in new[] { "NaN", "Infinity", "-Infinity", "0", "-2", "not-a-number", "0.01", "100" })
        {
            Entry(config, aircraft, 1, "map").Value = validPrefix + token;
            config.Save();
            var decoded = new HudWidgetProfiles(Open(path)).Load(aircraft)["map"];
            check(decoded.Aspect == (token == "0.01" ? .25f : token == "100" ? 4f : 1f), "Eleven-field malformed aspect is sanitized on load");
            Dimensions(decoded, check);
            var unchanged = decoded;
            unchanged.Aspect = original.Aspect;
            unchanged.Scale = original.Scale;
            Same(unchanged, original, check, "Aspect parsing retains the valid legacy fields");
        }
    }

    private static void Dimensions(HudWidgetPose pose, Action<bool, string> check)
    {
        var ratio = Math.Sqrt(pose.Aspect);
        var width = pose.Scale * ratio;
        var height = pose.Scale / ratio;
        check(width >= .35 - 1e-6 && width <= 3 + 1e-6 && height >= .35 - 1e-6 && height <= 3 + 1e-6,
            "Both effective map dimensions stay within .35–3, including extreme aspect/scale pairs");
    }
    private static void Same(HudWidgetPose actual, HudWidgetPose expected, Action<bool, string> check, string message) =>
        check(actual.Position.x == expected.Position.x && actual.Position.y == expected.Position.y && actual.Position.z == expected.Position.z &&
            actual.Euler.x == expected.Euler.x && actual.Euler.y == expected.Euler.y && actual.Euler.z == expected.Euler.z &&
            actual.Scale == expected.Scale && actual.Aspect == expected.Aspect && actual.Opacity == expected.Opacity &&
            actual.Background == expected.Background && actual.Visible == expected.Visible, message);
    private static ConfigFile Open(string path) => new(path, false) { SaveOnConfigSet = false };
    private static ConfigEntry<string> Entry(ConfigFile config, string aircraft, int slot, string id) => config.Bind(Section(aircraft, slot), id, "");
    private static string Section(string aircraft, int slot) => "VR HUD Widgets " +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(aircraft))).Substring(0, 32) + (slot == 1 ? "" : " Slot " + slot);
}
