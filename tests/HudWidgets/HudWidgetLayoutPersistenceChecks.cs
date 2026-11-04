using BepInEx.Configuration;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class HudWidgetLayoutPersistenceChecks
{
    internal static void Run(string directory, Action<bool, string> check)
    {
        var path = Path.Combine(directory, "saved-layout.cfg");
        var profiles = new HudWidgetProfiles(new ConfigFile(path, false) { SaveOnConfigSet = false });
        check(!profiles.HasSavedLayout("FS-20"), "Only untouched first-use defaults allow automatic reach placement");
        var saved = profiles.Load("FS-20");
        for (var i = 0; i < HudWidgetProfiles.Ids.Length; i++)
        {
            var id = HudWidgetProfiles.Ids[i]; var pose = saved[id];
            // Deliberately beyond the old reach-repair radius, enlarged, and
            // with different backing/content opacity for every widget.
            pose.Position = new Vector3(.7f + i * .1f, -.9f, 1.1f + i * .1f);
            pose.Scale = 1.5f + i * .1f; pose.Background = i / (float)(HudWidgetProfiles.Ids.Length - 1); pose.Opacity = .4f + i * .05f;
            saved[id] = pose;
        }
        profiles.Save("FS-20", saved);
        check(profiles.HasSavedLayout("FS-20"), "Aircraft save disables automatic reach placement");
        check(!profiles.HasSavedLayout("KR-67"), "Aircraft save does not mark another aircraft customized");
        profiles = new HudWidgetProfiles(new ConfigFile(path, false) { SaveOnConfigSet = false });
        check(profiles.HasSavedLayout("FS-20"), "Saved-placement status survives restart");
        var reopened = profiles.Load("FS-20");
        foreach (var id in HudWidgetProfiles.Ids)
        {
            var actual = reopened[id]; var expected = saved[id];
            check(actual.Position.x == expected.Position.x && actual.Position.y == expected.Position.y && actual.Position.z == expected.Position.z,
                "Saved distant position preserved: " + id);
            check(actual.Scale == expected.Scale, "Saved enlarged size preserved: " + id);
            check(actual.Background == expected.Background && actual.Opacity == expected.Opacity,
                "Independent per-widget backing and content opacity preserved: " + id);
        }
        profiles.Save("FS-20", HudWidgetProfiles.FactoryDefaults, 2);
        check(profiles.HasSavedLayout("FS-20", 2), "Explicitly saving even factory-valued slot2 locks its placement");
        check(!profiles.HasSavedLayout("KR-67", 2), "Saving a slot cannot mark another aircraft's same numbered slot customized");
        profiles = new HudWidgetProfiles(new ConfigFile(path, false) { SaveOnConfigSet = false });
        check(profiles.HasSavedLayout("FS-20", 2), "Slot2 placement preference survives restart");
        profiles.ResetAircraft("FS-20");
        check(!profiles.HasSavedLayout("FS-20") && profiles.HasSavedLayout("FS-20", 2), "Resetting slot1 leaves saved slot2 intact");

        var legacy = new ConfigFile(Path.Combine(directory, "pre-marker-shared.cfg"), false) { SaveOnConfigSet = false };
        legacy.Bind("VR HUD Widgets - Defaults", "weapons", "").Value = "0.8|-0.9|1.2|0|0|0|1.5|0.7|0.3|1";
        var older = new HudWidgetProfiles(legacy);
        check(!older.HasSavedLayout("new-aircraft"), "Legacy shared layout is never inherited by a new aircraft");
        var factory = HudWidgetProfiles.FactoryDefaults["weapons"];
        for (var slot = 1; slot <= 3; slot++)
        {
            var actual = older.Load("new-aircraft", slot)["weapons"];
            check(actual.Position.x == factory.Position.x && actual.Position.z == factory.Position.z,
                "Every new slot starts at reachable factory positions, regardless of old global layout");
        }
        foreach (var invalid in new[] { 0, 4, -1 })
        {
            try { profiles.Save("FS-20", saved, invalid); check(false, "Invalid slot was accepted"); }
            catch (ArgumentOutOfRangeException) { }
        }
    }
}
