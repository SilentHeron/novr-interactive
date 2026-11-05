using NOVR;
using NOVR.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using Native = NOVR.Rendering.FixedFoveationNative;

internal static class GazeRuntimeChecks
{
    internal static void Run(Action<bool, string> check)
    {
        void Near(float expected, float actual, string label) => check(Math.Abs(expected - actual) < .00001f, label);
        void SetPose(float yaw = .2f, float pitch = -.1f) => NovrEyeGaze.Pose =
            new NovrEyeGazePose(default, Quaternion.Angles(yaw, pitch, 0), default, Quaternion.identity);

        using (var f = new Fixture())
        {
            f.Activate();
            SetPose();
            var fixedView = f.World();
            f.Render(ref fixedView);
            check(NovrEyeGaze.Calls == 0 && FixedFoveationRuntime.EyeTrackingStatus == "Centre: fixed.",
                "Fixed mode remains unchanged and never polls gaze.");
            check(fixedView.cameraData.xr.ViewReads == 0, "Fixed mode does not read the optional gaze view transform.");
            FixedFoveationRuntime.SetEyeTracking(true);
            check(ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value && Native.Enabled,
                "Enabling gaze changes the preference without restarting or disabling VRS.");
            check(FixedFoveationRuntime.EyeTrackingStatus.Contains("waiting for a flight"),
                "A newly selected gaze mode does not claim active tracking before a world pass.");
            Native.AcquiredScopes.Clear();
            var left = f.World(0);
            left.cameraData.xr.Projection = new Matrix4x4 { m00 = 1.2f, m11 = 1.5f, m02 = .2f, m12 = .1f };
            f.Render(ref left);
            var firstPose = NovrEyeGaze.Pose;
            SetPose(-.8f, .4f); // A fresh physical sample cannot split this stereo pair.
            var right = f.World(1);
            right.cameraData.xr.Projection = new Matrix4x4 { m00 = 1.2f, m11 = 1.5f, m02 = -.2f, m12 = -.1f };
            f.Render(ref right);
            check(NovrEyeGaze.Calls == 2 && NovrEyeGaze.Samples == 1,
                "Both eye passes consume one coherent input sample through the gaze-reader contract.");
            var direction = firstPose.Rotation * Vector3.forward;
            var expectedX = .4f + .6f * direction.x / direction.z;
            var expectedY = .55f - .75f * direction.y / direction.z;
            Near(expectedX, Native.AcquiredScopes[0].X, "Left eye projects eye direction using its own frustum.");
            Near(expectedY, Native.AcquiredScopes[0].Y, "Left eye has top-left Y projection.");
            Near(expectedX + .2f, Native.AcquiredScopes[1].X, "Right eye keeps asymmetric horizontal center.");
            Near(expectedY - .1f, Native.AcquiredScopes[1].Y, "Right eye keeps asymmetric vertical center.");
            check(FixedFoveationRuntime.EyeTrackingStatus.Contains("following tracked eyes"), "Status reports actual tracked input.");
            check(Native.LiveScopes.Count == 0, "Eye tracking preserves balanced render-scope ownership.");

            f.Tick();
            f.Render(ref left);
            check(NovrEyeGaze.Samples == 2 && Native.AcquiredScopes[^1].X < .4f,
                "A later frame uses the new gaze rather than retaining the previous direction.");
            FixedFoveationRuntime.SetEyeTracking(false);
            var calls = NovrEyeGaze.Calls;
            f.Render(ref right);
            Near(.6f, Native.AcquiredScopes[^1].X, "Turning eye tracking off restores fixed horizontal center immediately.");
            Near(.45f, Native.AcquiredScopes[^1].Y, "Turning eye tracking off restores fixed vertical center immediately.");
            check(NovrEyeGaze.Calls == calls && FixedFoveationRuntime.EyeTrackingStatus == "Centre: fixed.",
                "Fixed mode stops gaze polling and clears the active gaze status.");
        }

        using (var f = new Fixture())
        {
            f.Activate();
            FixedFoveationRuntime.SetEyeTracking(true);
            var nativeRequests = Native.Enables.Count;
            var view = f.World();
            view.cameraData.xr.Projection = new Matrix4x4 { m00 = 1.1f, m11 = 1.3f, m02 = .2f, m12 = .1f };
            foreach (var (state, status) in new[]
            {
                (NovrEyeGazeReadState.Unavailable, "no eye gaze"),
                (NovrEyeGazeReadState.Unsupported, "no eye-gaze support"),
                (NovrEyeGazeReadState.Unfocused, "not focused"),
                (NovrEyeGazeReadState.Untracked, "not tracked"),
                (NovrEyeGazeReadState.Stale, "fresh eye tracking"),
                (NovrEyeGazeReadState.Invalid, "pose is invalid")
            })
            {
                NovrEyeGaze.State = state;
                f.Tick();
                f.Render(ref view);
                Near(.4f, Native.AcquiredScopes[^1].X, state + " uses fixed X fallback.");
                Near(.55f, Native.AcquiredScopes[^1].Y, state + " uses fixed Y fallback.");
                check(FixedFoveationRuntime.EyeTrackingStatus.Contains(status), state + " has an accurate visible reason.");
                check(Native.Enabled && Native.Enables.Count == nativeRequests,
                    "Missing gaze does not disable compatible VRS or repeatedly reconfigure the driver.");
            }
            NovrEyeGaze.State = NovrEyeGazeReadState.Tracked;
            NovrEyeGaze.Pose = new NovrEyeGazePose(default, Quaternion.Angles(MathF.PI, 0, 0), default, Quaternion.identity);
            f.Tick();
            f.Render(ref view);
            Near(.4f, Native.AcquiredScopes[^1].X, "Backward gaze preserves fixed fallback.");
            check(FixedFoveationRuntime.EyeTrackingStatus.Contains("pose is invalid"), "Unprojectable tracked gaze is not reported as following eyes.");
            SetPose();
            f.Tick();
            f.Render(ref view);
            check(Native.AcquiredScopes[^1].X > .4f && FixedFoveationRuntime.EyeTrackingStatus.Contains("following tracked eyes"),
                "Fresh valid gaze resumes automatically after a fallback.");
            f.Tick();
            f.Tick();
            check(FixedFoveationRuntime.EyeTrackingStatus.Contains("waiting for a flight"),
                "Gaze status expires when world views stop rendering.");
        }

        using (var f = new Fixture())
        {
            f.Activate();
            FixedFoveationRuntime.SetEyeTracking(true);
            var view = f.World();
            var localEye = Quaternion.Angles(.3f, -.2f, 0);
            foreach (var head in new[] { Quaternion.identity, Quaternion.Angles(.8f, 0, 0), Quaternion.Angles(-1, .6f, -.4f), Quaternion.Angles(2.7f, -.5f, 1) })
            {
                foreach (var origin in new[] { new Vector3(), new Vector3(50, -20, 100), new Vector3(-.1f, 1.5f, .3f) })
                {
                    NovrEyeGaze.Pose = new NovrEyeGazePose(origin, head * localEye, origin, head);
                    APIBus.MainCamera.transform.rotation = head;
                    view.cameraData.xr.View = Matrix4x4.EyeView(head, origin);
                    f.Tick();
                    f.Render(ref view);
                    var direction = localEye * Vector3.forward;
                    Near(.5f + .5f * direction.x / direction.z, Native.AcquiredScopes[^1].X,
                        "Tracking-space head yaw/pitch/roll and translated origins cancel from horizontal gaze.");
                    Near(.5f - .5f * direction.y / direction.z, Native.AcquiredScopes[^1].Y,
                        "Tracking-space head yaw/pitch/roll and translated origins cancel from vertical gaze.");
                }
            }
            // Eye-view translation is deliberately large above: transforming a
            // direction must not inherit cockpit/seat/world position offsets.
            check(Native.LiveScopes.Count == 0 && FixedFoveationRuntime.IsAvailable,
                "All head orientations preserve successful scoped rendering.");
        }

        CantedViewChecks.Run(check);

        using (var f = new Fixture())
        {
            f.Tick();
            FixedFoveationRuntime.SetEyeTracking(true);
            check(FixedFoveationRuntime.EyeTrackingStatus.Contains("foveation is Off"),
                "Gaze preference does not imply that shading reduction is enabled.");
            var view = f.World();
            view.cameraData.xr.ThrowViewRead = true;
            f.Render(ref view);
            check(NovrEyeGaze.Calls == 0 && Native.AcquiredScopes.Count == 0 && !Native.Enabled,
                "Off with selected gaze remains completely dormant.");
            FixedFoveationRuntime.SetDiagnostics(true);
            f.Tick();
            f.Render(ref view);
            check(NovrEyeGaze.Calls == 0 && !Native.Enabled && Native.AcquiredScopes.Count == 1,
                "Off baseline measurement does not accidentally sample gaze or enable VRS.");
            check(view.cameraData.xr.ViewReads == 0 && FixedFoveationRuntime.IsAvailable,
                "Off diagnostics never request the optional eye view, even when its provider would throw.");
            Near(.5f, Native.AcquiredScopes[0].X, "Off measurements use the unchanged fixed center.");
        }

        using (var f = new Fixture(RuntimePlatform.LinuxPlayer))
        {
            f.Tick();
            FixedFoveationRuntime.SetEyeTracking(true);
            check(!ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value,
                "Unsupported platforms cannot opt into eye-tracked shading from the runtime controls.");
            ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value = true;
            check(FixedFoveationRuntime.EyeTrackingStatus.Contains("inactive"), "An imported preference reports unavailable hardware accurately.");
            var view = f.World();
            f.Render(ref view);
            check(NovrEyeGaze.Calls == 0 && Native.Constructed == 0, "Unsupported platforms never poll gaze for rendering or load the native library.");
            FixedFoveationRuntime.SetEyeTracking(false);
            check(!ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value, "Opt-out remains available everywhere.");
        }

        using (var f = new Fixture())
        {
            f.Activate();
            FixedFoveationRuntime.SetEyeTracking(true);
            NovrEyeGaze.ThrowRead = true;
            var view = f.World();
            view.cameraData.xr.Projection = new Matrix4x4 { m00 = 1, m11 = 1, m02 = .2f, m12 = -.1f };
            var warnings = Debug.Warnings.Count;
            for (var i = 0; i < 3; i++)
            {
                f.Tick();
                f.Render(ref view);
                Near(.4f, Native.AcquiredScopes[^1].X, "An eye input exception keeps the fixed X center.");
                Near(.45f, Native.AcquiredScopes[^1].Y, "An eye input exception keeps the fixed Y center.");
                check(FixedFoveationRuntime.IsAvailable && Native.Enabled && Native.LiveScopes.Count == 0,
                    "Optional eye input failures cannot fault or disable working VRS.");
            }
            check(Debug.Warnings.Count == warnings + 1, "Repeated eye input failures log once instead of flooding the frame loop.");
            check(FixedFoveationRuntime.EyeTrackingStatus.Contains("no eye gaze"), "An input exception reports the fixed fallback.");
            NovrEyeGaze.ThrowRead = false;
            SetPose();
            f.Tick();
            f.Render(ref view);
            check(Native.AcquiredScopes[^1].X > .4f && FixedFoveationRuntime.EyeTrackingStatus.Contains("following tracked eyes"),
                "Eye input recovers after a transient exception without restarting VRS.");
            f.Disable();
            f.Reenable();
            f.Tick();
            NovrEyeGaze.ThrowRead = true;
            f.Tick();
            f.Render(ref view);
            check(Debug.Warnings.Count == warnings + 2, "Re-enabling the runtime resets the bounded eye warning latch.");
        }

        using (var f = new Fixture())
        {
            f.Activate();
            FixedFoveationRuntime.SetEyeTracking(true);
            var nonWorld = f.World();
            nonWorld.cameraData.camera = new Camera();
            f.Render(ref nonWorld);
            check(NovrEyeGaze.Calls == 0 && Native.AcquiredScopes.Count == 0, "HUD, menu and other cameras remain full rate without sampling gaze.");
            XRSettings.enabled = false;
            f.Tick();
            var view = f.World();
            f.Render(ref view);
            check(NovrEyeGaze.Calls == 0 && !Native.Enabled && ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value,
                "Leaving VR stops gaze rendering but preserves the selected preference.");
        }
    }
}
