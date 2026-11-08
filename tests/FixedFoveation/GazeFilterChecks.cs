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

        // Radii are the documented presets for fixed and eye-tracked foveation
        // alike: no lag margin or field-of-view scaling widens the full-rate area.
        foreach (var (level, inner, outer) in new[]
        {
            (FixedFoveationLevel.Mild, .70f, .95f), (FixedFoveationLevel.Balanced, .55f, .85f),
            (FixedFoveationLevel.Strong, .40f, .75f), (FixedFoveationLevel.Extreme, .30f, .60f),
            (FixedFoveationLevel.Maximum, .18f, .40f)
        })
        {
            FixedFoveationPolicy.Radii(level, out var actualInner, out var actualOuter);
            Near(inner, actualInner, "Preset full-rate radius is unchanged.");
            Near(outer, actualOuter, "Preset middle-region radius is unchanged.");
        }
    }
}
