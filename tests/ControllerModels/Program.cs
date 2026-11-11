using NOVR.MotionControls;
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

// ---- A button's own mesh, and where a label's line ends on it ----
Check(frame.Parts.Single(p => p.Name == "thumbstick").File.EndsWith(".obj") && frame.Parts.All(p => p.File.Length > 0), "Each part names its own mesh file");
// Identity grip: model coordinates map to grip space with only Z mirrored.
var plain = RenderModelInfo.Parse("""
    { "components": {
      "button_a": { "filename": "a.obj", "component_local": { "origin": [0, 0, 0], "rotate_xyz": [0, 0, 0] } },
      "ring": { "filename": "ring.obj", "component_local": { "origin": [0, 0, 0], "rotate_xyz": [0, 0, 0] } },
      "gone": { "filename": "gone.obj", "component_local": { "origin": [0, 0, 0], "rotate_xyz": [0, 0, 0] } },
      "escape": { "filename": "../a.obj", "component_local": { "origin": [0, 0, 0], "rotate_xyz": [0, 0, 0] } },
      "picture": { "filename": "a.png", "component_local": { "origin": [0, 0, 0], "rotate_xyz": [0, 0, 0] } },
      "broken": { "filename": "broken.obj", "component_local": { "origin": [0, 0, 0], "rotate_xyz": [0, 0, 0] } },
      "body": { "filename": "a.obj", "component_local": { "origin": [0, 0, 0], "rotate_xyz": [0, 0, 0] } },
      "openxr_grip": { "component_local": { "origin": [0, 0, 0], "rotate_xyz": [0, 0, 0] } }
    } }
    """);
// A box in model space; grip space mirrors Z.
string Box(float cx, float cy, float cz, float bx, float by, float bz, int first = 0)
{
    var text = new System.Text.StringBuilder();
    foreach (var z in new[] { -1, 1 })
        foreach (var y in new[] { -1, 1 })
            foreach (var x in new[] { -1, 1 })
                text.AppendLine(FormattableString.Invariant($"v {cx + x * bx} {cy + y * by} {cz + z * bz}"));
    text.AppendLine("vt 0 0");
    foreach (var quad in new[] { "1 2 4 3", "5 7 8 6", "1 5 6 2", "3 4 8 7", "1 3 7 5", "2 6 8 4" })
    {
        var c = quad.Split(' ');
        var n = c.Select(corner => int.Parse(corner) + first).ToArray();
        text.AppendLine($"f {n[0]}/1 {n[1]}/1 {n[2]}/1");
        text.AppendLine($"f {n[0]}/1 {n[2]}/1 {n[3]}/1");
    }
    return text.ToString();
}
var box = ControllerPartShape.From(ObjMesh.Parse(new StringReader(Box(.01f, .02f, -.03f, .01f, .01f, .005f))), plain)!;
Check(Near(box.CentreX, .01f) && Near(box.CentreY, .02f) && Near(box.CentreZ, .03f) && Near(box.Radius, (float)Math.Sqrt(.0001 + .0001 + .000025)),
    "A part's middle and reach are taken from its own mesh, in grip space");
box.Target(.01f, .10f, .03f, out var hx, out var hy, out var hz);
Check(Near(hx, .01f) && Near(hy, .03f) && Near(hz, .03f), "A line from above ends on the top face, not in the middle of the button");
box.Target(.01f, -.10f, .03f, out hx, out hy, out hz);
Check(Near(hx, .01f) && Near(hy, .01f) && Near(hz, .03f), "A line from below ends on the bottom face");
box.Target(.11f, .02f, .03f, out hx, out hy, out hz);
Check(Near(hx, .02f) && Near(hy, .02f) && Near(hz, .03f), "A line from the side ends on the side that faces it");
box.Target(.09f, .10f, .03f, out hx, out hy, out hz);
Check(hy <= .03f + .00001f && hx <= .02f + .00001f && (Near(hy, .03f) || Near(hx, .02f)) && Near(hx - .01f, hy - .02f, .0001f),
    "A slanted line ends where it first meets the surface, on its way to the middle");
