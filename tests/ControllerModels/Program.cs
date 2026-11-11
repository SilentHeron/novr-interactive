using NOVR.VrCamera;
using NOVR.VrUi.Controllers;

var checks = 0;
void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
bool Near(float a, float b, float tolerance = .00002f) => Math.Abs(a - b) <= tolerance;
bool Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return true; } return false; }

// ---- SteamVR's description files: JSON with comments and trailing commas ----
var parsed = (Dictionary<string, object?>)MiniJson.Parse("""
    // a comment
    { "a": [1, -2.5, 3e2,], "b": { "c": "x\ny \"q\" \u0041", }, /* block */ "t": true, "f": false, "n": null }
    """)!;
var numbers = (List<object?>)parsed["a"]!;
Check(numbers.Count == 3 && (double)numbers[0]! == 1 && (double)numbers[1]! == -2.5 && (double)numbers[2]! == 300, "Numbers and a trailing comma in an array");
Check((string)((Dictionary<string, object?>)parsed["b"]!)["c"]! == "x\ny \"q\" A", "String escapes and a trailing comma in an object");
Check((bool)parsed["t"]! && !(bool)parsed["f"]! && parsed["n"] == null, "Literals");
foreach (var bad in new[] { "", "{", "[1,", "{\"a\" 1}", "{\"a\":}", "nope", "{} extra", "\"open", "/* open", "{\"a\":1e999}" })
    Check(Throws<FormatException>(() => MiniJson.Parse(bad)), "Malformed JSON is refused: " + bad);
Check(Throws<FormatException>(() => MiniJson.Parse(new string('[', 200) + new string(']', 200))), "Deep nesting is refused");

// ---- OBJ geometry ----
var quad = ObjMesh.Parse(new StringReader("""
    # comment
    o part
    v 0 0 0
    v 1 0 0
    v 1 1 0
    v 0 1 0
    vt 0 0
    vt 1 0
    vt 1 1
    vt 0 1
    vn 0 0 1
    f 1/1/1 2/2/1 3/3/1 4/4/1
    f -4/-4 -3/-3 -2/-2
    f 1 2 3
    """));
Check(quad.Triangles.Length == 12, "A quad is fanned into two triangles; plain triangles are kept");
Check(quad.Triangles.Take(6).SequenceEqual(new[] { 0, 1, 2, 0, 2, 3 }), "Fan order follows the face");
Check(quad.VertexCount == 7, "Corners that share position and texture coordinate are one vertex; a corner without one is its own");
Check(quad.Triangles.Skip(6).Take(3).SequenceEqual(new[] { 0, 1, 2 }), "Negative indices count back from the latest element");
Check(quad.Uvs[2] == 1 && quad.Uvs[3] == 0 && quad.Uvs[quad.Uvs.Length - 1] == 0, "Texture coordinates follow their vertex; missing ones are zero");
Check(Throws<InvalidDataException>(() => ObjMesh.Parse(new StringReader("v 0 0 0\nf 1 2 3"))), "A face index past the end is refused");
Check(Throws<InvalidDataException>(() => ObjMesh.Parse(new StringReader("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 0 1 2"))), "Index zero is refused");
Check(Throws<InvalidDataException>(() => ObjMesh.Parse(new StringReader("v 0 0 0\nv 1 0 0"))), "A model with no faces is refused");
Check(Throws<InvalidDataException>(() => ObjMesh.Parse(new StringReader("v 0 0 nan\nv 1 0 0\nv 0 1 0\nf 1 2 3"))), "Bad numbers are refused");
Check(Throws<InvalidDataException>(() => ObjMesh.Parse(new StringReader("v 0 0 0\nv 1 0 0\nv 0 1 0\nv 1 1 0\nf 1 2 3\nf 2 3 4"), 3)), "The vertex limit is enforced");

