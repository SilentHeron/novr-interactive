using NOVR;
using NOVR.Rendering;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using Native = NOVR.Rendering.FixedFoveationNative;

internal static class StrongerMeasurementChecks
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var level in new[] { FixedFoveationLevel.Extreme, FixedFoveationLevel.Maximum })
        {
            using var f = new Fixture();
            f.Tick();
            FixedFoveationRuntime.SetLevel((int)level);
            FixedFoveationRuntime.SetEyeTracking(true);
            FixedFoveationRuntime.SetDiagnostics(true);
            f.Tick();
            NovrEyeGaze.Pose = new NovrEyeGazePose(default, Quaternion.Angles(.15f, -.05f, 0), default, Quaternion.identity);
            void RenderPair()
            {
                // Update before rendering matches the real lifecycle and expires
                // the previous reader sample before each new test frame.
                f.Tick();
                for (var eye = 0; eye < 2; eye++)
                {
                    var data = f.World(eye);
                    f.Render(ref data);
                }
            }
            void Samples(int mode, int rate)
            {
                Native.LeftSequence++;
                Native.RightSequence++;
                Native.LeftSampleMode = Native.RightSampleMode = mode;
                Native.LeftSampleCoarsestRate = Native.RightSampleCoarsestRate = rate;
                Native.LeftSampleRateClamped = Native.RightSampleRateClamped = rate == 2 && mode > 3;
                Native.LeftGpuMs = 2.9;
                Native.RightGpuMs = 3.1;
            }
            void Waiting(string label) => check(FixedFoveationRuntime.Status.Contains("waiting for matching eye samples") &&
                !FixedFoveationRuntime.Status.Contains("World opaque GPU"), label);

            RenderPair();
            Samples((int)level, 4);
            f.Tick();
            check(FixedFoveationRuntime.Status.StartsWith(level + ":") &&
                  FixedFoveationRuntime.Status.Contains("1×1 / 2×2 / 4×4") &&
                  FixedFoveationRuntime.Status.Contains("World opaque GPU:") &&
                  !FixedFoveationRuntime.Status.Contains("fallback"),
                "Fresh tracked " + level + " measurements match the actual applied 4×4 profile.");

            // The first readback after losing gaze can still belong to the
            // previous aggressive frame. It must not be labelled as fallback.
            NovrEyeGaze.State = NovrEyeGazeReadState.Stale;
            RenderPair();
            Samples((int)level, 4);
            f.Tick();
            Waiting("Delayed aggressive measurements are rejected after conservative fallback activates.");
            check(FixedFoveationRuntime.Status.StartsWith(level + " selected; Strong fallback active in both eyes."),
                "The status distinguishes selected strength from actual conservative fallback.");
            RenderPair();
            Samples((int)FixedFoveationLevel.Strong, 2);
            f.Tick();
            check(FixedFoveationRuntime.Status.Contains("World opaque GPU (Strong fallback):") &&
                  FixedFoveationRuntime.Status.Contains("1×1 / 2×1 / 2×2"),
                "Fresh fallback measurements explicitly identify Strong and its actual shading rates.");
            check(ModConfiguration.Instance.FixedFoveation.RequestedLevel == level,
                "Showing actual fallback costs never overwrites the user's requested test strength.");

            NovrEyeGaze.State = NovrEyeGazeReadState.Tracked;
            RenderPair();
            Samples((int)FixedFoveationLevel.Strong, 2);
            f.Tick();
            Waiting("Delayed fallback measurements are rejected once the selected aggressive profile resumes.");
            RenderPair();
            Samples((int)level, 4);
            f.Tick();
            check(FixedFoveationRuntime.Status.Contains("World opaque GPU:") && !FixedFoveationRuntime.Status.Contains("fallback"),
                "Fresh matching selected-profile measurements recover after tracking resumes.");

            foreach (var mismatch in new[] { "sample rates differ", "applied rates differ", "sample rate stale", "applied profile differs", "sample mode differs" })
            {
                RenderPair();
                Samples((int)level, 4);
                switch (mismatch)
                {
                    case "sample rates differ":
                        Native.RightSampleCoarsestRate = 2;
                        break;
                    case "applied rates differ":
                        Native.RightCoarsestRate = 2;
                        break;
                    case "sample rate stale":
                        Native.LeftSampleCoarsestRate = Native.RightSampleCoarsestRate = 2;
                        break;
                    case "applied profile differs":
                        Native.RightProfile = (int)FixedFoveationLevel.Strong - 1;
                        break;
                    case "sample mode differs":
                        Native.RightSampleMode = (int)FixedFoveationLevel.Strong;
                        break;
                }
                f.Tick();
                Waiting("Mixed/stale per-eye data cannot masquerade as comparable " + level + " measurements: " + mismatch + ".");
                if (mismatch == "applied profile differs")
                    check(!FixedFoveationRuntime.Status.Contains("shading commands applied to both eyes"),
                        "An applied profile mismatch also blocks the active-both-eyes claim.");
            }

            foreach (var reason in new[] { "msaa", "driver" })
            {
                RenderPair();
                Native.LeftCoarsestRate = Native.RightCoarsestRate = 2;
                Native.LeftRateClamped = Native.RightRateClamped = true;
                Native.LeftClampReason = Native.RightClampReason = reason;
                Samples((int)level, 2);
                f.Tick();
                check(FixedFoveationRuntime.Status.Contains("World opaque GPU:") &&
                      FixedFoveationRuntime.Status.Contains("1×1 / 2×2 / 2×2") &&
                      FixedFoveationRuntime.Status.Contains(reason == "msaa" ? "MSAA limit" : "driver limit") &&
                      !FixedFoveationRuntime.Status.Contains("4×4 shading"),
                    "Supported 2×2 fallback rates are measured and labelled with their actual " + reason + " limit.");
            }
            check(Native.LiveScopes.Count == 0 && Native.Enabled && FixedFoveationRuntime.IsAvailable,
                "Measurement rejection never disables valid shading or leaks ownership.");
        }
    }
}
