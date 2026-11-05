using BepInEx.Configuration;
using NOVR.MotionControls;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

var checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
void Near(Vector3 a, Vector3 b, string message) => Check((a - b).magnitude < .0001f, message);
WristDockChecks.Run(Check);
WristDropRuntimeChecks.Run(Check);
var visibility = new HudWidgetWristVisibility();
Check(visibility.Update(true, false, false, 1, 1, .5f, .05f) == 0, "Accidental brief glance does not flash the card");
Check(visibility.Update(true, false, false, 1, 1, .5f, .05f) > 0, "Deliberate wrist check fades into view");
for (var i = 0; i < 5; i++) visibility.Update(true, false, false, .85f, .4f, .5f, .04f);
Check(visibility.Alpha == 1, "Hysteresis retains a readable card near the entry boundary");
Check(visibility.Update(true, false, false, .5f, .4f, .5f, .05f) < 1, "Looking away fades the wrist card");
for (var i = 0; i < 5; i++) visibility.Update(true, false, false, .5f, .4f, .5f, .04f);
Check(visibility.Alpha == 0, "Card disappears when no longer checking the wrist");
Check(visibility.Update(true, false, true, -1, -1, .5f, 0) == 1, "Editing keeps the tracked wrist available while fitting it");
Check(visibility.Update(true, true, true, 1, 1, .5f, 0) == 0, "Quick-wheel ownership wins immediately, even during edit");
Check(visibility.Update(false, false, true, 1, 1, .5f, 0) == 0, "Tracking loss never leaves a floating card behind");
foreach (var distance in new[] { .05f, 2f, float.NaN })
{
    visibility.Reset();
    for (var i = 0; i < 5; i++)
        visibility.Update(true, false, false, 1, 1, distance, .1f);
    Check(visibility.Alpha == 0, "Cards inside the face or outside usable reach stay hidden");
}
foreach (var pair in new[] { (.5f, 1f), (1f, .1f), (float.NaN, 1f), (1f, float.PositiveInfinity) })
{
    visibility.Reset();
    for (var i = 0; i < 5; i++)
        visibility.Update(true, false, false, pair.Item1, pair.Item2, .5f, .1f);
    Check(visibility.Alpha == 0, "Both gaze and wrist face must agree, with finite inputs");
}

var directory = Path.Combine(Path.GetTempPath(), "novr-wrist-widgets-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var path = Path.Combine(directory, "hud.cfg");
    var config = new ConfigFile(path, false) { SaveOnConfigSet = false };
    var profiles = new HudWidgetProfiles(config);
    Check(profiles.Load(null).Values.All(p => p.Anchor == HudWidgetAnchor.Cockpit), "Wrist attachment remains opt-in for every widget");
    foreach (var aircraft in new[] { "FS-20", "mod-author/aircraft" })
        for (var slot = 1; slot <= 3; slot++)
        {
            var layout = profiles.Load(aircraft, slot);
            var pose = layout["map"];
            pose.Position = new(.13f * slot, -.4f, .6f);
            pose.Euler = new(-15, 25, 0);
            pose.Scale = .8f;
            pose.Anchor = slot == 2 ? HudWidgetAnchor.RightWrist : HudWidgetAnchor.LeftWrist;
            pose.WristPosition = new(.02f * slot, -.03f, .04f);
            pose.WristEuler = new(12, 27, -16);
            pose.WristScale = .55f;
            pose.Aspect = 1.4f;
            layout["map"] = pose;
            profiles.Save(aircraft, layout, slot);
        }
    profiles = new HudWidgetProfiles(new ConfigFile(path, false) { SaveOnConfigSet = false });
    foreach (var aircraft in new[] { "FS-20", "mod-author/aircraft" })
        for (var slot = 1; slot <= 3; slot++)
        {
            var pose = profiles.Load(aircraft, slot)["map"];
            Near(pose.Position, new(.13f * slot, -.4f, .6f), "Cockpit location survives each wrist attachment and config reload");
            Near(pose.Euler, new(-15, 25, 0), "Cockpit angle remains independent of wrist angle");
            Check(Math.Abs(pose.Scale - .8f) < .00001f && pose.WristScale == .55f, "Cockpit and wrist size are independent");
            Near(pose.WristPosition, new(.02f * slot, -.03f, .04f), "Wrist offset persists per aircraft and slot");
            Near(pose.WristEuler, new(12, 27, -16), "Wrist orientation survives roll from hand movement");
            Check(pose.Anchor == (slot == 2 ? HudWidgetAnchor.RightWrist : HudWidgetAnchor.LeftWrist), "Chosen hand survives reload");
            Check(Math.Abs(pose.Aspect - 1.4f) < .0001f, "Map aspect ratio survives extended encoding");
        }
    Check(!profiles.HasSavedLayout("different-aircraft") && profiles.Load("different-aircraft")["map"].Anchor == HudWidgetAnchor.Cockpit,
        "Wrist placement never leaks to another aircraft");
    profiles.ResetAircraft("FS-20", 2);
    Check(profiles.Load("FS-20", 2)["map"].Anchor == HudWidgetAnchor.Cockpit && profiles.Load("FS-20", 1)["map"].Anchor == HudWidgetAnchor.LeftWrist,
        "Reset affects only its own aircraft slot");

    foreach (var raw in new[] { ".2|-.4|.7|-10|23|0|.8|.9|.2|1", ".2|-.4|.7|-10|23|0|.8|.9|.2|1|1.6", ".2|-.4|.7|-10|23|0|.8|.9|.2|1|1.6|bad|0|0|0|0|0|0|.6" })
    {
        var section = "VR HUD Widgets " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw))).Substring(0, 32);
        config.Bind(section, "Use aircraft layout", true).Value = true;
        config.Bind(section, "Compact reachable defaults applied", true).Value = true;
        config.Bind(section, "map", raw).Value = raw;
        config.Save();
        profiles = new HudWidgetProfiles(new ConfigFile(path, false) { SaveOnConfigSet = false });
        var pose = profiles.Load(raw)["map"];
        Near(pose.Position, new(.2f, -.4f, .7f), "Legacy/malformed attachment preserves the entire cockpit location");
        Check(pose.Anchor == HudWidgetAnchor.Cockpit, "10/11-field layouts and malformed attachment data stay cockpit-fixed");
    }
}
finally { Directory.Delete(directory, true); }