// ---- Grip space, checked against the offsets the quick wheel already uses ----
// Values as SteamVR ships them for the left Frame and left Quest 2 controllers.
const string frameLeft = """
    { "thumbnail": "t.png", "components": {
      "body": { "filename": "b.obj", "component_local": { "origin": [0.0, 0.0, 0.0], "rotate_xyz": [0.0, 0.0, 0.0] } },
      "thumbstick": { "filename": "s.obj", "motion": { "type": "joystick" }, "component_local": { "origin": [0.024709, 0.009419, 0.046381], "rotate_xyz": [51.7053, -2.1218, -8.2871] } },
      "trigger": { "filename": "t.obj", "component_local": { "origin": [0.005987, -0.046922, 0.050916], "rotate_xyz": [15.8758, -29.5434, -115.0083] } },
      "tip": { "component_local": { "origin": [0.012694, -0.02522, 0.020687], "rotate_xyz": [-40, 0.0, 0.0] } },
      "openxr_grip": { "component_local": { "origin": [-0.003117, -0.004277, 0.099501], "rotate_xyz": [2.8091, 0.0, 0.0] } }
    } }
    """;
const string questLeft = """
    { "components": {
      "thumbstick": { "filename": "s.obj", "component_local": { "origin": [0.000587, 0.001502, 0.045776], "rotate_xyz": [53.0, 0.0, -7.0] } },
      "button_x": { "filename": "x.obj", "component_local": { "origin": [0.012733, 0.005519, 0.061359], "rotate_xyz": [53.0, 0.0, -7.0] } },
      "openxr_grip": { "component_local": { "origin": [0.007, -0.00182941, 0.1019482], "rotate_xyz": [20.6, 0.0, 0.0] } },
      "handgrip": { "component_local": { "origin": [0.0, 0.003, 0.097], "rotate_xyz": [5.037, 0.0, 0.0] } }
    } }
    """;
var frame = RenderModelInfo.Parse(frameLeft);
Check(frame.Parts.Select(p => p.Name).SequenceEqual(new[] { "body", "thumbstick", "trigger" }), "Only parts with geometry are listed; pose markers are not buttons");
var frameStick = frame.Parts.Single(p => p.Name == "thumbstick");
// HandOptionWheelPose: Frame cap = (side * .027826, .0110762, .0537274), left side = +1.
Check(Near(frameStick.X, .027826f) && Near(frameStick.Y, .0110762f) && Near(frameStick.Z, .0537274f),
    "Frame thumbstick lands where the quick wheel already places itself");
var quest = RenderModelInfo.Parse(questLeft);
var questStick = quest.Parts.Single(p => p.Name == "thumbstick");
// HandOptionWheelPose: Touch cap = (-side * .006413, -.0166453, .0537527).
Check(Near(questStick.X, -.006413f) && Near(questStick.Y, -.0166453f) && Near(questStick.Z, .0537527f),
    "Quest thumbstick lands where the quick wheel already places itself");
frame.ToGrip(-0.003117f, -0.004277f, 0.099501f, out var gx, out var gy, out var gz);
Check(Near(gx, 0) && Near(gy, 0) && Near(gz, 0), "The grip pose itself is the origin of grip space");
frame.ToGrip(-0.003117f, -0.004277f, 0.099501f - 1f, out gx, out gy, out gz);
Check(Near(gx, 0) && gz > .99f && gy < 0, "The model's -Z (toward the tip) is forward in grip space, pitched by the grip angle");
Check(Throws<FormatException>(() => RenderModelInfo.Parse("{ \"components\": { \"body\": { \"filename\": \"b.obj\", \"component_local\": { \"origin\": [0,0,0], \"rotate_xyz\": [0,0,0] } } } }")),
    "A model with no OpenXR grip pose is refused, not guessed");
Check(Throws<FormatException>(() => RenderModelInfo.Parse(questLeft.Replace("[20.6, 0.0, 0.0]", "[20.6, 5.0, 0.0]"))),
    "A grip pose rotated on more than one axis is refused, not misplaced");
Check(Throws<FormatException>(() => RenderModelInfo.Parse("[1, 2]")) && Throws<FormatException>(() => RenderModelInfo.Parse("{}")), "Other JSON is refused");

