using NOVR;
using NOVR.Rendering;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using Native = NOVR.Rendering.FixedFoveationNative;

internal static class StrongerLevelsChecks
{
    internal static void Run(Action<bool, string> check)
    {
        void RenderPair(Fixture f, bool brokenView = false, bool throwView = false)
        {
            for (var eye = 0; eye < 2; eye++)
            {
                var data = f.World(eye);
                data.cameraData.xr.Projection = new Matrix4x4 { m00 = 1, m11 = 1, m02 = eye == 0 ? .2f : -.2f, m12 = .1f };
                if (brokenView)
                    data.cameraData.xr.View.m00 = float.NaN;
                data.cameraData.xr.ThrowViewRead = throwView;
                f.Render(ref data);
            }
        }
        void PairLevel(FixedFoveationLevel expected, string label) => check(
            Native.AcquiredScopes[^2].Eye == 0 && Native.AcquiredScopes[^1].Eye == 1 &&
            Native.AcquiredScopes[^2].Level == expected && Native.AcquiredScopes[^1].Level == expected, label);
        void FixedCenters(string label) => check(
            Math.Abs(Native.AcquiredScopes[^2].X - .4f) < .00001f &&
            Math.Abs(Native.AcquiredScopes[^1].X - .6f) < .00001f &&
            Math.Abs(Native.AcquiredScopes[^1].Y - .55f) < .00001f, label);

        foreach (var level in Enum.GetValues<FixedFoveationLevel>().Where(value => value != FixedFoveationLevel.Off))
        {
            using var f = new Fixture();
            f.Tick();
            FixedFoveationRuntime.SetLevel((int)level);
            f.Tick();
            NovrEyeGaze.State = NovrEyeGazeReadState.Untracked;
            RenderPair(f);
            PairLevel(level, "Fixed " + level + " submits its selected strength to both eyes without requiring tracking.");
            FixedCenters("Fixed " + level + " preserves each eye's optical center.");
            check(NovrEyeGaze.Calls == 0 && Native.Enabled, "Fixed strengths do not poll gaze or depend on eye-tracking availability.");

            FixedFoveationRuntime.SetEyeTracking(true);
            var fallback = level > FixedFoveationLevel.Strong ? FixedFoveationLevel.Strong : level;
            var enables = Native.Enables.Count;
            foreach (var state in new[] { NovrEyeGazeReadState.Unavailable, NovrEyeGazeReadState.Unsupported,
                         NovrEyeGazeReadState.Unfocused, NovrEyeGazeReadState.Untracked,
                         NovrEyeGazeReadState.Stale, NovrEyeGazeReadState.Invalid })
            {
                NovrEyeGaze.State = state;
                f.Tick();
                RenderPair(f);
                PairLevel(fallback, level + ": " + state + " uses the conservative actual fallback strength on both eyes.");
                FixedCenters("Lost gaze uses fixed optical centers rather than a retained gaze direction.");
                check(ModConfiguration.Instance.FixedFoveation.RequestedLevel == level &&
                      ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value,
                    "Fallback preserves selected strength and eye-tracking preference.");
                check(Native.Enabled && Native.Enables.Count == enables && Native.LiveScopes.Count == 0,
                    "Tracking loss changes the scoped profile without repeatedly re-enabling the driver or leaking scopes.");
            }

            NovrEyeGaze.State = NovrEyeGazeReadState.Tracked;
            NovrEyeGaze.Pose = new NovrEyeGazePose(default, Quaternion.Angles(.2f, -.1f, 0), default, Quaternion.identity);
            f.Tick();
            RenderPair(f);
            PairLevel(level, "Valid eye tracking restores selected " + level + " automatically.");
            check(Native.AcquiredScopes[^2].X > .4f && Native.AcquiredScopes[^1].X > .6f,
                "Recovered profiles follow the current gaze in both asymmetric eyes.");

            foreach (var age in new[] { .049, .05, .0501, .09 })
            {
                NovrEyeGaze.Pose = new NovrEyeGazePose(default, Quaternion.Angles(.2f, -.1f, 0), default, Quaternion.identity, age);
                f.Tick();
                RenderPair(f);
                var expected = level > FixedFoveationLevel.Strong && age > .05 ? FixedFoveationLevel.Strong : level;
                PairLevel(expected, "Aggressive levels require at most 50ms-old input while legacy strengths retain their existing freshness window.");
                if (expected != level)
                {
                    FixedCenters("Too-old aggressive gaze uses conservative fixed centers.");
                    check(FixedFoveationRuntime.EyeTrackingStatus.Contains("fresh eye tracking") &&
                          ModConfiguration.Instance.FixedFoveation.RequestedLevel == level,
                        "Age fallback is visibly stale input and does not erase the user's selected strength.");
                }
            }

            foreach (var failure in new[] { "backward", "invalid view", "view exception", "input exception" })
            {
                NovrEyeGaze.Pose = new NovrEyeGazePose(default,
                    failure == "backward" ? Quaternion.Angles(MathF.PI, 0, 0) : Quaternion.identity, default, Quaternion.identity);
                NovrEyeGaze.ThrowRead = failure == "input exception";
                f.Tick();
                RenderPair(f, failure == "invalid view", failure == "view exception");
                PairLevel(fallback, "Unusable " + failure + " cannot leave an aggressive gaze profile at the fixed center.");
                FixedCenters("Projection/input failure falls back to optical centers.");
                check(FixedFoveationRuntime.IsAvailable && Native.Enabled,
                    "Optional gaze failure retains the validated rendering backend.");
            }
            NovrEyeGaze.ThrowRead = false;
            NovrEyeGaze.Pose = new NovrEyeGazePose(default, Quaternion.Angles(1.3f, 0, 0), default, Quaternion.identity);
            f.Tick();
            RenderPair(f);
            PairLevel(level, "Valid gaze beyond one eye's frustum keeps the selected profile while clamping its center.");
            check(Native.AcquiredScopes[^2].X == 1 && Native.AcquiredScopes[^1].X == 1,
                "Peripheral tracked gaze protects the visible edge instead of masquerading as tracking loss.");

            FixedFoveationRuntime.SetEyeTracking(false);
            NovrEyeGaze.State = NovrEyeGazeReadState.Untracked;
            var reads = NovrEyeGaze.Calls;
            RenderPair(f);
            PairLevel(level, "Opting into fixed mode restores the selected profile instead of retaining gaze fallback.");
            FixedCenters("Fixed-mode switch restores optical centers immediately.");
            check(NovrEyeGaze.Calls == reads && ModConfiguration.Instance.FixedFoveation.RequestedLevel == level,
                "Eye-mode toggles preserve strength and stop gaze polling.");
            FixedFoveationRuntime.SetLevel(0);
            for (var frame = 0; frame < 4; frame++)
                f.Tick();
            var scopes = Native.AcquiredScopes.Count;
            RenderPair(f);
            check(!Native.Enabled && Native.AcquiredScopes.Count == scopes && NovrEyeGaze.Calls == reads,
                "Every strength returns to dormant Off without stale aggressive scopes.");
        }
    }
}
