using NOVR.VrUi.SpecialBehavior;

internal static class HudWidgetMotionChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var at90 = Step(90, .2f);
        check(Math.Abs(at90 - Step(60, .2f)) < .00001f && Math.Abs(at90 - Step(120, .2f)) < .00001f,
            "Held motion smoothing has the same response at 60, 90 and 120 Hz");
        check(Step(100, .04f) > .63f && Step(100, .04f) < .64f && at90 > .99f,
            "Light smoothing follows intentional movement promptly without overshoot");
        var value = 0f; var max = 0f;
        for (var frame = 0; frame < 180; frame++)
        {
            var target = .003f * MathF.Sin(frame / 90f * 12 * 2 * MathF.PI);
            value += (target - value) * HudWidgetMotionMath.Blend(1f / 90);
            if (frame > 90) max = Math.Max(max, Math.Abs(value));
        }
        check(max < .0011f, "Three-millimeter 12 Hz tracking tremor is reduced below 1.1 millimeters");
        var angle = HudWidgetMotionMath.Angle(179, -179, .5f);
        check(Math.Abs(angle) == 180, "Yaw crosses the 180-degree seam by the short path");
        check(HudWidgetMotionMath.Blend(0) == 0 && HudWidgetMotionMath.Blend(-1) == 0 &&
            HudWidgetMotionMath.Blend(float.NaN) == 0 && HudWidgetMotionMath.Blend(1) <= 1,
            "Paused/invalid time stays put and frame hitches cannot overshoot");

        static float Step(int hz, float seconds)
        {
            var result = 0f;
            for (var n = 0; n < (int)Math.Round(hz * seconds); n++)
                result += (1 - result) * HudWidgetMotionMath.Blend(1f / hz);
            return result;
        }
    }
}