// ---- Finding SteamVR's models ----
Check(SteamVrRenderModels.ModelName(ControllerModelFamily.SteamFrame, true) == "frame_controller_left" &&
      SteamVrRenderModels.ModelName(ControllerModelFamily.Quest2, false) == "oculus_quest2_controller_right" &&
      SteamVrRenderModels.ModelName(ControllerModelFamily.QuestPlus, true) == "oculus_quest_plus_controller_left" &&
      SteamVrRenderModels.ModelName(ControllerModelFamily.QuestPro, false) == "oculus_quest_pro_controller_right" &&
      SteamVrRenderModels.ModelName(ControllerModelFamily.None, true) == null, "Each controller family names SteamVR's own model for that hand");
Check(SteamVrRenderModels.OpenVrRuntimes("{ \"runtime\": [ \"C:\\\\Program Files (x86)\\\\Steam\\\\steamapps\\\\common\\\\SteamVR\" ], \"version\": 1 }")
    .SequenceEqual(new[] { "C:\\Program Files (x86)\\Steam\\steamapps\\common\\SteamVR" }), "OpenVR's path file lists the SteamVR folder");
Check(SteamVrRenderModels.OpenVrRuntimes("not json").Count == 0 && SteamVrRenderModels.OpenVrRuntimes("{\"runtime\": 3}").Count == 0, "A damaged path file yields nothing");
Check(SteamVrRenderModels.LibraryFolders("\"libraryfolders\"\n{\n \"0\"\n {\n  \"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n }\n \"1\"\n {\n  \"path\"\t\t\"D:\\\\Games\"\n }\n}")
    .SequenceEqual(new[] { "C:\\Program Files (x86)\\Steam", "D:\\Games" }), "Steam's library list is read");
Check(SteamVrRenderModels.FromLinuxPath("/home/p/.local/share/Steam") == "Z:\\home\\p\\.local\\share\\Steam" &&
      SteamVrRenderModels.FromLinuxPath("C:\\Steam") == "C:\\Steam" && SteamVrRenderModels.FromLinuxPath("") == null && SteamVrRenderModels.FromLinuxPath(null) == null,
    "A Linux path from Proton is reached through drive Z");
var listed = SteamVrRenderModels.ModelDirectories("root", "frame_controller_left", d => new[] { Path.Combine(d, "frame_controller"), Path.Combine(d, "htc") }).ToArray();
Check(listed.Length == 3 && listed[0] == Path.Combine("root", "resources", "rendermodels", "frame_controller_left") &&
      listed[1] == Path.Combine("root", "drivers", "frame_controller", "resources", "rendermodels", "frame_controller_left"),
    "A model is looked for in the shared folder first, then in each driver's folder");
Check(SteamVrRenderModels.ModelDirectories("root", "x", _ => throw new IOException()).Count() == 1, "An unreadable drivers folder still leaves the shared folder");
// Only names that are refused before the disk is touched: a real name may be
// found on a machine that has SteamVR installed, which is not a failure.
Check(SteamVrRenderModels.Find("../escape", "/nowhere/Game_Data") == null && SteamVrRenderModels.Find("", "/nowhere/Game_Data") == null &&
      SteamVrRenderModels.Find("a/b", "/nowhere/Game_Data") == null, "A model name that is not a plain folder name is refused");
var found = SteamVrRenderModels.Find("frame_controller_left", "/nowhere/Game_Data");
Check(found == null || File.Exists(Path.Combine(found, "frame_controller_left.obj")), "A real name is either not found or points at a folder that holds the model");

