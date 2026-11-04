using NOVR.MotionControls;
using UnityEngine;

var checks = 0;
void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
void Near(Vector3 a, Vector3 b, string message, float tolerance = .00001f) => Check((a - b).magnitude <= tolerance, message);
void SameRotation(Quaternion a, Quaternion b, string message) => Check(Quaternion.Angle(a, b) < .08f, message);
GloveBoneChecks.Run(Check);
foreach (var collectiveAircraft in new[] { false, true })
{
    Check(NativeControlSelection.Source(true, collectiveAircraft) == "SmallFighter1", "Helicopter stick lost the shared FS20 model");
    Check(NativeControlSelection.FittedFs20(true, collectiveAircraft, true), "Helicopter stick lost the refined FS20 grip");
    Check(!NativeControlSelection.FittedFs20(true, collectiveAircraft, false), "Missing canonical stick incorrectly received a fitted pose");
    Check(NativeControlSelection.Source(false, collectiveAircraft) == (collectiveAircraft ? "UtilityHelo1" : "SmallFighter1"), "Aircraft throttle/collective model changed");
    Check(NativeControlSelection.FittedFs20(false, collectiveAircraft, true) == !collectiveAircraft, "Collective incorrectly received the FS20 throttle pose");
}
foreach (var left in new[] { false, true })
foreach (var fps in new[] { 45, 72, 90, 120 })
{
    Time.unscaledTime = 0; Time.unscaledDeltaTime = 1f / fps;
    var tracking = new GameObject("tracking").transform;
    tracking.SetPositionAndRotation(new Vector3(3, 1, -4), Quaternion.Euler(5, 30, -8));
    using var glove = new RiggedGlove(left, tracking, new Material(), default);
    var sample = new ControllerSample { Valid = true, Position = new Vector3(.15f, -.1f, .2f), Rotation = Quaternion.Euler(12, 30, 9), HasAimPose = true, AimRotation = Quaternion.Euler(12, 30, 9) * Quaternion.Euler(42.8091f, 0, 0), Profile = ControllerHardwareProfile.SteamFrame };
    var grip = new Pose(new Vector3(3.1f, 1.25f, -3.6f), Quaternion.Euler(10, -15, 7));
    var button = new Pose(new Vector3(3.23f, 1.16f, -3.45f), Quaternion.Euler(2, -15, 45));
    void Frame(Pose? attachment = null, bool stick = false, bool collective = false, Pose? cockpit = null, bool pointing = false, bool pinching = false)
    {
        Time.unscaledTime += Time.unscaledDeltaTime;
        glove.Pose(sample, attachment, stick, collective, cockpit, pointing, pinching);
    }
    void Settle(Pose? attachment = null, bool stick = false, bool collective = false, Pose? cockpit = null, bool pointing = false, bool pinching = false)
    { for (var i = 0; i < fps; i++) Frame(attachment, stick, collective, cockpit, pointing, pinching); }
    void CheckFree()
    {
        var settings = ModConfiguration.Instance.MotionControls;
        Near(glove.Root.localPosition, sample.Position + sample.Rotation * settings.FreeHandOffset(left), "Free calibrated wrist anchor changed");
        SameRotation(glove.Root.localRotation, sample.AimRotation * Quaternion.Euler(settings.FreeHandAngles(left)), "Free palm orientation changed");
        Check(!glove.IsTransitioning, "Free transition never settled");
    }
    Frame(); CheckFree();
    var rawTip = glove.FreeTouchPoint(sample, true); var rawPalm = glove.FreeTouchPoint(sample, false);
    var previous = glove.Root.localPosition; var previousRotation = glove.Root.localRotation;
    Frame(grip, true);
    Near(glove.Root.localPosition, previous, "Acquiring stick snapped wrist"); SameRotation(glove.Root.localRotation, previousRotation, "Acquiring stick snapped rotation");
    Settle(grip, true);
    Near(glove.Root.position, grip.position + grip.rotation * Fs20HandPoses.Wrist(true, left), "Stick anchor changed");
    SameRotation(glove.Root.rotation, grip.rotation * Fs20HandPoses.StickRotation(left), "Stick wrist orientation changed");
    Near(glove.FreeTouchPoint(sample, true), rawTip, "Held visual attachment fed back into raw fingertip");
    Near(glove.FreeTouchPoint(sample, false), rawPalm, "Held visual attachment fed back into raw palm");
    Check(!glove.IsTransitioning, "Held transition never settled");
    previous = glove.Root.localPosition; previousRotation = glove.Root.localRotation;
    var previousBones = glove.Renderer.bones.Select(b => b.localRotation).ToArray();
    Frame(); Near(glove.Root.localPosition, previous, "Releasing stick snapped wrist"); SameRotation(glove.Root.localRotation, previousRotation, "Releasing stick snapped rotation");
    for (var i = 0; i < previousBones.Length; i++) SameRotation(glove.Renderer.bones[i].localRotation, previousBones[i], "Release snapped a finger joint");
    for (var i = 0; i < 3; i++) Frame();
    previous = glove.Root.localPosition; previousBones = glove.Renderer.bones.Select(b => b.localRotation).ToArray();
    Frame(grip, true); Near(glove.Root.localPosition, previous, "Reacquiring during release snapped wrist");
    for (var i = 0; i < previousBones.Length; i++) SameRotation(glove.Renderer.bones[i].localRotation, previousBones[i], "Reacquiring during release snapped finger joint");
    Settle(grip, true); Frame();
    for (var i = 0; i < Math.Ceiling(.09f * fps) + 1; i++) Frame();
    CheckFree();
    previous = glove.Root.localPosition;
    Frame(cockpit: button, pointing: true); Near(glove.Root.localPosition, previous, "Button guide entry snapped wrist");
    Settle(cockpit: button, pointing: true);
    Near(glove.Renderer.bones[10].position, button.position, "Button guide failed to settle exact fingertip contact");
    Near(glove.FreeTouchPoint(sample, true), rawTip, "Button guide fed back into raw fingertip");
    Near(glove.FreeTouchPoint(sample, false), rawPalm, "Button guide fed back into raw palm");
    previous = glove.Root.localPosition;
    Frame(); Near(glove.Root.localPosition, previous, "Leaving button guide snapped wrist"); Settle(); CheckFree();
    Settle(cockpit: button, pinching: true);
    Near((glove.Renderer.bones[5].position + glove.Renderer.bones[10].position) * .5f, button.position, "Pinch midpoint contact changed");
    Near(glove.FreeTouchPoint(sample, true), rawTip, "Pinch fed back into raw fingertip");

    // The layout editor uses the same visual contact contract: an exact
    // thumb/index midpoint at the live corner, with the user's glove approach
    // preserved relative to the widget. The corner can move during resizing.
    Settle();
    var corner = new Pose(new Vector3(3.31f, 1.20f, -3.46f),
        tracking.rotation * FreeHandPose.Rotation(sample, ModConfiguration.Instance.MotionControls.FreeHandAngles(left)));
    previous = glove.Root.localPosition; previousRotation = glove.Root.localRotation;
    previousBones = glove.Renderer.bones.Select(b => b.localRotation).ToArray();
    var untouchedSample = sample;
    Frame(cockpit: corner, pinching: true);
    Near(glove.Root.localPosition, previous, "Widget corner acquisition snapped the wrist");
    SameRotation(glove.Root.localRotation, previousRotation, "Widget corner acquisition snapped wrist orientation");
    for (var i = 0; i < previousBones.Length; i++)
        SameRotation(glove.Renderer.bones[i].localRotation, previousBones[i], "Widget corner acquisition snapped a finger");
    Settle(cockpit: corner, pinching: true);
    Near((glove.Renderer.bones[5].position + glove.Renderer.bones[10].position) * .5f, corner.position,
        "Widget corner failed to attach at the thumb/index midpoint");
    SameRotation(glove.Root.rotation, corner.rotation, "Widget corner changed the user's glove approach");
    corner = new Pose(corner.position + new Vector3(.08f, .04f, -.05f),
        Quaternion.Euler(13, -18, 0) * corner.rotation);
    Frame(cockpit: corner, pinching: true);
    Near((glove.Renderer.bones[5].position + glove.Renderer.bones[10].position) * .5f, corner.position,
        "Settled pinch lagged a resized or tilted widget corner");
    SameRotation(glove.Root.rotation, corner.rotation, "Corner pinch did not follow the widget angle");
    Near(glove.FreeTouchPoint(sample, true), rawTip, "Widget pinch changed raw fingertip hit testing");
    Near(glove.FreeTouchPoint(sample, false), rawPalm, "Widget pinch changed raw palm hit testing");
    Near(sample.Position, untouchedSample.Position, "Widget pinch changed controller input position");
    SameRotation(sample.Rotation, untouchedSample.Rotation, "Widget pinch changed controller input rotation");
    Check(sample.Grip == untouchedSample.Grip && sample.Trigger == untouchedSample.Trigger,
        "Widget pinch changed controller input buttons");
    previous = glove.Root.localPosition; previousRotation = glove.Root.localRotation;
    Frame();
    Near(glove.Root.localPosition, previous, "Widget corner release snapped the wrist");
    SameRotation(glove.Root.localRotation, previousRotation, "Widget corner release snapped wrist orientation");
    Settle(); CheckFree();

    // Inline appearance controls use a pointing pose with no contact socket:
    // only the fingers change, and the wrist remains freely tracked.
    Settle(pointing: true); CheckFree();
    Near(glove.FreeTouchPoint(sample, true), rawTip, "Inline pointing changed raw fingertip hit testing");
    var beforeSliderReach = sample.Position;
    sample.Position += new Vector3(.01f, .02f, .03f);
    Frame(pointing: true); CheckFree();
    sample.Position = beforeSliderReach;
    Settle(); CheckFree();

    Settle(grip);
    Near(glove.Root.position, grip.position + grip.rotation * Fs20HandPoses.Wrist(false, left), "Throttle anchor changed");
    MotionControlVisuals.FittedStick = false;
    Settle(grip, true);
    var genericStick = grip.rotation * Quaternion.Euler(0, 0, left ? 90 : -90);
    SameRotation(glove.Root.rotation, genericStick, "Missing canonical stick fallback received the fitted FS20 wrist");
    Near(glove.Root.position, grip.position - genericStick * new Vector3(0, -.019f, .043f), "Unfitted stick palm offset changed");
    // A successfully acquired standard throttle remains fitted even when the
    // other handle is unavailable; selection follows each actual visual model.
    Settle(grip);
    Near(glove.Root.position, grip.position + grip.rotation * Fs20HandPoses.Wrist(false, left), "Independent fitted throttle lost its pose");
    MotionControlVisuals.FittedThrottle = false;
    Settle(grip);
    SameRotation(glove.Root.rotation, grip.rotation * Quaternion.Euler(8, 0, 0), "Fallback throttle received the fitted FS20 wrist");
    MotionControlVisuals.FittedStick = MotionControlVisuals.FittedThrottle = true;
    Settle(grip, true);
    SameRotation(glove.Root.rotation, grip.rotation * Fs20HandPoses.StickRotation(left), "Standard handle did not restore approved fitted pose");
    Settle(grip, collective: true);
    SameRotation(glove.Root.rotation, grip.rotation * CollectiveHandPoses.Rotation(left), "Shared collective lost authored wrist rotation");
    Near(glove.Root.position, grip.position + grip.rotation * CollectiveHandPoses.Wrist(left), "Shared collective lost sleeve socket");
    Near(glove.FreeTouchPoint(sample, true), rawTip, "Collective attachment changed raw fingertip input");
    MotionControlVisuals.HasFittedCollective = false;
    Settle(grip, collective: true);
    var collectiveRotation = grip.rotation * Quaternion.Euler(0, 0, left ? 60 : -60) * Quaternion.Euler(0, left ? -90 : 90, 0);
    SameRotation(glove.Root.rotation, collectiveRotation, "Missing canonical model lost safe fallback rotation");
    Near(glove.Root.position, grip.position - collectiveRotation * new Vector3(0, -.019f, .043f), "Missing canonical model lost fallback palm anchor");
    MotionControlVisuals.HasFittedCollective = true;
    sample.TriggerTouched = true; Settle(grip, true);
    previous = glove.Root.position; previousBones = glove.Renderer.bones.Select(b => b.localRotation).ToArray();
    sample.Trigger = 1; Settle(grip, true);
    Near(glove.Root.position, previous, "Trigger squeeze moved fitted wrist");
    for (var i = 0; i < previousBones.Length; i++)
        if (i >= 7 && i <= 9) Check(Quaternion.Angle(glove.Renderer.bones[i].localRotation, previousBones[i]) > .1f, "Trigger did not articulate all three index joints");
        else SameRotation(glove.Renderer.bones[i].localRotation, previousBones[i], "Trigger pressure altered another finger");
    previousBones = glove.Renderer.bones.Select(b => b.localRotation).ToArray();
    sample.Primary = true; Frame(grip, true);
    Check(Quaternion.Angle(glove.Renderer.bones[2].localRotation, previousBones[2]) < 12, "Thumb target switch jumped in one frame");
    Settle(grip, true); Near(glove.Root.position, previous, "Thumb button moved fitted wrist");
    for (var i = 7; i <= 9; i++) SameRotation(glove.Renderer.bones[i].localRotation, previousBones[i], "Thumb button moved trigger finger");
    // Aircraft/tracking movement must carry a release transition, not leave it in world space.
    Frame(); previous = glove.Root.localPosition;
    tracking.position += new Vector3(50, 0, -30);
    Near(glove.Root.position, tracking.TransformPoint(previous), "Release transition lagged a moving aircraft");
    sample.Valid = false; Frame(); Check(!glove.IsTransitioning && !glove.Root.gameObject.activeSelf, "Tracking loss kept stale transition active");
    sample.Valid = true; Frame(); CheckFree();
    Settle(grip, true); Frame(); sample.Position += new Vector3(.25f, 0, 0); Frame(); CheckFree();
    Settle(grip, true); Frame(); sample.Rotation = sample.Rotation * Quaternion.Euler(0, 75, 0); Frame(); CheckFree();
    Settle(grip, true); Frame(); Time.unscaledDeltaTime = .2f; Frame(); CheckFree(); Time.unscaledDeltaTime = 1f / fps;
    Settle(grip, true); Frame(); glove.ResetAttachmentTransition(); Frame(); CheckFree();
    // Same-position, same-skin smooth UV splits receive one shading normal.
    var mesh = glove.Renderer.sharedMesh;
    var original = new Mesh { vertices = mesh.vertices, triangles = mesh.triangles }; original.RecalculateNormals();
    var welded = 0;
    foreach (var group in Enumerable.Range(0, mesh.vertices.Length).GroupBy(i => mesh.vertices[i]))
    {
        var indices = group.ToArray(); if (indices.Length < 2) continue;
        var smooth = indices.All(i => indices.All(j => mesh.boneWeights[i].Equals(mesh.boneWeights[j]) && Vector3.Dot(original.normals[i], original.normals[j]) >= .5f));
        foreach (var i in indices)
            if (smooth) { Near(mesh.normals[i], mesh.normals[indices[0]], "Smooth skin seam retained split normal"); welded++; }
            else Near(mesh.normals[i], original.normals[i], "Hard edge or different skin weights were welded");
    }
    Check(welded > 900, "Shading repair did not cover the known UV seams");
}
// The native controls and gloves must reflect together. A separate left-hand
// fit can look plausible yet drift off the mirrored handle during button motion.
{
    Time.unscaledTime = 0; Time.unscaledDeltaTime = 1f / 90;
    var tracking = new GameObject("mirror check").transform;
    using var right = new RiggedGlove(false, tracking, new Material(), default);
    using var left = new RiggedGlove(true, tracking, new Material(), default);
    var grip = new Pose(Vector3.zero, Quaternion.identity);
    var asset = RiggedGlove.Load(false);
    var bind = new Vector3[asset.bones.Length];
    for (var i = 0; i < bind.Length; i++)
    {
        var b = asset.bones[i]; bind[i] = new Vector3(b.position[0], b.position[1], b.position[2]);
        if (b.parent >= 0) bind[i] += bind[b.parent];
    }
    Vector3 Surface(int vertex)
    {
        var point = new Vector3(asset.vertices[vertex * 3], asset.vertices[vertex * 3 + 1], asset.vertices[vertex * 3 + 2]);
        var result = Vector3.zero;
        for (var j = 0; j < 4; j++)
        {
            var offset = vertex * 4 + j; var bone = asset.indices[offset];
            result += right.Renderer.bones[bone].TransformPoint(point - bind[bone]) * asset.weights[offset];
        }
        return result;
    }
    Vector3 Reflect(Vector3 v) => new(-v.x, v.y, v.z);
    int[] thenar = [1132, 1262, 1309, 1319, 2145];
    Vector3[] contact = []; Quaternion baseThumb = default; Quaternion upperThumb = default;
    foreach (var mode in new[] { "rest", "hat", "hat press", "release touch", "release press", "trigger", "rest again" })
    {
        var sample = new ControllerSample { Valid = true, Rotation = Quaternion.identity,
            ThumbstickTouched = mode == "hat", StickClick = mode == "hat press",
            PrimaryTouched = mode == "release touch", Primary = mode == "release press",
            Trigger = mode == "trigger" ? 1 : 0 };
        for (var frame = 0; frame < 90; frame++)
        {
            Time.unscaledTime += Time.unscaledDeltaTime;
            right.Pose(sample, grip, true, false); left.Pose(sample, grip, true, false);
        }
        Near(left.Root.position, Reflect(right.Root.position), "Mirrored grip moved wrist off reflected handle");
        for (var i = 0; i < right.Renderer.bones.Length; i++)
        {
            var rb = right.Renderer.bones[i]; var lb = left.Renderer.bones[i];
            Near(lb.position, Reflect(rb.position), "Left finger did not exactly mirror canonical right grip");
            Near(lb.rotation * Vector3.up, Reflect(rb.rotation * Vector3.up), "Mirrored joint orientation diverged", .0001f);
        }
        if (mode == "rest")
        { contact = thenar.Select(Surface).ToArray(); baseThumb = right.Renderer.bones[2].localRotation; upperThumb = right.Renderer.bones[3].localRotation; }
        else
        {
            SameRotation(right.Renderer.bones[2].localRotation, baseThumb, "Thumb control pulled its supporting metacarpal away from grip");
            for (var i = 0; i < thenar.Length; i++) Near(Surface(thenar[i]), contact[i], "Thumb animation moved the supporting thenar surface", .00005f);
            if (mode != "trigger" && mode != "rest again")
                Check(Quaternion.Angle(right.Renderer.bones[3].localRotation, upperThumb) > .05f, "Thumb actuator did not articulate above the fixed contact patch");
        }
    }
}
// Aim and grip axes are not interchangeable on angled controller handles.
foreach(var profile in new[]{ControllerHardwareProfile.SteamFrame, ControllerHardwareProfile.Touch})
foreach(var left in new[]{true,false})
foreach(var aimed in new[]{Quaternion.identity,Quaternion.Euler(-90,0,0),Quaternion.Euler(23,71,-14)})
{
    var pitch=profile==ControllerHardwareProfile.SteamFrame ? 42.8091f : 60f;
    var sample=new ControllerSample { Valid=true,HasAimPose=true,Profile=profile,AimRotation=aimed,Rotation=aimed*Quaternion.Euler(-pitch,0,0) };
    var correction=new Vector3(0,0,left?90:-90);
    var rotation=FreeHandPose.Rotation(sample,correction);
    Near(rotation*Vector3.forward,aimed*Vector3.forward,"Free fingers fail to point along controller aim");
    Near(rotation*new Vector3(left?1:-1,0,0),aimed*Vector3.up,"Handshake thumb/palm roll changed");
    sample.HasAimPose=false;
    SameRotation(FreeHandPose.Rotation(sample,correction),rotation,"Missing aim pose changes known-controller hand orientation");
    sample.HasAimPose=true;sample.AimRotation=new Quaternion(float.NaN,0,0,1);
    SameRotation(FreeHandPose.Rotation(sample,correction),rotation,"Invalid aim pose poisoned free wrist");
    var adjustment=new Vector3(5,-10,left?90:-90);sample.AimRotation=aimed;
    SameRotation(FreeHandPose.Rotation(sample,adjustment),aimed*Quaternion.Euler(adjustment),"Independent hand alignment correction was lost");
}
Console.WriteLine($"{checks} hand pose checks passed at 45/72/90/120 Hz on both glove rigs.");
Console.WriteLine("Standalone transform/math checks only: Unity rendering and headset comfort remain unverified.");
CollectiveChecks.Run(args);
