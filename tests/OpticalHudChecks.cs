using NOVR.MotionControls;

internal static class OpticalHudChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> yes)
    {
        check("Contextual cue stays quiet outside acquisition and obeys off and flight state", () =>
        {
            yes(!TargetCueVisibility.Show(TargetCueMode.Context, true, false, false));
            yes(TargetCueVisibility.Show(TargetCueMode.Context, true, true, false));
            yes(TargetCueVisibility.Show(TargetCueMode.Context, true, false, true));
            yes(TargetCueVisibility.Show(TargetCueMode.Always, true, false, false));
            foreach (var mode in Enum.GetValues<TargetCueMode>()) yes(!TargetCueVisibility.Show(mode, false, true, true));
            yes(!TargetCueVisibility.Show(TargetCueMode.Off, true, true, true));
        });
        check("Optical HUD suspends during non-flight contexts regardless of sensor or target state", () =>
        {
            foreach (var sensor in new[] { false, true })
            foreach (var landing in new[] { false, true })
            foreach (var track in new[] { false, true })
            foreach (var targets in new[] { 0, 1, 4 })
            {
                yes(OpticalHudMath.Mode(false, true, sensor, landing, track, targets) == OpticalHudMode.Hidden);
                yes(OpticalHudMath.Mode(true, false, sensor, landing, track, targets) == OpticalHudMode.Hidden);
            }
        });
        check("Optical HUD respects missing optics and native landing-camera ownership", () =>
        {
            yes(OpticalHudMath.Mode(true, true, false, false, true, 1) == OpticalHudMode.Unavailable);
            yes(OpticalHudMath.Mode(true, true, true, true, false, 0) == OpticalHudMode.Landing);
            yes(OpticalHudMath.Mode(true, true, true, true, true, 1) == OpticalHudMode.Landing);
        });
        check("Optical tracking requires a deliberate selection and returns to HEAD without targets", () =>
        {
            yes(OpticalHudMath.Mode(true, true, true, false, false, 4) == OpticalHudMode.Head);
            yes(OpticalHudMath.Mode(true, true, true, false, true, 1) == OpticalHudMode.Track);
            yes(OpticalHudMath.Mode(true, true, true, false, true, 0) == OpticalHudMode.Head);
        });
        check("Head optical projection preserves image aspect and requested angular magnification", () =>
        {
            foreach (var width in new[] { 12f, 20, 26 })
            foreach (var aspect in new[] { .5f, 1, 4f / 3, 3 })
            foreach (var zoom in new[] { 1f, 2, 6 })
            {
                var imageWidth = OpticalHudMath.Width(width, 3);
                var fov = OpticalHudMath.SensorFov(width, aspect, zoom);
                var magnification = (imageWidth / aspect) / (6 * Math.Tan(fov * Math.PI / 360));
                yes(Math.Abs(magnification - zoom) < .00001);
                yes(OpticalHudMath.SelectionRadius(fov) < 100);
                yes(OpticalHudMath.SelectionRadius(fov) < 1000 * Math.Tan(fov * Math.PI / 360));
            }
        });
        check("Malformed optical configuration stays finite and within supported zoom bounds", () =>
        {
            foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -500f, 10000f })
            {
                var fov = OpticalHudMath.SensorFov(bad, bad, bad);
                yes(float.IsFinite(fov) && fov > 0 && fov < 60);
                yes(float.IsFinite(OpticalHudMath.Width(bad, 3)));
            }
        });
    }
}