// ---- Labels: what the buttons do on this screen, and nothing about flying ----
string Detail(List<ControllerCallout> list, string part) => list.Single(c => c.Part == part).Detail;
var touchParts = new[] { "body", "trigger", "button_a", "button_b", "button_x", "button_y", "thumbstick", "button_grip", "button_enter" };
var frameParts = new[] { "thumbstick", "dpad", "button_a", "button_b", "button_x", "button_y", "button_grip", "trigger", "button_menu", "button_view", "button_system", "bumper" };
foreach (var parts in new[] { touchParts, frameParts })
    foreach (var left in new[] { false, true })
    {
        Check(ControllerCallouts.Build(parts, ControllerHintContext.None, left, true).Count == 0 &&
              ControllerCallouts.Build(parts, ControllerHintContext.None, left, false).Count == 0, "No screen, no labels");
        var menuPointer = ControllerCallouts.Build(parts, ControllerHintContext.Menus, left, true);
        var menuOther = ControllerCallouts.Build(parts, ControllerHintContext.Menus, left, false);
        Check(menuPointer.Count == 2 && Detail(menuPointer, "trigger") == "Select" && Detail(menuPointer, "thumbstick") == "Up / down: scroll",
            "Menus: the pointer hand selects and scrolls, and that is all");
        Check(menuOther.Count == 1 && Detail(menuOther, "trigger") == "Take the pointer", "Menus: the other hand's trigger takes the pointer, and that is all");
        var walk = ControllerCallouts.Build(parts, ControllerHintContext.AircraftSelection, left, false);
        var point = ControllerCallouts.Build(parts, ControllerHintContext.AircraftSelection, left, true);
        Check(walk.Count == 2 && Detail(walk, "trigger") == "Take the pointer" &&
              Detail(walk, "thumbstick").StartsWith("Push, release: TELEPORT\nPull back: bring menu here\n") && Detail(walk, "thumbstick").EndsWith("Click: quick wheel"),
            "Aircraft selection: the walking hand's thumbstick calls out teleport, bringing the menu and the wheel");
        Check(point.Count == 2 && Detail(point, "trigger") == "Select" && Detail(point, "thumbstick").StartsWith("Up / down: scroll\n") &&
              !Detail(point, "thumbstick").Contains("TELEPORT") && Detail(point, "thumbstick").EndsWith("Click: quick wheel"),
            "Aircraft selection: the pointer hand scrolls and opens the wheel; it does not walk");
        Check(Detail(walk, "thumbstick").Contains("Left / right: snap turn") == left && Detail(point, "thumbstick").Contains("Left / right: snap turn") == left,
            "Snap turn is called out on the left thumbstick only, whichever hand holds the pointer");
        foreach (var list in new[] { menuPointer, menuOther, walk, point })
            Check(list.All(c => c.Part is "trigger" or "thumbstick") && list.All(c => c.Title is "Trigger" or "Thumbstick"),
                "Face buttons, grips and bumpers get no label on these screens: there is nothing to fly");
    }
Check(ControllerCallouts.Lines(new ControllerCallout("trigger", "Trigger", "Select")) == 2 &&
      ControllerCallouts.Lines(ControllerCallouts.Build(touchParts, ControllerHintContext.AircraftSelection, true, false).Single(c => c.Part == "thumbstick")) == 5,
    "A label is as tall as its title and lines");

// ---- Label layout ----
var anchors = new (float x, float y)[] { (-20, 40), (-18, 10), (5, 30), (25, 35), (22, -40), (0, 0), (-30, -60) };
var spots = ControllerCallouts.Layout(anchors, 200, 58);
Check(spots.Length == anchors.Length && spots.All(s => Math.Abs(Math.Abs(s.x) - 200) < .001f), "Every label sits in the left or the right column");
Check(spots.Count(s => s.x < 0) == 4 && spots.Count(s => s.x > 0) == 3, "Both columns are used");
for (var i = 0; i < anchors.Length; i++)
    for (var j = 0; j < anchors.Length; j++)
    {
        if (i == j || spots[i].x != spots[j].x)
        {
            if (i != j && spots[i].x < spots[j].x)
                Check(anchors[i].x <= anchors[j].x, "A label is on the same side as its button relative to the others");
            continue;
        }
        Check(Math.Abs(spots[i].y - spots[j].y) >= 58 - .001f, "Labels in a column never overlap");
        Check(anchors[i].y == anchors[j].y || (anchors[i].y > anchors[j].y) == (spots[i].y > spots[j].y), "Labels keep the order of their buttons, so leaders do not cross");
    }
