using BepInEx.Configuration;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class HudWidgetAuxiliaryMigrationChecks
{
    internal static void Run(string directory, Action<bool, string> check)
    {
        var path = Path.Combine(directory, "pre-auxiliary-layout.cfg");
        var config = new ConfigFile(path, false) { SaveOnConfigSet = false };
        var previousIds = HudWidgetProfiles.Ids.Where(id => id != "auxiliary").ToArray();
        var keys = new[] { "FS-20", "mod-author/wing-sweep" };
        var originals = new Dictionary<(string key, int slot, string id), string>();
        foreach (var key in keys)
            for (var slot = 1; slot <= 3; slot++)
            {
                var section = Section(key, slot);
                config.Bind(section, "Use aircraft layout", true).Value = true;
                config.Bind(section, "Compact reachable defaults applied", true).Value = true;
                for (var i = 0; i < previousIds.Length; i++)
                {
                    // Deliberately retain noncanonical numeric spelling too:
                    // migrating the new widget must not rewrite any old value.
                    var raw = $"0.{i + 1}00|-0.600|1.{slot}00|-17|23|0|1.200|0.750|0.250|{i % 2}";
                    var id = previousIds[i];
                    config.Bind(section, id, raw).Value = raw;
                    originals[(key, slot, id)] = raw;
                }
            }
        config.Save();
        config = new ConfigFile(path, false) { SaveOnConfigSet = false };
        var profiles = new HudWidgetProfiles(config);
        foreach (var key in keys)
            for (var slot = 1; slot <= 3; slot++)
                check(profiles.NeedsAuxiliaryPlacement(key, slot), "Each old saved slot waits for its own new-widget placement");
        check(!profiles.NeedsAuxiliaryPlacement("new-aircraft", 1), "A new unsaved slot uses normal full-layout placement");
        check(!profiles.NeedsAuxiliaryPlacement(null), "Missing aircraft identity never creates a pending shared layout");

        var head = new Vector3(.23f, .51f, -.31f);
        var auxiliary = HudWidgetDefaultPlacement.AtHead(head)["auxiliary"];
        profiles.InitializeAuxiliary(keys[0], auxiliary, 2);
        check(!profiles.NeedsAuxiliaryPlacement(keys[0], 2), "Tracked new-widget placement is recorded for only its saved slot");
        check(profiles.NeedsAuxiliaryPlacement(keys[0], 1) && profiles.NeedsAuxiliaryPlacement(keys[0], 3) &&
            profiles.NeedsAuxiliaryPlacement(keys[1], 2), "Migration leaves other aircraft and slots pending independently");
        CheckOldEntries(config);

        config = new ConfigFile(path, false) { SaveOnConfigSet = false };
        profiles = new HudWidgetProfiles(config);
        check(!profiles.NeedsAuxiliaryPlacement(keys[0], 2), "Captured auxiliary placement survives restart");
        var saved = profiles.Load(keys[0], 2)["auxiliary"];
        check((saved.Position - auxiliary.Position).magnitude < .00001f, "New frame is saved relative to the actual seated head");
        profiles.InitializeAuxiliary(keys[0], HudWidgetDefaultPlacement.AtHead(new Vector3(-.6f, 1.2f, .8f))["auxiliary"], 2);
        check((profiles.Load(keys[0], 2)["auxiliary"].Position - saved.Position).magnitude < .00001f,
            "A different head position cannot replace an already captured auxiliary frame");
        CheckOldEntries(config);

        var custom = saved;
        custom.Position = new Vector3(.7f, -.6f, 1.2f);
        custom.Euler = new Vector3(-21, 32, 0);
        custom.Visible = false;
        custom.Opacity = .7f;
        custom.Background = .3f;
        profiles.Save(keys[0], new Dictionary<string, HudWidgetPose> { ["auxiliary"] = custom }, 2);
        profiles = new HudWidgetProfiles(new ConfigFile(path, false) { SaveOnConfigSet = false });
        profiles.InitializeAuxiliary(keys[0], auxiliary, 2);
        var reopened = profiles.Load(keys[0], 2)["auxiliary"];
        check(reopened.Position.x == .7f && reopened.Euler.y == 32 && !reopened.Visible && reopened.Opacity == .7f && reopened.Background == .3f,
            "Customized/hidden auxiliary position, tilt and appearance survive restart and another initialization attempt");

        void CheckOldEntries(ConfigFile file)
        {
            foreach (var pair in originals)
                check(file.Bind(Section(pair.Key.key, pair.Key.slot), pair.Key.id, "").Value == pair.Value,
                    "Adding the auxiliary frame preserves original stored text for " + pair.Key);
        }
    }

    private static string Section(string key, int slot) => "VR HUD Widgets " +
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))).Substring(0, 32) +
        (slot == 1 ? "" : " Slot " + slot);
}
