using NOVR.Rendering;

internal static class GazeFilterChecks
{
    internal static void Run(Action<bool, string> check)
    {
        void Near(float expected, float actual, string label) => check(Math.Abs(expected - actual) < .00001f, label);

        var stabilizer = new FoveationGazeStabilizer();
        float x = .5f, y = .5f;
        stabilizer.Stabilize(0, 2000, 2000, ref x, ref y);
        Near(.5f, x, "The first gaze sample is used unchanged.");
        x = .5f + 10f / 2000; y = .5f - 12f / 2000;
        stabilizer.Stabilize(0, 2000, 2000, ref x, ref y);
        Near(.5f, x, "Fixation jitter inside one tile keeps the held horizontal centre.");
        Near(.5f, y, "Fixation jitter inside one tile keeps the held vertical centre.");
        x = .5f + 17f / 2000; y = .5f;
        stabilizer.Stabilize(0, 2000, 2000, ref x, ref y);
        Near(.5f + 17f / 2000, x, "Leaving the one-tile box moves the centre immediately.");
        float rx = .9f, ry = .1f;
        stabilizer.Stabilize(1, 2000, 2000, ref rx, ref ry);
        Near(.9f, rx, "Each eye keeps its own held centre.");
        stabilizer.Reset();
        x = .5f + 18f / 2000; y = .5f;
        stabilizer.Stabilize(0, 2000, 2000, ref x, ref y);
        Near(.5f + 18f / 2000, x, "Reset drops the held centre so a fallback never inherits it.");
        foreach (var (eye, width, height) in new[] { (-1, 2000, 2000), (2, 2000, 2000), (0, 0, 2000), (0, 2000, -1) })
        {
            float ix = .3f, iy = .7f;
            stabilizer.Stabilize(eye, width, height, ref ix, ref iy);
            check(ix == .3f && iy == .7f, "Invalid eye or target dimensions pass the centre through untouched.");
        }

        var saccades = new GazeSaccadeDetector();
        static (float, float, float) Direction(double degrees) =>
            ((float)Math.Sin(degrees * Math.PI / 180), 0f, (float)Math.Cos(degrees * Math.PI / 180));
        var (dx, dy, dz) = Direction(0);
        check(!saccades.Update(dx, dy, dz, 1.000), "A first sample is never a saccade.");
        (dx, dy, dz) = Direction(.5);
        check(!saccades.Update(dx, dy, dz, 1.011), "Smooth pursuit (about 45 deg/s) does not widen the region.");
        (dx, dy, dz) = Direction(4.5);
        check(saccades.Update(dx, dy, dz, 1.022), "A 4 degree jump in 11 ms (over 300 deg/s) is a saccade.");
        (dx, dy, dz) = Direction(4.6);
        check(saccades.Update(dx, dy, dz, 1.100), "The widened region is held briefly after the eyes land.");
        check(!saccades.Update(dx, dy, dz, 1.300), "The hold expires once the eyes settle.");
        (dx, dy, dz) = Direction(40);
        check(!saccades.Update(dx, dy, dz, 2.0), "A long input gap carries no motion and cannot fake a saccade.");
        check(!saccades.Update(0, 0, 0, 2.01) && !saccades.Update(float.NaN, 0, 1, 2.02),
            "Degenerate or non-finite gaze resets motion state.");

        check(FixedFoveationPolicy.AngularScale(FixedFoveationPolicy.ReferenceProjectionScale, FixedFoveationPolicy.ReferenceProjectionScale) == 1,
            "The reference projection keeps preset radii unchanged.");
        var narrow = FixedFoveationPolicy.AngularScale(1.0f, 1.0f);
        var wide = FixedFoveationPolicy.AngularScale(.75f, .75f);
        check(narrow > 1 && wide < 1, "A narrower eye FOV scales radii up and a wider one scales them down to keep the protected angle.");
        foreach (var bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            check(FixedFoveationPolicy.AngularScale(bad, 1) == 1, "Invalid projections leave radii unscaled.");
        check(FixedFoveationPolicy.AngularScale(10, 10) == FixedFoveationPolicy.MaximumAngularScale &&
              FixedFoveationPolicy.AngularScale(.1f, .1f) == FixedFoveationPolicy.MinimumAngularScale, "Scaling is bounded.");

        foreach (var level in Enum.GetValues<FixedFoveationLevel>())
            foreach (var tracked in new[] { false, true })
                foreach (var saccade in new[] { false, true })
                    foreach (var scale in new[] { .5f, .7f, 1f, 1.3f, 2f, float.NaN })
                    {
                        FixedFoveationPolicy.Radii(level, tracked, saccade, scale, out var inner, out var outer);
                        check(inner >= .15f && inner <= 1.25f && outer > inner && outer <= 1.5f,
                            "Every combination stays inside the native backend's accepted radii.");
                    }
        FixedFoveationPolicy.Radii(FixedFoveationLevel.Strong, false, false, 1, out var fixedInner, out var fixedOuter);
        FixedFoveationPolicy.Radii(FixedFoveationLevel.Strong, out var presetInner, out var presetOuter);
        Near(presetInner, fixedInner, "Fixed foveation at the reference FOV uses the documented preset radius.");
        Near(presetOuter, fixedOuter, "Fixed foveation at the reference FOV uses the documented outer radius.");
        FixedFoveationPolicy.Radii(FixedFoveationLevel.Strong, true, false, 1, out var trackedInner, out _);
        FixedFoveationPolicy.Radii(FixedFoveationLevel.Strong, true, true, 1, out var saccadeInner, out _);
        Near(presetInner + FixedFoveationPolicy.GazeMargin, trackedInner, "Tracked gaze adds the lag margin.");
        Near(presetInner + FixedFoveationPolicy.GazeMargin + FixedFoveationPolicy.SaccadeMargin, saccadeInner, "Saccades add a further margin.");
        FixedFoveationPolicy.Radii(FixedFoveationLevel.Strong, false, true, 1, out var fixedSaccadeInner, out _);
        Near(presetInner, fixedSaccadeInner, "Without tracked gaze a saccade flag cannot widen the fixed region.");
    }
}
