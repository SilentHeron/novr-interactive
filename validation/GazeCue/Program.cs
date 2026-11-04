using System.Reflection;
using BepInEx.Configuration;
using NOVR;
using NOVR.MotionControls;
using NuclearOption.UIStyleSystem;
using UnityEngine;

var count = 0;
void Check(bool ok, string message) { count++; if (!ok) throw new Exception(message); }
void Near(float a, float b, string message) => Check(Math.Abs(a - b) < .0001f, message);
var dir = Path.Combine(Path.GetTempPath(), "novr-gaze-cue-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
try
{
    var file = new ConfigFile(Path.Combine(dir, "cue.cfg"), false) { SaveOnConfigSet = false };
    var config = new ModConfiguration(file); var settings = config.LookCue;
    Check(settings.Size.Value == 1 && settings.Opacity.Value == .65f && settings.Shape.Value == GazeCueShape.Circle, "No new appearance defaults");
    var go = GazeRingGraphic.Create(new GameObject("parent").transform);
    var cue = go.GetComponent<GazeRingGraphic>();
    void Refresh() => typeof(GazeRingGraphic).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(cue, null);
    var original = cue.Mesh();
    Check(cue.rectTransform.sizeDelta == new Vector2(60, 60), "Default remains 60-pixel cue");
    Check(settings.VerticalAngle == -1 && cue.rectTransform.anchoredPosition.x == 0, "Default shifts cue slightly lower without lateral movement");
    Near((float)Math.Atan(cue.rectTransform.anchoredPosition.y * GazeCueSettings.CanvasScale / GazeCueSettings.ViewDepthMeters) * 180 / MathF.PI,
        -1, "Default is exactly one degree below view centre");
    Check(!cue.raycastTarget, "Cue cannot interfere with controller pointers");
    Near(cue.color.a, .65f, "Original base alpha unchanged");
    Check(original.Vertices.Count == 260 && original.Triangles.Count == 256, "Original two 64-segment rings preserved");
    Near(original.Vertices[0].p.x, 30 * .83f, "Original contrast rim inner radius");
    Near(original.Vertices[1].p.x, 30, "Original contrast rim outer radius");
    Near(original.Vertices[130].p.x, 30 * .87f, "Original green ring inner radius");
    Near(original.Vertices[131].p.x, 30 * .97f, "Original green ring outer radius");
    Near(original.Vertices[0].color.a, .65f * .65f, "Original contrast rim alpha");
    settings.Size.Value = -10; settings.Opacity.Value = -10;
    Check(settings.Scale == .5f && settings.Alpha == .05f, "Config ranges prevent unusable negative geometry/alpha");
    settings.Size.Value = 100; settings.Opacity.Value = 100;
    Check(settings.Scale == 2.5f && settings.Alpha == 1, "Config ranges cap size/opacity");
    settings.Shape.Value = (GazeCueShape)999;
    Check(settings.CurrentShape == GazeCueShape.Circle, "Invalid shape falls back to circle");
    settings.Size.Value = float.NaN; settings.Opacity.Value = float.NaN;
    Check(float.IsFinite(settings.Scale) && settings.Scale >= .5f && settings.Scale <= 2.5f &&
        float.IsFinite(settings.Alpha) && settings.Alpha >= .05f && settings.Alpha <= 1, "Nonfinite config cannot create missing geometry");
    settings.Reset();
    settings.Size.Value = 1.7f; Refresh();
    Near(cue.rectTransform.sizeDelta.x, 102, "Size changes cue alone");
    Near(cue.color.a, .65f, "Size does not change opacity");
    settings.Opacity.Value = .35f; Refresh();
    Near(cue.rectTransform.sizeDelta.x, 102, "Opacity does not change size");
    Near(cue.color.a, .35f, "Opacity adjustable independently");
    foreach (var angle in new[] { -8f, -2.25f, 0f, .25f, 5f, 8f })
    {
        settings.VerticalDegrees.Value = angle; Refresh();
        var actual = (float)Math.Atan(cue.rectTransform.anchoredPosition.y * GazeCueSettings.CanvasScale / GazeCueSettings.ViewDepthMeters) * 180 / MathF.PI;
        Near(actual, angle, "Cue position is an angular offset from view centre");
        Near(cue.rectTransform.sizeDelta.x, 102, "Position does not change cue size");
        Near(cue.color.a, .35f, "Position does not change cue opacity");
        Check(cue.rectTransform.anchoredPosition.x == 0, "Vertical position cannot shift cue laterally");
        Check(settings.PositionLabel.Contains(angle < 0 ? "below" : angle > 0 ? "above" : "Centre"), "Position label states direction clearly");
    }
    settings.VerticalDegrees.Value = -100; Check(settings.VerticalAngle == -8, "Downward travel bounded");
    settings.VerticalDegrees.Value = 100; Check(settings.VerticalAngle == 8, "Upward travel bounded");
    settings.VerticalDegrees.Value = float.NaN;
    Check(float.IsFinite(settings.VerticalAngle) && float.IsFinite(settings.CanvasOffsetY), "Invalid position cannot hide or corrupt cue");
    config.InstrumentBrightness.Value = .5f; Refresh();
    Near(cue.color.a, .175f, "HUD brightness still multiplies configured alpha");
    ThemeManager.Active = new ThemeManager(); Refresh();
    Near(cue.color.g, .8f, "Cue follows actual native HUD theme");
    foreach (var shape in Enum.GetValues<GazeCueShape>())
    {
        settings.Shape.Value = shape;
        foreach (var size in new[] { .5f, 1f, 2.5f })
        {
            settings.Size.Value = size; Refresh(); var mesh = cue.Mesh(); var radius = 30f * size;
            Check(mesh.Vertices.Count > 0 && mesh.Triangles.Count > 0, "Each shape produces geometry at min/default/max scale");
            Check(mesh.Vertices.All(v => float.IsFinite(v.p.x) && float.IsFinite(v.p.y) && Math.Abs(v.p.x) <= radius + .001f && Math.Abs(v.p.y) <= radius + .001f), "Geometry stays within bounded cue area");
            Check(mesh.Triangles.All(t => t.a >= 0 && t.b >= 0 && t.c >= 0 && t.a < mesh.Vertices.Count && t.b < mesh.Vertices.Count && t.c < mesh.Vertices.Count), "Mesh has valid triangle indices");
            Check(mesh.Triangles.All(t => {
                var a = mesh.Vertices[t.a].p; var b = mesh.Vertices[t.b].p; var c = mesh.Vertices[t.c].p;
                return (b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x) < 0;
            }), "Geometry has consistent original clockwise winding without zero-area triangles");
            if (shape != GazeCueShape.Dot)
                Check(mesh.Vertices.All(v => v.p.x*v.p.x + v.p.y*v.p.y >= radius*radius*.07f), "Circle/crosshair/brackets leave aiming centre open");
            else Check(mesh.Vertices.Any(v => v.p == Vector2.zero), "Dot has a filled centre");
        }
    }
    Check(cue.DirtyCount >= 3, "Changing shape invalidates the live mesh");
    settings.Reset(); config.InstrumentBrightness.Value = 1; ThemeManager.Active = null; Refresh();
    var reset = cue.Mesh();
    Check(reset.Vertices.SequenceEqual(original.Vertices) && reset.Triangles.SequenceEqual(original.Triangles), "Reset restores exact original circle mesh/colors");
    foreach (var mode in Enum.GetValues<TargetCueMode>())
        foreach (var cockpit in new[] { false, true })
            foreach (var target in new[] { false, true })
                foreach (var selecting in new[] { false, true })
                    Check(TargetCueVisibility.Show(mode, cockpit, target, selecting) == (cockpit && mode != TargetCueMode.Off && (mode == TargetCueMode.Always || target)), "Context/Always/Off semantics unchanged, empty Select stays hidden");
    settings.Size.Value = 1.5f; settings.Opacity.Value = .4f; settings.Shape.Value = GazeCueShape.Brackets; settings.VerticalDegrees.Value = -2.25f; file.Save();
    var reloaded = new GazeCueSettings(new ConfigFile(Path.Combine(dir, "cue.cfg"), false));
    Check(reloaded.Scale == 1.5f && reloaded.Alpha == .4f && reloaded.CurrentShape == GazeCueShape.Brackets, "Custom appearance persists across reload");
    Check(reloaded.VerticalAngle == -2.25f, "Custom angular position persists across reload");
    reloaded.Reset(); Check(reloaded.VerticalAngle == -1, "Reset restores modest lower default");
    CandidateChecks.Run(Check);
    Console.WriteLine($"PASS: {count} gaze-cue settings/default/geometry/candidate checks.");
}
finally { Directory.Delete(dir, true); }