Check(ControllerCallouts.Layout(Array.Empty<(float, float)>(), 200, 58).Length == 0, "No buttons, no labels");
var single = ControllerCallouts.Layout(new[] { (3f, 7f) }, 200, 58);
Check(single[0].x == -200 && single[0].y == 7, "A single label sits level with its button");
var oneSided = ControllerCallouts.Layout(new[] { (10f, 0f), (11f, 5f), (12f, 9f), (13f, 2f) }, 200, 58);
Check(oneSided.Count(s => s.x < 0) == 2 && oneSided.Count(s => s.x > 0) == 2, "Buttons all on one half still fill both columns");
Check(ControllerCallouts.LookedAt(.9f, .4f) && ControllerCallouts.LookedAt(.5f, .8f) && !ControllerCallouts.LookedAt(.4f, .4f) &&
      !ControllerCallouts.LookedAt(.9f, .9f) && !ControllerCallouts.LookedAt(float.NaN, .4f) && !ControllerCallouts.LookedAt(-1f, .2f),
    "Labels show only while the controller is raised and turned toward the face");

// ---- Teleport gesture ----
var gesture = new TeleportGesture();
Check(gesture.Update(true, 0, 1) == TeleportEvent.None && gesture.Update(true, 0, 1) == TeleportEvent.None && !gesture.Aiming,
    "A stick already pushed when the screen opens does not aim");
Check(gesture.Update(true, 0, 0) == TeleportEvent.None, "Centring arms it");
Check(gesture.Update(true, 0, .5f) == TeleportEvent.None && !gesture.Aiming, "A half push does not aim");
Check(gesture.Update(true, .1f, .9f) == TeleportEvent.Aim && gesture.Aiming && gesture.Update(true, .3f, .8f) == TeleportEvent.Aim, "Pushing forward aims and keeps aiming");
Check(gesture.Update(true, 0, .5f) == TeleportEvent.Aim && gesture.Aiming, "Easing off without letting go keeps the arc");
Check(gesture.Update(true, 0, .1f) == TeleportEvent.Teleport && !gesture.Aiming, "Letting go teleports");
Check(gesture.Update(true, 0, 0) == TeleportEvent.None, "Once");
Check(gesture.Idle && gesture.Update(true, 0, -.9f) == TeleportEvent.None && !gesture.Aiming && !gesture.Idle &&
      gesture.Update(true, 0, 0) == TeleportEvent.Summon && gesture.Idle, "Pull back and let go calls the menu");
Check(gesture.Update(true, .9f, .2f) == TeleportEvent.None && gesture.Update(true, -.9f, 0) == TeleportEvent.None && gesture.Update(true, 0, 0) == TeleportEvent.None,
    "Sideways pushes do nothing");
gesture.Update(true, 0, .9f);
Check(gesture.Update(true, 0, -.9f) == TeleportEvent.Cancel && !gesture.Aiming && gesture.Update(true, 0, 0) == TeleportEvent.None,
    "Swinging to the opposite side cancels, and that release is not a return");
gesture.Update(true, 0, -.9f);
Check(gesture.Update(true, 0, .9f) == TeleportEvent.Cancel && gesture.Update(true, 0, 0) == TeleportEvent.None, "The same for calling the menu");
gesture.Update(true, 0, .9f);
Check(gesture.Update(false, 0, .9f) == TeleportEvent.Cancel && gesture.Update(true, 0, .9f) == TeleportEvent.None && !gesture.Aiming,
    "A menu, the wheel or lost tracking cancels, and the held stick needs a fresh centre");
gesture.Update(true, 0, 0); gesture.Update(true, 0, .9f);
Check(gesture.Update(true, float.NaN, 0) == TeleportEvent.Cancel && gesture.Update(false, 0, 0) == TeleportEvent.None, "Bad stick values cancel safely");

