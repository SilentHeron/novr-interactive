using System.Globalization;
using BepInEx.Configuration;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

var count = 0;
var directory = Path.Combine(Path.GetTempPath(), "novr-hud-widgets-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var previousCulture = CultureInfo.CurrentCulture;
try
{
    var path = Path.Combine(directory, "hud.cfg");
    var config = new ConfigFile(path, false) { SaveOnConfigSet = false };
    var profiles = new HudWidgetProfiles(config);
    True(HudWidgetProfiles.Ids.SequenceEqual(new[]
    {
        "weapons", "countermeasures", "power", "threats", "ew", "feed", "map", "damage", "objectives"
    }), "Catalog preserves persisted widget IDs and menu order");
    foreach (var definition in HudWidgetCatalog.All)
    {
        True(definition.Size.x > 0 && definition.Size.y > 0 &&
            !string.IsNullOrWhiteSpace(definition.Label) && !string.IsNullOrWhiteSpace(definition.Preview),
            "Every registered widget has explicit presentation metadata: " + definition.Id);
        Same(profiles.Load(null)[definition.Id], definition.DefaultPose,
            "Widget presentation and profile initialization use the same default: " + definition.Id);
    }
    Equal(HudWidgetCatalog.Get("weapons").Size.x, 420f, "Weapon panel retains its fitted width");
    Equal(HudWidgetCatalog.Get("feed").Size.y, 270f, "Feed retains its formerly implicit height");
    Equal(HudWidgetCatalog.Get("map").Size.x, 300f, "Map retains its aspect-ratio base width");
    Equal(HudWidgetCatalog.Get("map").Size.y, 300f, "Map retains its aspect-ratio base height");
    Equal(HudWidgetCatalog.Get("threats").Idle, "", "Idle threats remain free of placeholder text");
    Equal(HudWidgetCatalog.Presentation("unknown").Size.y, 270f, "Unknown widget presentation retains the historical feed fallback");
    Equal(HudWidgetCatalog.Label("unknown"), "HUD widget", "Unknown widget labels retain their fallback");
    foreach(var id in new[]{"SmallFighter1","Multirole1","modauthor/FS-3_Ternion","other-author/custom-plane"})
    {
        var all=profiles.Load(id,2);
        True(all.Count==9 && HudWidgetProfiles.Ids.All(all.ContainsKey),"Every stock/custom aircraft receives all nine editable widgets");
        var hidden=all["damage"];hidden.Visible=false;all["damage"]=hidden;
        profiles.Save(id,all,2);
        var restored=profiles.Load(id,2);
        True(restored.Count==9 && !restored["damage"].Visible,"Universal availability preserves saved hide settings");
        True(profiles.Load(id,1)["damage"].Visible,"Visibility stays independent across aircraft slots");
    }
    True(profiles.Load("").Count==9 && !profiles.HasSavedLayout(""),"Missing metadata retains editable defaults without a fallback save");
    profiles.Save("",profiles.Load(""));
    True(!profiles.HasSavedLayout(""),"An unresolved aircraft identity never writes a shared profile");
    var factory = HudWidgetProfiles.FactoryDefaults;
    var defaults = profiles.Load(null);
    Equal(defaults.Count, 9, "Nine independent widget types including damage and instructions");
    True(!profiles.HasOverride(null) && !profiles.HasOverride("FS-20"), "New aircraft inherits defaults");
    foreach (var id in HudWidgetProfiles.Ids)
    {
        Same(defaults[id], factory[id], "Factory " + id);
        Same(profiles.Load("FS-20")[id], factory[id], "Uncustomized aircraft inherits factory " + id);
        Equal(defaults[id].Scale, id == "feed" ? .60f : .65f, "Compact factory scale " + id);
        Equal(defaults[id].Opacity, 1f, "Factory opacity " + id);
        Equal(defaults[id].Background, 0f, "Factory has no backplate " + id);
        True(defaults[id].Visible, "Factory widget visible " + id);
        var p = defaults[id].Position;
        True(Math.Sqrt(p.x * p.x + (p.y + .18f) * (p.y + .18f) + p.z * p.z) <= .55,
            "Default center within 55 cm of nominal shoulder " + id);
        True(defaults[id].Euler.x == 0 && defaults[id].Euler.y == 0 && defaults[id].Euler.z == 0,
            "Default faces straight forward " + id);
    }
    var detached = profiles.Load("FS-20");
    detached["weapons"] = Pose(.15f, .7f, .4f, false);
    Same(profiles.Load("FS-20")["weapons"], factory["weapons"], "Unsaved preview/cancel cannot mutate saved config");
    factory["weapons"] = detached["weapons"];
    Same(HudWidgetProfiles.FactoryDefaults["weapons"], defaults["weapons"], "Caller cannot mutate factory defaults");

    var slotTwo = profiles.Load("FS-20", 2);
    slotTwo["weapons"] = Pose(.2f, .8f, .1f, true);
    profiles.Save("FS-20", slotTwo, 2);
    Same(profiles.Load("FS-20")["weapons"], defaults["weapons"], "Saving slot2 cannot replace slot1");
    Same(profiles.Load("VT-7", 2)["weapons"], defaults["weapons"], "A different aircraft's slot2 remains independent");
    var customized = profiles.Load("FS-20");
    customized["weapons"] = Pose(.45f, .6f, .4f, false);
    profiles.Save("FS-20", customized);
    True(profiles.HasOverride("FS-20"), "Saving creates an aircraft override");
    var slotThree = profiles.Load("FS-20", 3);
    slotThree["weapons"] = Pose(.9f, .5f, .8f, true);
    slotThree["threats"] = Pose(-.3f, .8f, .2f, true);
    profiles.Save("FS-20", slotThree, 3);
    Same(profiles.Load("FS-20")["weapons"], customized["weapons"], "Slot3 save preserves slot1");
    Same(profiles.Load("FS-20")["threats"], customized["threats"], "Full slot1 layout stays stable when another slot changes");
    Same(profiles.Load("VT-7")["weapons"], defaults["weapons"], "Other aircraft never inherits another aircraft's customized layout");

    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
    var precise = new HudWidgetPose(new Vector3(.12345679f, -.8765432f, 1.2345679f), new Vector3(-32.1f, 67.23f, 0), 1.37f, .59f, .31f, false);
    profiles.Save("FS-20", new Dictionary<string, HudWidgetPose> { ["weapons"] = precise, ["unknown widget"] = precise });
    Same(profiles.Load("FS-20")["threats"], customized["threats"], "Partial save preserves other widgets");
    Equal(profiles.Load("FS-20").Count, 9, "Unknown widgets do not create new slots");
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
    var reloadedConfig = new ConfigFile(path, false) { SaveOnConfigSet = false };
    var reloaded = new HudWidgetProfiles(reloadedConfig);
    Same(reloaded.Load("FS-20")["weapons"], precise, "Positions/rotation/appearance persist across locales and process reload");
    Same(reloaded.Load("FS-20", 2)["weapons"], slotTwo["weapons"], "Slot2 persists across reload");
    Same(reloaded.Load("FS-20", 3)["weapons"], slotThree["weapons"], "Slot3 persists across reload");
    Same(reloaded.Load("VT-7")["weapons"], defaults["weapons"], "New aircraft remains factory after reload");
    reloaded.ResetAircraft("FS-20");
    True(!reloaded.HasOverride("FS-20"), "Reset removes aircraft override");
    Same(reloaded.Load("FS-20")["weapons"], defaults["weapons"], "Reset returns only the requested slot to factory");
    var resetReload = new HudWidgetProfiles(new ConfigFile(path, false) { SaveOnConfigSet = false });
    Same(resetReload.Load("FS-20")["weapons"], defaults["weapons"], "Reset override flag persists");
    Same(resetReload.Load("FS-20", 2)["weapons"], slotTwo["weapons"], "Resetting slot1 leaves slot2 intact");
    Same(resetReload.Load("FS-20", 3)["threats"], slotThree["threats"], "Resetting slot1 leaves slot3 intact");

    foreach (var id in HudWidgetProfiles.Ids)
    {
        var invalid = new HudWidgetPose(new Vector3(float.NaN, float.PositiveInfinity, float.NegativeInfinity),
            new Vector3(float.NaN, float.PositiveInfinity, float.NegativeInfinity), float.NaN, float.PositiveInfinity, float.NegativeInfinity, false);
        var safe = HudWidgetProfiles.Sanitize(id, invalid);
        var expected = HudWidgetProfiles.FactoryDefaults[id]; expected.Visible = false;
        Same(safe, expected, "Nonfinite fields fall back safely " + id);
        var bounds = HudWidgetProfiles.Sanitize(id, new HudWidgetPose(new Vector3(-100, 100, -100), new Vector3(720, -450, 1080), -10, -1, 50));
        True(bounds.Position.x == -3 && bounds.Position.y == 3 && bounds.Position.z == -.5f, "Position clamps " + id);
        True(bounds.Euler.x == 0 && bounds.Euler.y == -90 && bounds.Euler.z == 0, "Angles normalize " + id);
        True(bounds.Scale == HudWidgetProfiles.MinScale && bounds.Opacity == HudWidgetProfiles.MinOpacity && bounds.Background == 1, "Appearance clamps " + id);
        var upper = HudWidgetProfiles.Sanitize(id, new HudWidgetPose(new Vector3(100, -100, 100), new Vector3(float.MaxValue, 0, 0), 20, 20, -20));
        True(upper.Position.x == 3 && upper.Position.y == -3 && upper.Position.z == 5, "Upper position clamps " + id);
        True(Math.Abs(upper.Euler.x) <= 180 && float.IsFinite(upper.Euler.x), "Extreme finite angle remains valid " + id);
        True(upper.Scale == HudWidgetProfiles.MaxScale && upper.Opacity == 1 && upper.Background == 0, "Upper appearance clamps " + id);
    }

    var malformed = new ConfigFile(Path.Combine(directory, "malformed.cfg"), false) { SaveOnConfigSet = false };
    var malformedProfiles = new HudWidgetProfiles(malformed);
    var section = "VR HUD Widgets " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("malformed"))).Substring(0, 32);
    malformed.Bind(section, "Use aircraft layout", true).Value = true;
    foreach (var value in new[] { "", "1|2|3", "a|b|c|0|0|0|1|1|0|1", "0|0|1|0|0|0|1|1|0|maybe" })
    {
        malformed.Bind(section, "threats", "").Value = value;
        Same(malformedProfiles.Load("malformed")["threats"], HudWidgetProfiles.FactoryDefaults["threats"], "Malformed config recovers");
    }
    malformed.Bind(section, "threats", "").Value = "NaN|Infinity|-Infinity|NaN|Infinity|-Infinity|NaN|Infinity|-Infinity|0";
    var malformedSafe = malformedProfiles.Load("malformed")["threats"];
    True(float.IsFinite(malformedSafe.Position.x) && float.IsFinite(malformedSafe.Scale) && !malformedSafe.Visible, "Nonfinite persisted values sanitized on read");
    var specialKey = "modded/aircraft[variant]\nwith=characters";
    malformedProfiles.Save(specialKey, new Dictionary<string, HudWidgetPose> { ["ew"] = precise });
    Same(malformedProfiles.Load(specialKey)["ew"], precise, "Arbitrary modded aircraft IDs round-trip through hash");
    True(malformed.Keys.All(k => !k.Section.Contains("aircraft[") && !k.Section.Contains('\n')), "Aircraft identity never becomes a config section verbatim");
    malformedProfiles.Save("fs-20", new Dictionary<string, HudWidgetPose> { ["weapons"] = precise });
    Same(malformedProfiles.Load("FS-20")["weapons"], HudWidgetProfiles.FactoryDefaults["weapons"], "Distinct case-sensitive aircraft IDs remain separate");
    malformedProfiles.Save("", new Dictionary<string, HudWidgetPose> { ["weapons"] = precise });
    Same(malformedProfiles.Load(null)["weapons"], HudWidgetProfiles.FactoryDefaults["weapons"], "Empty aircraft key cannot accidentally replace defaults");

    var old = new Dictionary<string, HudWidgetPose>
    {
        ["weapons"] = new(new Vector3(.34f, -.34f, .75f), new Vector3(0, 12, 0)),
        ["countermeasures"] = new(new Vector3(-.34f, -.32f, .75f), new Vector3(0, -12, 0)),
        ["power"] = new(new Vector3(-.34f, -.43f, .75f), new Vector3(0, -12, 0)),
        ["threats"] = new(new Vector3(-.27f, -.12f, .85f), new Vector3(0, -12, 0)),
        ["ew"] = new(new Vector3(-.54f, -.31f, .85f), new Vector3(0, -20, 0)),
        ["feed"] = new(new Vector3(.27f, -.12f, .85f), new Vector3(0, 12, 0))
    };
    var legacyPath = Path.Combine(directory, "legacy.cfg");
    var legacy = new ConfigFile(legacyPath, false) { SaveOnConfigSet = false };
    const string sharedSection = "VR HUD Widgets - Defaults";
    var aircraftSection = "VR HUD Widgets " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("legacy-aircraft"))).Substring(0, 32);
    foreach (var pair in old)
    {
        legacy.Bind(sharedSection, pair.Key, Encode(pair.Value));
        legacy.Bind(aircraftSection, pair.Key, Encode(pair.Value));
    }
    legacy.Bind(aircraftSection, "Use aircraft layout", true);
    var customDefault = old["feed"]; customDefault.Position.x += .015f;
    legacy.Bind(sharedSection, "feed", "").Value = Encode(customDefault);
    var customAircraft = old["ew"]; customAircraft.Opacity = .45f;
    legacy.Bind(aircraftSection, "ew", "").Value = Encode(customAircraft);
    legacy.Save();
    // Reload so aircraft entries begin as unbound/orphan config values. Slot1
    // migration must preserve the actual aircraft override without importing global settings.
    var upgraded = new HudWidgetProfiles(new ConfigFile(legacyPath, false) { SaveOnConfigSet = false });
    var upgradedAircraft = upgraded.Load("legacy-aircraft");
    foreach (var pair in old)
    {
        Same(upgraded.Load(null)[pair.Key], HudWidgetProfiles.FactoryDefaults[pair.Key],
            "Legacy shared defaults are never imported " + pair.Key);
        Same(upgradedAircraft[pair.Key], pair.Key == "ew" ? customAircraft : HudWidgetProfiles.FactoryDefaults[pair.Key],
            "Only exact legacy aircraft defaults migrate " + pair.Key);
    }
    Same(upgradedAircraft["map"], HudWidgetProfiles.FactoryDefaults["map"], "Old override gets missing map with reachable visible default");
    Same(upgradedAircraft["damage"], HudWidgetProfiles.FactoryDefaults["damage"], "Old saved layout gains reachable damage widget without repositioning other widgets");
    Same(upgradedAircraft["objectives"], HudWidgetProfiles.FactoryDefaults["objectives"], "Old saved layout gains readable instructions widget");
    True(upgraded.HasOverride("legacy-aircraft"), "Migration preserves override selection");
    var migratedReload = new HudWidgetProfiles(new ConfigFile(legacyPath, false) { SaveOnConfigSet = false });
    Same(migratedReload.Load("legacy-aircraft")["weapons"], HudWidgetProfiles.FactoryDefaults["weapons"], "Migrated positions persist without explicit user save");
    Same(migratedReload.Load(null)["feed"], HudWidgetProfiles.FactoryDefaults["feed"], "Custom shared placement is ignored after reload");
    migratedReload.Save("legacy-aircraft", new Dictionary<string, HudWidgetPose> { ["weapons"] = old["weapons"] }, 3);
    migratedReload.Save("legacy-aircraft", new Dictionary<string, HudWidgetPose> { ["power"] = old["power"] });
    var intentionalRestore = new HudWidgetProfiles(new ConfigFile(legacyPath, false) { SaveOnConfigSet = false });
    Same(intentionalRestore.Load("legacy-aircraft", 3)["weapons"], old["weapons"], "Later intentional old default placement is preserved in its own slot");
    Same(intentionalRestore.Load("legacy-aircraft")["power"], old["power"], "Aircraft migration also runs once");
    HudWidgetGestureChecks.Run(True);
    HudWidgetCornerAcquireChecks.Run(True);
    CockpitWidgetSpaceChecks.Run(True);
    HudWidgetLayoutPersistenceChecks.Run(directory, True);
    HudWidgetDefaultPlacementChecks.Run(directory, True);
    HudWidgetAngleChecks.Run(True);
    HudWidgetMotionChecks.Run(True);
    HudWidgetTwoHandChecks.Run(True);
    HudWidgetMapShapeChecks.Run(True);
    HudWidgetAspectPersistenceChecks.Run(directory, True);
    Console.WriteLine($"HUD widget checks passed: {count} assertions (real config persistence, gesture math, and meter-space conversion).");
}
finally { CultureInfo.CurrentCulture = previousCulture; Directory.Delete(directory, true); }

HudWidgetPose Pose(float x, float opacity, float background, bool visible) => new(new Vector3(x, -.25f, .9f), new Vector3(0, 8, 0), 1.2f, opacity, background, visible);
void Same(HudWidgetPose a, HudWidgetPose b, string label) => True(a.Position.x == b.Position.x && a.Position.y == b.Position.y && a.Position.z == b.Position.z &&
    a.Euler.x == b.Euler.x && a.Euler.y == b.Euler.y && a.Euler.z == b.Euler.z && a.Scale == b.Scale && a.Aspect == b.Aspect && a.Opacity == b.Opacity && a.Background == b.Background && a.Visible == b.Visible, label);
void Equal<T>(T a, T b, string label) => True(EqualityComparer<T>.Default.Equals(a, b), label);
void True(bool value, string label) { if (!value) throw new Exception(label); count++; }
string Encode(HudWidgetPose p) => string.Join("|", new[] { p.Position.x, p.Position.y, p.Position.z,
    p.Euler.x, p.Euler.y, p.Euler.z, p.Scale, p.Opacity, p.Background }.Select(v => v.ToString("R", CultureInfo.InvariantCulture)).Append(p.Visible ? "1" : "0"));
