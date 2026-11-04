using BepInEx.Configuration;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class HudWidgetDefaultPlacementChecks
{
    internal static void Run(string directory, Action<bool, string> check)
    {
        var defaults = HudWidgetProfiles.FactoryDefaults;
        foreach (var center in new[] { Vector3.zero, new Vector3(.4f, 1.3f, -.7f), new Vector3(-.6f, -.4f, .8f) })
            foreach (var origin in new[] { Vector3.zero, new Vector3(31, 8, -19), new Vector3(-73, 44, 51) })
                foreach (var rotation in new[] { Quaternion.identity, Quaternion.Euler(0, 90, 0), Quaternion.Euler(-22, 137, 17) })
                {
                    var actual = HudWidgetDefaultPlacement.AtHead(center);
                    var seatedWorld = CockpitWidgetSpace.ToWorld(origin, rotation, center);
                    foreach (var id in HudWidgetProfiles.Ids)
                    {
                        var pose = actual[id];
                        var offset = defaults[id].Position;
                        var world = CockpitWidgetSpace.ToWorld(origin, rotation, pose.Position);
                        Near(world, seatedWorld + rotation * offset, "New " + id + " uses seated center across cockpit origins/heights/orientations");
                        check(pose.Position.z > center.z + .2f, "New widget stays forward of the seated head: " + id);
                        check(pose.Euler.x == defaults[id].Euler.x && pose.Euler.y == defaults[id].Euler.y && pose.Scale == defaults[id].Scale,
                            "First placement preserves default tilt and physical scale: " + id);
                        var size = Size(id);
                        var half = size * (.0007f * pose.Scale * .5f);
                        foreach (var x in new[] { -half.x, half.x })
                            foreach (var y in new[] { -half.y, half.y })
                            {
                                var corner = offset + new Vector3(x, y, 0);
                                var reach = Math.Min((corner - new Vector3(-.16f, -.18f, 0)).magnitude,
                                    (corner - new Vector3(.16f, -.18f, 0)).magnitude);
                                check(reach < .60f, "Every new widget corner remains within a 60cm shoulder reach: " + id);
                            }
                        // No TransformPoint is involved for the original pilot bone:
                        // its 100x imported scale cannot multiply these meter offsets.
                        Near(CockpitWidgetSpace.ToLocal(origin, rotation, world), pose.Position,
                            "Default can be stored/reloaded as rigid cockpit meters: " + id);
                    }
                }
        Near(defaults["weapons"].Position, HudWidgetProfiles.FactoryDefaults["weapons"].Position,
            "Seated placement cannot mutate the common offset template");

        var gate = new HudWidgetDefaultPlacement();
        check(!gate.Observe(false, Vector3.zero, 10, 1) && !gate.Observe(false, Vector3.zero, 20, 2),
            "Missing tracking never commits a zero-position fallback, however long startup takes");
        check(!gate.Observe(true, Vector3.zero, 20, 3), "First actual pose begins settling rather than accepting a stale camera frame");
        check(!gate.Observe(true, Vector3.zero, 20.2f, 3), "Repeated calls in one frame cannot satisfy startup readiness");
        check(!gate.Observe(true, Vector3.zero, 20.21f, 4), "At least three distinct tracked frames required");
        check(gate.Observe(true, Vector3.zero, 20.22f, 5), "A genuinely tracked calibrated zero position is valid");
        gate.Reset();
        var previousAircraft = new Vector3(0, 1.6f, 0);
        check(!gate.Observe(true, previousAircraft, 30, 10), "New aircraft starts a fresh pose gate");
        check(!gate.Observe(true, previousAircraft, 30.06f, 11), "A single-frame valid position does not finalize defaults");
        check(!gate.Observe(true, Vector3.zero, 30.1f, 12), "Spawn translation/calibration jump restarts settling");
        check(!gate.Observe(true, Vector3.zero, 30.2f, 13), "Old aircraft's time does not count after recenter");
        check(gate.Observe(true, new Vector3(.005f, 0, 0), 30.3f, 14), "Minor normal head tracking motion does not prevent initialization");
        gate.Reset();
        check(!gate.Observe(true, Vector3.zero, 40, 20), "Calibration context reset starts waiting anew");
        check(!gate.Observe(false, Vector3.zero, 40.2f, 21), "Tracking loss discards pending placement");
        check(!gate.Observe(true, Vector3.zero, 50, 22), "Returning tracking does not reuse elapsed lost-tracking time");
        check(!gate.Observe(true, new Vector3(float.NaN, 0, 0), 50.2f, 23), "Nonfinite position cannot initialize a layout");
        check(!gate.Observe(true, new Vector3(float.PositiveInfinity, 0, 0), 50.3f, 24), "Infinite position is rejected too");

        var file = Path.Combine(directory, "seated-default-layout.cfg");
        var profiles = new HudWidgetProfiles(new ConfigFile(file, false) { SaveOnConfigSet = false });
        var fitted = HudWidgetDefaultPlacement.AtHead(new Vector3(.19f, .34f, -.25f));
        check(!profiles.HasSavedLayout("different-cockpit", 3), "Fitting initial defaults does not silently save a slot");
        var custom = fitted["map"];
        custom.Euler = new Vector3(13, -24, 0);
        fitted["map"] = custom;
        profiles.Save("different-cockpit", fitted, 3);
        profiles = new HudWidgetProfiles(new ConfigFile(file, false) { SaveOnConfigSet = false });
        check(profiles.HasSavedLayout("different-cockpit", 3), "Explicit saved slot bypasses default-placement initialization on spawn");
        foreach (var newSeat in new[] { Vector3.zero, new Vector3(-.8f, 1.8f, .9f) })
        {
            // Loading a saved profile does not ask for or incorporate the new
            // head center. Edit previews receive this same loaded dictionary.
            var saved = profiles.Load("different-cockpit", 3);
            foreach (var id in HudWidgetProfiles.Ids)
                Near(saved[id].Position, fitted[id].Position, "Saved widget stays fixed after a different seated pose: " + id);
            check(saved["map"].Euler.x == 13 && saved["map"].Euler.y == -24, "Saved pitch/yaw survives initialization changes");
            check(!profiles.HasSavedLayout("different-cockpit", 1), "Saved slot3 does not initialize another slot");
            var fresh = HudWidgetDefaultPlacement.AtHead(newSeat);
            Near(fresh["map"].Position - newSeat, defaults["map"].Position, "New unsaved slot is centered independently");
        }
        void Near(Vector3 a, Vector3 b, string message) => check((a - b).magnitude < .00004f, message);
    }
    private static Vector3 Size(string id) => id switch
    {
        "weapons" => new Vector3(420, 170, 0),
        "countermeasures" => new Vector3(260, 135, 0),
        "power" => new Vector3(420, 80, 0),
        "threats" => new Vector3(460, 190, 0),
        "ew" => new Vector3(260, 260, 0),
        "map" or "damage" => new Vector3(300, 300, 0),
        "objectives" => new Vector3(420, 150, 0),
        _ => new Vector3(460, 270, 0)
    };
}