// ---- Snap turn ----
var turn = new SnapTurnGesture();
Check(turn.Update(true, .9f, 0) == 0 && turn.Update(true, .9f, 0) == 0, "A stick already held over when the screen opens does not turn");
Check(turn.Update(true, 0, 0) == 0, "Centring arms it");
Check(turn.Update(true, .5f, 0) == 0, "A half push does not turn");
Check(turn.Update(true, .9f, .1f) == 1 && turn.Update(true, .9f, 0) == 0 && turn.Update(true, 1f, 0) == 0, "One flick right is one turn right, however long it is held");
Check(turn.Update(true, .5f, 0) == 0 && turn.Update(true, .9f, 0) == 0, "The stick has to come back toward the centre before the next turn");
Check(turn.Update(true, .2f, 0) == 0 && turn.Update(true, -.8f, 0) == -1 && turn.Update(true, -.8f, 0) == 0, "Then a flick left is one turn left");
turn.Update(true, 0, 0);
Check(turn.Update(true, .6f, .9f) == 0 && turn.Update(true, .75f, -.9f) == 0 && turn.Update(true, -.75f, .8f) == 0,
    "A forward push or a pull back with some sideways drift is not a turn");
Check(turn.Update(true, .8f, -.3f) == 1, "A mostly sideways flick still is");
turn.Update(true, 0, 0);
Check(turn.Update(false, .9f, 0) == 0 && turn.Update(true, .9f, 0) == 0, "A menu, the wheel, an aimed teleport or lost tracking blocks it, and the held stick needs a fresh centre");
turn.Update(true, 0, 0);
Check(turn.Update(true, float.NaN, 0) == 0 && turn.Update(true, .9f, 0) == 0 && turn.Update(true, 0, float.PositiveInfinity) == 0, "Bad stick values are ignored safely");
turn.Update(true, 0, 0);
turn.Reset();
Check(turn.Update(true, .9f, 0) == 0, "Leaving the screen disarms it");

// ---- Arc and boundary ----
TeleportArc.Point(1, 2, 3, 0, 0, 1, 0, out var ax, out var ay, out var az);
Check(ax == 1 && ay == 2 && az == 3, "The arc starts at the hand");
var s45 = (float)Math.Sqrt(.5);
var landed = -1;
for (var step = 1; step <= TeleportArc.Steps; step++)
{
    TeleportArc.Point(0, 0, 0, 0, s45, s45, step, out _, out var py, out var pz);
    if (py <= 0) { landed = step; Check(Math.Abs(pz - TeleportArc.Speed * TeleportArc.Speed / TeleportArc.Gravity) < 1f, "A 45 degree throw lands at its ballistic range"); break; }
}
Check(landed > 0, "The arc is long enough to come back down from a 45 degree throw");
TeleportArc.Point(0, 0, 0, 0, -1, 0, 4, out _, out var down, out _);
Check(down < -TeleportArc.Speed * 4 * TeleportArc.StepSeconds, "Aiming down, the arc falls faster than the throw alone");
Check(TeleportArc.BoundaryRadius(2) == TeleportArc.MinRadius && TeleportArc.BoundaryRadius(500) == TeleportArc.MaxRadius &&
      Near(TeleportArc.BoundaryRadius(10), 22) && TeleportArc.BoundaryRadius(float.NaN) == TeleportArc.MinRadius, "The boundary scales with the aircraft's stand-off, within limits");
float bx = 30, bz = 40;
Check(TeleportArc.ClampToBoundary(ref bx, ref bz, 0, 0, 25) && Near(bx, 15) && Near(bz, 20), "A spot outside the boundary is pulled back onto it, along the same bearing");
bx = 3; bz = 4;
Check(!TeleportArc.ClampToBoundary(ref bx, ref bz, 0, 0, 25) && bx == 3 && bz == 4, "A spot inside the boundary is left alone");
bx = 110; bz = 100;
Check(TeleportArc.ClampToBoundary(ref bx, ref bz, 100, 100, 5) && Near(bx, 105) && Near(bz, 100), "The boundary is centred on the aircraft, not the world origin");

Console.WriteLine($"{checks} controller model, label and teleport checks passed (file formats, grip alignment, bindings, layout and gesture; no rendering or headset claim).");