box.Target(.01f, .02f, .03f, out hx, out hy, out hz);
Check(Near(hx, .01f) && Near(hy, .02f) && Near(hz, .03f), "A line that starts in the middle stays there");
box.Target(.012f, .021f, .031f, out hx, out hy, out hz);
Check(!float.IsNaN(hx) && Math.Abs(hx - .01f) <= .0101f && Math.Abs(hy - .02f) <= .0101f && Math.Abs(hz - .03f) <= .0051f, "A line that starts inside the part ends on or in it");
foreach (var bad in new[] { float.NaN, float.PositiveInfinity })
{
    box.Target(bad, .1f, 0, out hx, out hy, out hz);
    Check(Near(hx, .01f) && Near(hy, .02f) && Near(hz, .03f), "A bad starting point falls back to the middle");
}
// A flat square ring: the middle is a hole, so the line passes through it.
var ring = ControllerPartShape.From(ObjMesh.Parse(new StringReader("""
    v -2 0 -2
    v 2 0 -2
    v 2 0 2
    v -2 0 2
    v -1 0 -1
    v 1 0 -1
    v 1 0 1
    v -1 0 1
    vt 0 0
    f 1/1 2/1 6/1
    f 1/1 6/1 5/1
    f 2/1 3/1 7/1
    f 2/1 7/1 6/1
    f 3/1 4/1 8/1
    f 3/1 8/1 7/1
    f 4/1 1/1 5/1
    f 4/1 5/1 8/1
    """)), plain)!;
