using NOVR;
using NOVR.Rendering;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using Native = NOVR.Rendering.FixedFoveationNative;

internal static class CantedViewChecks
{
    internal static void Run(Action<bool, string> check)
    {
        void Near(float expected, float actual, string label) => check(Math.Abs(expected - actual) < .00002f, label);
        using var f = new Fixture();
        f.Activate();
        FixedFoveationRuntime.SetEyeTracking(true);
        var localGaze = Quaternion.Angles(.13f, -.07f, 0);
        foreach (var origin in new[] { Quaternion.identity, Quaternion.Angles(1.3f, .5f, -.4f) })
            foreach (var calibration in new[] { Quaternion.identity, Quaternion.Angles(-.6f, .3f, .2f) })
                foreach (var head in new[] { Quaternion.identity, Quaternion.Angles(.75f, -.2f, .3f) })
                    foreach (var parented in new[] { false, true })
                        foreach (var position in new[] { new Vector3(), new Vector3(2134, -78, -4001) })
                        {
                            // Simulate NOVRPoseDriver plus recentering: the camera has
                            // calibrated head rotation below the aircraft parent, while
                            // gaze/head input remains in the original tracking space.
                            var camera = APIBus.MainCamera.transform;
                            camera.parent = parented ? new Transform { rotation = origin, position = position } : null;
                            camera.rotation = origin * calibration * head;
                            camera.position = position;
                            NovrEyeGaze.Pose = new NovrEyeGazePose(new Vector3(.04f, 1.5f, 0), head * localGaze,
                                new Vector3(0, 1.5f, 0), head);
                            f.Tick();
                            var samplesBefore = NovrEyeGaze.Samples;
                            for (var eye = 0; eye < 2; eye++)
                            {
                                // Intentionally include yaw, pitch and roll differences;
                                // this is stronger than symmetric left/right panel cant.
                                var cant = eye == 0 ? Quaternion.Angles(-.16f, .04f, -.025f) : Quaternion.Angles(.19f, -.03f, .02f);
                                var view = f.World(eye);
                                view.cameraData.xr.View = Matrix4x4.EyeView(origin * calibration * head * cant, position);
                                view.cameraData.xr.Projection = new Matrix4x4
                                {
                                    m00 = eye == 0 ? 1.2f : 1.05f,
                                    m11 = eye == 0 ? 1.3f : 1.15f,
                                    m02 = eye == 0 ? .2f : -.17f,
                                    m12 = eye == 0 ? .08f : -.1f
                                };
                                f.Render(ref view);
                                var scope = Native.AcquiredScopes[^1];
                                var localDirection = Quaternion.Inverse(cant) * (localGaze * Vector3.forward);
                                var projection = view.cameraData.xr.Projection;
                                var expectedX = .5f * (1 - projection.m02 + projection.m00 * localDirection.x / localDirection.z);
                                var expectedY = .5f * (1 + projection.m12 - projection.m11 * localDirection.y / localDirection.z);
                                Near(expectedX, scope.X, "Horizontal gaze stays aligned through per-eye cant, recenter calibration, parent rotation and seat translation.");
                                Near(expectedY, scope.Y, "Vertical gaze stays aligned through per-eye cant, recenter calibration, parent rotation and seat translation.");
                                check(view.cameraData.xr.ViewReads == 1 && scope.Eye == eye,
                                    "Each eye obtains its own view once and retains the matching native eye identity.");
                            }
                            check(NovrEyeGaze.Samples == samplesBefore + 1,
                                "Canted stereo eyes still share one eye-tracking sample per frame.");
                        }

        // A broken optional view cannot emit non-finite native centers or disable
        // fixed foveation: the existing validated fixed center stays available.
        var invalid = f.World();
        invalid.cameraData.xr.View.m00 = float.NaN;
        invalid.cameraData.xr.Projection = new Matrix4x4 { m00 = 1, m11 = 1, m02 = .2f, m12 = -.1f };
        f.Tick();
        f.Render(ref invalid);
        Near(.4f, Native.AcquiredScopes[^1].X, "Invalid eye view falls back to fixed horizontal center.");
        Near(.45f, Native.AcquiredScopes[^1].Y, "Invalid eye view falls back to fixed vertical center.");
        check(Native.Enabled && FixedFoveationRuntime.IsAvailable && Native.LiveScopes.Count == 0,
            "Invalid eye projection remains a bounded optional-input failure.");
        check(FixedFoveationRuntime.EyeTrackingStatus.Contains("pose is invalid"),
            "Invalid view is not described as working eye tracking.");

        var throwing = f.World();
        throwing.cameraData.xr.Projection = invalid.cameraData.xr.Projection;
        throwing.cameraData.xr.ThrowViewRead = true;
        var warnings = Debug.Warnings.Count;
        for (var i = 0; i < 3; i++)
        {
            f.Tick();
            f.Render(ref throwing);
            Near(.4f, Native.AcquiredScopes[^1].X, "Eye-view retrieval failure retains the fixed X center.");
            Near(.45f, Native.AcquiredScopes[^1].Y, "Eye-view retrieval failure retains the fixed Y center.");
            check(Native.Enabled && FixedFoveationRuntime.IsAvailable && Native.LiveScopes.Count == 0,
                "An optional eye-view exception cannot disable working VRS or strand a render scope.");
        }
        check(Debug.Warnings.Count == warnings + 1, "Repeated eye-view exceptions produce one bounded warning.");
        check(FixedFoveationRuntime.EyeTrackingStatus.Contains("no eye gaze"), "An unavailable eye view reports the fixed fallback.");
        var viewReads = throwing.cameraData.xr.ViewReads;
        NovrEyeGaze.State = NovrEyeGazeReadState.Stale;
        f.Tick();
        f.Render(ref throwing);
        check(throwing.cameraData.xr.ViewReads == viewReads, "Stale gaze does not request an optional eye view.");
        NovrEyeGaze.State = NovrEyeGazeReadState.Tracked;
        throwing.cameraData.xr.ThrowViewRead = false;
        f.Tick();
        f.Render(ref throwing);
        check(FixedFoveationRuntime.EyeTrackingStatus.Contains("following tracked eyes") && Native.Enabled,
            "View retrieval recovers on a later frame without restarting the validated rendering backend.");
    }
}
