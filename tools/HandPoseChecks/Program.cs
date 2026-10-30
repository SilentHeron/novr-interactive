using NOVR.MotionControls;
using UnityEngine;

var checks = 0;
void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); checks++; }
void Near(Vector3 a, Vector3 b, string message, float tolerance = .00001f) => Check((a - b).magnitude <= tolerance, message);
void SameRotation(Quaternion a, Quaternion b, string message) => Check(Quaternion.Angle(a, b) < .08f, message);
foreach (var left in new[] { false, true })
foreach (var fps in new[] { 45, 72, 90, 120 })
{
    Time.unscaledTime = 0; Time.unscaledDeltaTime = 1f / fps;
    var tracking = new GameObject("tracking").transform;
    tracking.SetPositionAndRotation(new Vector3(3, 1, -4), Quaternion.Euler(5, 30, -8));
    using var glove = new RiggedGlove(left, tracking, new Material(), default);
    var sample = new ControllerSample { Valid = true, Position = new Vector3(.15f, -.1f, .2f), Rotation = Quaternion.Euler(12, 30, 9) };
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
        SameRotation(glove.Root.localRotation, sample.Rotation * Quaternion.Euler(settings.FreeHandAngles(left)), "Free palm orientation changed");
        Check(!glove.IsTransitioning, "Free transition never settled");
    }
    Frame(); CheckFree();
    var rawTip = glove.FreeTouchPoint(sample, true); var rawPalm = glove.FreeTouchPoint(sample, false);
    var previous = glove.Root.localPosition; var previousRotation = glove.Root.localRotation;
    Frame(grip, true);
    Near(glove.Root.localPosition, previous, "Acquiring stick snapped wrist"); SameRotation(glove.Root.localRotation, previousRotation, "Acquiring stick snapped rotation");
    Settle(grip, true);
    Near(glove.Root.position, grip.position + grip.rotation * Fs20HandPoses.Wrist(true, left), "Stick anchor changed");
    SameRotation(glove.Root.rotation, grip.rotation * Quaternion.Euler(0, 0, left ? 90 : -90), "Stick wrist orientation changed");
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
    Settle(grip);
    Near(glove.Root.position, grip.position + grip.rotation * Fs20HandPoses.Wrist(false, left), "Throttle anchor changed");
    Settle(grip, collective: true);
    var collectiveRotation = grip.rotation * Quaternion.Euler(0, 0, left ? 60 : -60) * Quaternion.Euler(0, left ? -90 : 90, 0);
    SameRotation(glove.Root.rotation, collectiveRotation, "Collective cuff orientation changed");
    Near(glove.Root.position, grip.position - collectiveRotation * new Vector3(0, -.019f, .043f), "Collective palm anchor changed");
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
Console.WriteLine($"{checks} hand pose checks passed at 45/72/90/120 Hz on both glove rigs.");
Console.WriteLine("Standalone transform/math checks only: Unity rendering and headset comfort remain unverified.");
