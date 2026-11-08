using NOVR.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.OpenXR;
using Native = NOVR.Rendering.FixedFoveationNative;

// URP's XR path draws the bracketed opaque pass into an intermediate texture
// whose projection is flipped on Direct3D. The rate map addresses target rows,
// so the centre must be mirrored there and left alone on an upright target.
internal static class FlippedTargetChecks
{
    internal static void Run(Action<bool, string> check)
    {
        void Near(float expected, float actual, string label) => check(Math.Abs(expected - actual) < .00001f, label);
        using var f = new Fixture();
        f.Activate();
        var projections = new[]
        {
            new Matrix4x4 { m00 = 1.2f, m11 = 1.5f, m02 = .2f, m12 = .1f },
            new Matrix4x4 { m00 = 1.05f, m11 = 1.15f, m02 = -.17f, m12 = -.1f }
        };
        float FixedX(int eye) => .5f * (1 - projections[eye].m02);
        float FixedY(int eye) => .5f * (1 + projections[eye].m12);
        RenderingData View(int eye, bool flipped)
        {
            var data = f.World(eye);
            data.cameraData.xr.Projection = projections[eye];
            data.cameraData.Flipped = flipped;
            return data;
        }

        for (var eye = 0; eye < 2; eye++)
        {
            var upright = View(eye, false);
            f.Render(ref upright);
            Near(FixedX(eye), Native.AcquiredScopes[^1].X, "An upright target keeps the horizontal optical centre.");
            Near(FixedY(eye), Native.AcquiredScopes[^1].Y, "An upright target keeps the top-left vertical centre.");
        }
        for (var eye = 0; eye < 2; eye++)
        {
            var flipped = View(eye, true);
            f.Render(ref flipped);
            Near(FixedX(eye), Native.AcquiredScopes[^1].X, "A flipped target leaves the horizontal centre unchanged.");
            Near(1 - FixedY(eye), Native.AcquiredScopes[^1].Y, "A flipped intermediate target mirrors the fixed centre onto its rows.");
            check(Native.AcquiredScopes[^1].Eye == eye, "Mirroring never changes the native eye identity.");
        }
        check(Debug.Logs.Count(log => log.Contains("upside-down")) == 1 && Debug.Logs.Count(log => log.Contains("drawn upright")) == 1,
            "Each target orientation change is logged once, never per frame.");

        FixedFoveationRuntime.SetEyeTracking(true);
        var gaze = Quaternion.Angles(.15f, -.2f, 0);
        NovrEyeGaze.Pose = new NovrEyeGazePose(default, gaze, default, Quaternion.identity);
        var direction = gaze * Vector3.forward;
        check(direction.y > .05f && direction.z > .9f, "The fixture gaze looks upward and ahead.");
        for (var eye = 0; eye < 2; eye++)
        {
            var projection = projections[eye];
            var expectedX = .5f * (1 - projection.m02 + projection.m00 * direction.x / direction.z);
            var expectedY = .5f * (1 + projection.m12 - projection.m11 * direction.y / direction.z);
            f.Tick();
            var upright = View(eye, false);
            f.Render(ref upright);
            Near(expectedX, Native.AcquiredScopes[^1].X, "Upright gaze keeps its horizontal projection.");
            Near(expectedY, Native.AcquiredScopes[^1].Y, "Upright gaze projects with a top-left origin.");
            check(Native.AcquiredScopes[^1].Y < FixedY(eye), "Looking up moves an upright centre toward the first rows.");
            var flipped = View(eye, true);
            f.Render(ref flipped);
            Near(expectedX, Native.AcquiredScopes[^1].X, "Flipped gaze keeps its horizontal projection.");
            Near(1 - expectedY, Native.AcquiredScopes[^1].Y, "Flipped gaze mirrors onto the rows that hold the top of the image.");
            check(Native.AcquiredScopes[^1].Y > 1 - FixedY(eye), "Looking up moves a flipped centre toward the last rows, where the image top is drawn.");
            check(FixedFoveationRuntime.EyeTrackingStatus.Contains("following tracked eyes"),
                "Mirroring the centre is not reported as a tracking fallback.");
            var scale = FixedFoveationPolicy.AngularScale(projections[eye].m00, projections[eye].m11);
            FixedFoveationPolicy.Radii(Native.AcquiredScopes[^1].Level, false, false, scale, out var fixedInner, out var fixedOuter);
            FixedFoveationPolicy.Radii(Native.AcquiredScopes[^1].Level, true, false, scale, out var trackedInner, out var trackedOuter);
            check(Math.Abs(Native.AcquiredScopes[^1].Inner - trackedInner) < .00001f && Math.Abs(Native.AcquiredScopes[^1].Outer - trackedOuter) < .00001f &&
                  trackedInner > fixedInner && trackedOuter > fixedOuter,
                "Tracked gaze protects a wider region than the fixed-centre preset to cover eye-tracking lag.");
        }

        // Fallbacks must mirror as well: an untracked eye on a flipped target
        // protects the mirrored fixed centre, never the opposite half.
        NovrEyeGaze.State = NovrEyeGazeReadState.Untracked;
        f.Tick();
        var fallback = View(0, true);
        f.Render(ref fallback);
        Near(1 - FixedY(0), Native.AcquiredScopes[^1].Y, "Untracked gaze falls back to the mirrored fixed centre on a flipped target.");
        Near(FixedX(0), Native.AcquiredScopes[^1].X, "Fallback keeps the horizontal centre.");
        check(FixedFoveationRuntime.EyeTrackingStatus.Contains("not tracked"), "Fallback reporting is unchanged by target orientation.");
        FixedFoveationPolicy.Radii(Native.AcquiredScopes[^1].Level, false, false,
            FixedFoveationPolicy.AngularScale(projections[0].m00, projections[0].m11), out var fallbackInner, out _);
        check(Math.Abs(Native.AcquiredScopes[^1].Inner - fallbackInner) < .00001f, "The fixed fallback uses the unwidened preset radii.");
        NovrEyeGaze.State = NovrEyeGazeReadState.Tracked;
        f.Tick();
        var recovered = View(0, false);
        f.Render(ref recovered);
        Near(FixedY(0) - .5f * projections[0].m11 * direction.y / direction.z, Native.AcquiredScopes[^1].Y,
            "Returning to an upright target restores the direct projection without any retained mirroring.");
    }
}