TrackedControllers.Left = new() { Valid = true, Position = new(-.25f, -.25f, .4f), Rotation = Quaternion.identity };
TrackedControllers.Right = new() { Valid = true, Position = new(.25f, -.25f, .4f), Rotation = Quaternion.identity };
var manager = new CockpitHudWidgets();
var original = manager.Stored("map");
manager.SetAnchor("map", HudWidgetAnchor.LeftWrist);
var frame = manager.Frame("map");
Near(frame.position, new(-.295f, -.25f, .31f), "Left card sits above the dorsal wrist, behind the controller grip");
Near(frame.rotation * Vector3.back, Vector3.left, "Left wrist card faces away from the palm");
Near(frame.rotation * new Vector3(0, 1, 0), Vector3.forward, "Card text points toward the fingers");
Near(manager.Point("map", frame.position), Vector3.zero, "Touch geometry uses the same wrist frame as rendering");
Near(manager.Rotation("map", frame.rotation) * Vector3.forward, Vector3.forward, "Grip orientation is transformed into the same wrist frame");
var active = manager.Active("map");
active.Position = new(.03f, .04f, .05f);
active.Euler = new(5, 15, 0);
active.Scale = .75f;
manager.Edit("map", active);
Near(manager.Stored("map").Position, original.Position, "Moving a wrist card does not overwrite its cockpit location");
Near(manager.Stored("map").Euler, original.Euler, "Tilting a wrist card does not overwrite its cockpit angle");
Check(manager.Stored("map").Scale == original.Scale && manager.Stored("map").WristScale == .75f, "Resizing a wrist card retains cockpit size");
manager.SetAnchor("map", HudWidgetAnchor.Cockpit);
Near(manager.Active("map").Position, original.Position, "Returning to cockpit restores the previous placement");
manager.SetAnchor("map", HudWidgetAnchor.RightWrist);
Near(manager.Frame("map").rotation * Vector3.back, Vector3.right, "Right wrist mounting mirrors the dorsal face");
manager.SetAnchor("map", HudWidgetAnchor.LeftWrist);
frame = manager.Frame("map");
manager.Freeze("map");
TrackedControllers.Left.Position += new Vector3(.1f, .02f, -.03f);
Near(manager.Frame("map").position, frame.position, "A grabbed wrist card freezes its frame for stable two-hand fitting");
var displayed = frame.position + frame.rotation * manager.Active("map").Position;
manager.Release();
var released = manager.Frame("map");
Near(released.position + released.rotation * manager.Active("map").Position, displayed, "Release rebases onto the moving wrist without jumping");
// Rotating the anchor while fitting introduces a legitimate local roll. A
// subsequent grip with no movement must preserve that complete orientation.
manager.Freeze("map");
var beforeTurn = manager.Frame("map");
var worldBeforeTurn = beforeTurn.rotation * Quaternion.Euler(manager.Active("map").Euler);
TrackedControllers.Left.Rotation = Quaternion.Euler(new Vector3(17, 24, 31));
manager.Release();
var afterTurn = manager.Frame("map");
var regrip = manager.Active("map");
var displayedAfterTurn = afterTurn.rotation * Quaternion.Euler(regrip.Euler);
Near(displayedAfterTurn * Vector3.forward, worldBeforeTurn * Vector3.forward, "Releasing after anchor rotation preserves card direction");
Near(displayedAfterTurn * new Vector3(0, 1, 0), worldBeforeTurn * new Vector3(0, 1, 0), "Releasing after anchor rotation preserves card roll");
var grip = Quaternion.Euler(new Vector3(-27, 41, 63));
var unchanged = HudWidgetRotationMath.Drag(regrip.Euler, grip, grip, regrip.Euler, true);
Near(Quaternion.Euler(unchanged) * Vector3.forward, Quaternion.Euler(regrip.Euler) * Vector3.forward, "Regrabbing a rotated wrist card does not change its forward direction");
Near(Quaternion.Euler(unchanged) * new Vector3(0, 1, 0), Quaternion.Euler(regrip.Euler) * new Vector3(0, 1, 0), "Regrabbing retains wrist-local roll without introducing a visual snap");
var corners = Quaternion.Euler(regrip.Euler);
var leftCorner = regrip.Position + corners * new Vector3(-.08f, -.07f, 0);
var rightCorner = regrip.Position + corners * new Vector3(.08f, .07f, 0);
var resizePair = new HudWidgetTwoHandGesture(regrip, leftCorner, rightCorner, grip, grip, true);
var still = resizePair.Move(regrip, leftCorner, rightCorner, grip, grip);
Near(still.Position, regrip.Position, "Unchanged second corner acquisition preserves wrist card position");
Near(Quaternion.Euler(still.Euler) * new Vector3(0, 1, 0), Quaternion.Euler(regrip.Euler) * new Vector3(0, 1, 0), "Two-hand resize retains full captured wrist orientation");
manager.Freeze("map");
TrackedControllers.Left.Position += new Vector3(.05f, 0, 0);
var beforeAnchorSwitch = manager.Frame("map").position + manager.Frame("map").rotation * manager.Active("map").Position;
manager.SetAnchor("map", HudWidgetAnchor.Cockpit);
manager.SetAnchor("map", HudWidgetAnchor.LeftWrist);
Near(manager.Frame("map").position + manager.Frame("map").rotation * manager.Active("map").Position, beforeAnchorSwitch,
    "Changing anchor while held retains the freshly rebased wrist offset");