ring.Target(0, 5, 0, out hx, out hy, out hz);
Check(Near(Math.Abs(hx), 1) && Near(hy, 0) && Near(Math.Abs(hz), 1), "A part with a hollow middle gets the line at its nearest edge, not in the hole");
Check(Throws<InvalidDataException>(() => ObjMesh.Parse(new StringReader("v 0 0 0\nv 1 0 0\nvt 0 0\n"))), "A file with no surface is refused before it can become a shape");
var shapeFolder = Path.Combine(Path.GetTempPath(), "novr-shapes-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(shapeFolder);
try
{
    File.WriteAllText(Path.Combine(shapeFolder, "a.obj"), Box(0, 0, 0, .01f, .01f, .01f));
    File.WriteAllText(Path.Combine(shapeFolder, "ring.obj"), Box(.05f, 0, 0, .01f, .01f, .01f));
    File.WriteAllText(Path.Combine(shapeFolder, "broken.obj"), "v 1 2\nf 1 2 3\n");
    File.WriteAllText(Path.Combine(shapeFolder, "a.png"), Box(0, 0, 0, .01f, .01f, .01f));
    var shapes = ControllerPartShape.LoadAll(shapeFolder, plain);
    Check(shapes.Keys.OrderBy(k => k).SequenceEqual(new[] { "button_a", "ring" }),
        "Parts are read from their own files; the body, a missing file, a damaged file, a path out of the folder and a non-mesh file are left out");
    Check(Near(shapes["ring"].CentreX, .05f) && Near(shapes["button_a"].CentreX, 0), "Each part keeps its own place");
    Check(ControllerPartShape.LoadAll(Path.Combine(shapeFolder, "nowhere"), plain).Count == 0, "A missing folder gives no shapes and no error");
}
finally
{
    Directory.Delete(shapeFolder, true);
}

// ---- The line's route: straight when clear, round the controller when not ----
ControllerPartShape Shape(params (float cx, float cy, float cz, float bx, float by, float bz)[] boxes)
{
    var text = new System.Text.StringBuilder();
    for (var i = 0; i < boxes.Length; i++)
        text.Append(Box(boxes[i].cx, boxes[i].cy, boxes[i].cz, boxes[i].bx, boxes[i].by, boxes[i].bz, i * 8));
    return ControllerPartShape.From(ObjMesh.Parse(new StringReader(text.ToString())), plain)!;
}
bool Point(float[] path, int index, float x, float y, float z) => Near(path[index * 3], x, .0001f) && Near(path[index * 3 + 1], y, .0001f) && Near(path[index * 3 + 2], z, .0001f);
// A deck with a button hanging under it, like a trigger under a controller's head.
var deck = (0f, 0f, 0f, .05f, .005f, .05f);
var under = (0f, -.03f, 0f, .01f, .01f, .01f);
var trigger = Shape(under);
var controller = Shape(deck, under);
Check(controller.Blocks(-.10f, .03f, 0, 0, -.03f, 0, .004f) && !controller.Blocks(-.10f, -.03f, 0, -.01f, -.03f, 0, .004f) &&
      !controller.Blocks(-.10f, .03f, 0, -.10f, -.03f, 0, 0) && !controller.Blocks(0, .03f, 0, 0, .006f, 0, .004f) && controller.Blocks(0, .03f, 0, 0, -.02f, 0, .004f),
    "A shape blocks a line that passes through it, not one that only arrives on it or passes beside it");
Check(!controller.Blocks(float.NaN, 0, 0, 0, 0, 0, .004f) && !controller.Blocks(0, .03f, 0, 0, .03f, 0, .004f), "A bad or zero-length line is not blocked");
var route = ControllerPartShape.Leader(trigger, controller, -.10f, .03f, 0, 0, 1, 0);
Check(route.Length == 9 && Point(route, 0, -.10f, .03f, 0) && Point(route, 1, -.10f, -.03f, 0) && Point(route, 2, -.01f, -.03f, 0),
    "A button under the deck is reached round the side: down beside the controller to its depth, then in to its near face");
route = ControllerPartShape.Leader(trigger, controller, -.10f, -.03f, 0, 0, 1, 0);
Check(route.Length == 6 && Point(route, 0, -.10f, -.03f, 0) && Point(route, 1, -.01f, -.03f, 0), "With nothing in the way the line is straight");
route = ControllerPartShape.Leader(trigger, null, -.10f, .03f, 0, 0, 1, 0);
Check(route.Length == 6 && Point(route, 0, -.10f, .03f, 0), "Without the whole controller's shape the line is straight");
route = ControllerPartShape.Leader(trigger, controller, -.10f, .03f, 0, 0, 0, 0);
Check(route.Length == 6, "Without a direction for the label plane the line is straight");
// A button on top between two walls taller than it, like a thumbstick inside a tracking ring.
var top = (0f, .01f, 0f, .005f, .005f, .005f);
var stick = Shape(top);
var ringed = Shape(deck, top, (-.03f, .02f, 0f, .002f, .015f, .05f), (.03f, .02f, 0f, .002f, .015f, .05f));
route = ControllerPartShape.Leader(stick, ringed, -.10f, .03f, 0, 0, 1, 0);
Check(route.Length == 9 && Point(route, 1, 0, .03f, 0) && Point(route, 2, 0, .015f, 0),
    "A button walled in on both sides is reached over the top: along the label plane to above it, then straight down onto it");
// Sealed inside a box: nothing reaches it, and the side route is kept.
var sealedIn = Shape(under, (0f, -.03f, 0f, .03f, .03f, .03f));
route = ControllerPartShape.Leader(trigger, sealedIn, -.10f, .03f, 0, 0, 1, 0);
Check(route.Length == 9 && Point(route, 1, -.10f, -.03f, 0) && Point(route, 2, -.01f, -.03f, 0), "A part nothing reaches cleanly keeps the route round the side");
var columns = ControllerCallouts.Layout(new[] { (-20f, 10f), (25f, -5f) }, -310f, 240f, 58f);
Check(columns[0].x == -310 && columns[1].x == 240 && columns[0].y == 10 && columns[1].y == -5, "Each column can stand at its own distance, to clear its side of the controller");

// ---- Pressed buttons light up ----
var everything = new ControllerPresses { Trigger = true, Grip = true, StickClick = true, Primary = true, Secondary = true, Top = true, Outside = true, Menu = true, Shoulder = true };
foreach (var isFrame in new[] { false, true })
    foreach (var left in new[] { false, true })
    {
        Check(ControllerCallouts.Pressable("trigger", isFrame, left) && ControllerCallouts.Pressable("thumbstick", isFrame, left) &&
              ControllerCallouts.Pressable("button_grip", isFrame, left), "Trigger, thumbstick and grip can light up on every controller");
        foreach (var part in new[] { "body", "status", "button_system", "nonsense", "" })
            Check(!ControllerCallouts.Pressable(part, isFrame, left) && !ControllerCallouts.Pressed(part, isFrame, left, everything),
                "A part that is not a readable button never lights up: " + part);
        Check(!new[] { "trigger", "thumbstick", "button_grip", "button_a", "button_b", "button_x", "button_y", "bumper", "dpad", "button_menu", "button_view", "button_enter" }
            .Any(part => ControllerCallouts.Pressed(part, isFrame, left, default)), "Nothing lights up while nothing is pressed");
        Check(ControllerCallouts.Pressed("trigger", isFrame, left, new ControllerPresses { Trigger = true }) &&
              ControllerCallouts.Pressed("thumbstick", isFrame, left, new ControllerPresses { StickClick = true }) &&
              ControllerCallouts.Pressed("button_grip", isFrame, left, new ControllerPresses { Grip = true }) &&
              !ControllerCallouts.Pressed("trigger", isFrame, left, new ControllerPresses { Grip = true, StickClick = true }) &&
              !ControllerCallouts.Pressed("button_grip", isFrame, left, new ControllerPresses { Trigger = true }),
            "Each of trigger, thumbstick click and grip lights its own part and no other");
    }
Check(ControllerCallouts.Pressed("button_a", false, false, new ControllerPresses { Primary = true }) && ControllerCallouts.Pressed("button_b", false, false, new ControllerPresses { Secondary = true }) &&
      ControllerCallouts.Pressed("button_x", false, true, new ControllerPresses { Primary = true }) && ControllerCallouts.Pressed("button_y", false, true, new ControllerPresses { Secondary = true }) &&
      ControllerCallouts.Pressed("button_enter", false, true, new ControllerPresses { Menu = true }) && !ControllerCallouts.Pressable("button_enter", false, false) &&
      !ControllerCallouts.Pressed("button_a", false, false, new ControllerPresses { Secondary = true }),
    "Quest: A, B, X, Y and the left menu button light up on the hand that has them; the right Meta button is the system's");
Check(ControllerCallouts.Pressed("button_a", true, false, new ControllerPresses { Primary = true }) && ControllerCallouts.Pressed("button_b", true, false, new ControllerPresses { Outside = true }) &&
      ControllerCallouts.Pressed("button_x", true, false, new ControllerPresses { Secondary = true }) && ControllerCallouts.Pressed("button_y", true, false, new ControllerPresses { Top = true }) &&
      ControllerCallouts.Pressed("bumper", true, false, new ControllerPresses { Shoulder = true }) && ControllerCallouts.Pressed("bumper", true, true, new ControllerPresses { Shoulder = true }) &&
      ControllerCallouts.Pressed("button_menu", true, false, new ControllerPresses { Menu = true }) && ControllerCallouts.Pressed("button_view", true, true, new ControllerPresses { Menu = true }) &&
      !ControllerCallouts.Pressable("bumper", false, false),
    "Frame: the four face buttons, both bumpers, Menu and View light up");
foreach (var direction in new[] { new ControllerPresses { Top = true }, new ControllerPresses { Secondary = true }, new ControllerPresses { Primary = true }, new ControllerPresses { Outside = true } })
    Check(ControllerCallouts.Pressable("dpad", true, true) && ControllerCallouts.Pressed("dpad", true, true, direction) && !ControllerCallouts.Pressed("dpad", true, false, direction) &&
          !ControllerCallouts.Pressed("dpad", false, true, direction), "Frame: any direction lights the left controller's D-pad, and only there");

// ---- SteamVR's real files, when this machine has them ----
var realModels = 0;
var realParts = 0;
var realRoutes = 0;
var realBent = 0;
foreach (var family in new[] { ControllerModelFamily.SteamFrame, ControllerModelFamily.Quest2, ControllerModelFamily.QuestPlus, ControllerModelFamily.QuestPro })
    foreach (var left in new[] { true, false })
    {
        var modelName = SteamVrRenderModels.ModelName(family, left)!;
        var folder = SteamVrRenderModels.Find(modelName, Environment.GetEnvironmentVariable("NOVR_TEST_GAME_DATA") ?? "/nowhere/Game_Data");
        if (folder == null)
            continue;
        var real = RenderModelInfo.Parse(File.ReadAllText(Path.Combine(folder, modelName + ".json")));
        var realShapes = ControllerPartShape.LoadAll(folder, real);
        realModels++;
        foreach (var part in real.Parts)
        {
            if (!ControllerCallouts.Pressable(part.Name, family == ControllerModelFamily.SteamFrame, left))
                continue;
            Check(realShapes.TryGetValue(part.Name, out var shape), $"{modelName}: {part.Name} has a mesh of its own");
            realParts++;
            // A button is a few centimetres at most, and its mesh sits on its origin.
            Check(shape!.Radius > .001f && shape.Radius < .06f, $"{modelName}: {part.Name} is button-sized ({shape.Radius * 1000:0.0} mm)");
            var apart = Math.Sqrt(Math.Pow(shape.CentreX - part.X, 2) + Math.Pow(shape.CentreY - part.Y, 2) + Math.Pow(shape.CentreZ - part.Z, 2));
            Check(apart < .03, $"{modelName}: {part.Name}'s mesh is where its description puts it ({apart * 1000:0.0} mm apart)");
            // A label about where the game puts one (beside the head, above the deck): the route ends on the part.
            var whole = ControllerPartShape.From(ObjMesh.Parse(new StringReader(File.ReadAllText(Path.Combine(folder, modelName + ".obj")))), real)!;
            foreach (var sideways in new[] { -.09f, .09f })
            {
                var path = ControllerPartShape.Leader(shape, whole, whole.CentreX + sideways, shape.CentreY + .06f, shape.CentreZ, 0, .75f, .66f);
                var last = path.Length - 3;
                var off = Math.Sqrt(Math.Pow(path[last] - shape.CentreX, 2) + Math.Pow(path[last + 1] - shape.CentreY, 2) + Math.Pow(path[last + 2] - shape.CentreZ, 2));
                Check((path.Length == 6 || path.Length == 9) && off <= shape.Radius + .0001, $"{modelName}: {part.Name}'s route ends on the part");
                if (path.Length == 9)
                    realBent++;
                realRoutes++;
            }
            // From every side, a label's line ends on the part's surface, on the near side of its middle.
            foreach (var (dx, dy, dz) in new[] { (1f, 0f, 0f), (-1f, 0f, 0f), (0f, 1f, 0f), (0f, -1f, 0f), (0f, 0f, 1f), (0f, 0f, -1f), (.6f, .6f, .5f), (-.6f, .7f, -.4f) })
            {
                float fx = shape.CentreX + dx * .08f, fy = shape.CentreY + dy * .08f, fz = shape.CentreZ + dz * .08f;
                shape.Target(fx, fy, fz, out var tx, out var ty, out var tz);
                var reach = Math.Sqrt(Math.Pow(tx - shape.CentreX, 2) + Math.Pow(ty - shape.CentreY, 2) + Math.Pow(tz - shape.CentreZ, 2));
                var travelled = Math.Sqrt(Math.Pow(tx - fx, 2) + Math.Pow(ty - fy, 2) + Math.Pow(tz - fz, 2));
                var toMiddle = Math.Sqrt(Math.Pow(shape.CentreX - fx, 2) + Math.Pow(shape.CentreY - fy, 2) + Math.Pow(shape.CentreZ - fz, 2));
                Check(reach <= shape.Radius + .0001 && travelled <= toMiddle + shape.Radius + .0001, $"{modelName}: {part.Name}'s line ends on the part");
                var nearest = double.PositiveInfinity;
                for (var i = 0; i + 2 < shape.Positions.Length; i += 3)
                    nearest = Math.Min(nearest, Math.Sqrt(Math.Pow(shape.Positions[i] - tx, 2) + Math.Pow(shape.Positions[i + 1] - ty, 2) + Math.Pow(shape.Positions[i + 2] - tz, 2)));
                // A hit lies inside a triangle, so on a coarse mesh (a grip paddle) it can be several millimetres from any corner.
                Check(nearest < Math.Max(.006, shape.Radius * .7), $"{modelName}: {part.Name}'s line ends on its surface ({nearest * 1000:0.0} mm from the nearest vertex)");
            }
        }
    }
Console.WriteLine(realModels == 0 ? "SteamVR's controller files are not on this machine; real-model checks skipped."
    : $"Real SteamVR files: {realModels} models, {realParts} pressable parts, each with a mesh and line ends on its surface; {realBent} of {realRoutes} sample routes go round the controller.");

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
// ---- The full-screen map: its thumbsticks and the button that closes it ----
ControllerActionBinding Defaults(ControllerBindingProfile profile, ControllerActionId action) => ControllerBindingDefaults.Get(profile, action);
foreach (var isFrame in new[] { false, true })
{
    var mapParts = isFrame ? frameParts : touchParts;
    var mapProfile = isFrame ? ControllerBindingProfile.Frame : ControllerBindingProfile.Touch;
    var mapRight = ControllerCallouts.Build(mapParts, ControllerHintContext.Map, false, true, isFrame, false, a => Defaults(mapProfile, a));
    var mapLeft = ControllerCallouts.Build(mapParts, ControllerHintContext.Map, true, false, isFrame, false, a => Defaults(mapProfile, a));
    Check(Detail(mapRight, "trigger") == "Select" && Detail(mapLeft, "trigger") == "Take the pointer", "Map: the trigger selects, the other hand's takes the pointer");
    Check(Detail(mapLeft, "thumbstick") == "Move map\nClick: quick wheel" && Detail(mapRight, "thumbstick") == "Up / down: zoom map\nClick: quick wheel",
        "Map: the left thumbstick moves it and the right one zooms, as the game's map patch has it");
    // The default layout puts Map on B of the stick hand.
    var close = mapRight.Single(c => c.Detail == "Close map");
    Check(close.Part == "button_b" && close.Title == "B" && mapLeft.All(c => c.Detail != "Close map") && mapRight.Count == 3 && mapLeft.Count == 2,
        "Map: the button bound to Map is labelled Close map, on the hand it is bound to, and nothing else is labelled");
    var mirroredRight = ControllerCallouts.Build(mapParts, ControllerHintContext.Map, false, true, isFrame, true, a => Defaults(mapProfile, a));
    var mirroredLeft = ControllerCallouts.Build(mapParts, ControllerHintContext.Map, true, false, isFrame, true, a => Defaults(mapProfile, a));
    Check(Detail(mirroredLeft, "thumbstick").StartsWith("Up / down: zoom map") && Detail(mirroredRight, "thumbstick").StartsWith("Move map"),
        "Map: a left-handed cockpit swaps which thumbstick zooms");
    Check(mirroredRight.All(c => c.Detail != "Close map") && mirroredLeft.Count(c => c.Detail == "Close map") == 1,
        "Map: and the close button moves to the left controller with the stick hand");
    var rebound = ControllerCallouts.Build(mapParts, ControllerHintContext.Map, false, true, isFrame, false, a =>
        a == ControllerActionId.Map ? new ControllerActionBinding(ControllerButton.Primary, ControllerHandRole.Right) :
        new ControllerActionBinding(ControllerButton.None, ControllerHandRole.Stick));
    Check(rebound.Single(c => c.Detail == "Close map").Part == "button_a", "Map: the label follows a rebound Map button");
    var unbound = ControllerCallouts.Build(mapParts, ControllerHintContext.Map, false, true, isFrame, false,
        _ => new ControllerActionBinding(ControllerButton.None, ControllerHandRole.Stick));
    Check(unbound.Count == 2 && unbound.All(c => c.Part is "trigger" or "thumbstick"), "Map: with Map unbound only the trigger and thumbstick are labelled");
    Check(ControllerCallouts.Build(mapParts, ControllerHintContext.Map, false, true).Count == 2, "Map: without bindings to read, the close button is simply not labelled");
    // The other screens never read the bindings.
    Check(ControllerCallouts.Build(mapParts, ControllerHintContext.Menus, false, true, isFrame, false, _ => throw new InvalidOperationException()).Count == 2 &&
          ControllerCallouts.Build(mapParts, ControllerHintContext.AircraftSelection, false, false, isFrame, false, _ => throw new InvalidOperationException()).Count == 2,
        "Menus and aircraft selection do not look at the bindings at all");
}
var dpad = ControllerCallouts.Build(frameParts, ControllerHintContext.Map, true, false, true, false, a =>
    a == ControllerActionId.Map ? new ControllerActionBinding(ControllerButton.Primary, ControllerHandRole.Left) :
    new ControllerActionBinding(ControllerButton.None, ControllerHandRole.Stick));
Check(dpad.Single(c => c.Detail == "Close map").Part == "dpad" && dpad.Single(c => c.Detail == "Close map").Title == "D-pad ↓",
    "Map: on the Frame's left controller a D-pad direction bound to Map is named with its arrow");
foreach (var part in new[] { "body", "status", "button_system", "nonsense", "" })
    Check(!ControllerCallouts.TryGetButton(part, true, true, out _) && !ControllerCallouts.TryGetButton(part, false, false, out _), "Not a bindable button: " + part);
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