Time.unscaledTime = 1;
Check(manager.Alpha("map") == 1, "Edit mode keeps a valid wrist card available for fitting");
NativeHandOptionWheel.Left = true;
Check(manager.Alpha("map") == 0, "Actual same-hand quick wheel suppresses the wrist card immediately");
NativeHandOptionWheel.Left = false;
NativeHandOptionWheel.Right = true;
Check(manager.Alpha("map") == 1, "Other-hand quick wheel does not suppress the card");
NativeHandOptionWheel.Right = false;
WristMenu.IsOpen = true;
Check(manager.Alpha("map") == 1, "Layout settings can remain open while fitting a wrist card");
manager._editing = false;
Check(manager.Alpha("map") == 0, "Normal VR menu hides wrist content while open");
WristMenu.IsOpen = false;
TrackedControllers.Left.Valid = true;
manager.Freeze("map");
Check(manager.HasContact, "Opposite hand can hold a wrist card during editing");
TrackedControllers.Left.Valid = false;
Check(!manager.FrameReady("map"), "An untracked attachment cannot keep offering its frozen edit frame");
manager.CancelUntracked();
Check(!manager.HasContact, "Losing the anchor hand releases the other hand's invisible card grab");
manager._editing = true;
Check(manager.Alpha("map") == 0, "Controller loss overrides edit-mode visibility");
Console.WriteLine($"Wrist widget checks: {checks} passed");
