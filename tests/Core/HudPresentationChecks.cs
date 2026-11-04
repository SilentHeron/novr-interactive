using NOVR.VrUi.SpecialBehavior;

internal static class HudPresentationChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> yes)
    {
        void near(float a, float b) => yes(Math.Abs(a - b) < .002f);
        check("Aircraft target labels shrink by affiliation while preserving user scale and flat mode", () =>
        {
            foreach (var userScale in new[] { .2f, .55f, 1f })
            {
                near(HudPresentationMath.AircraftLabelScale(userScale, AircraftLabelAffiliation.Friendly, true), userScale * .65f);
                near(HudPresentationMath.AircraftLabelScale(userScale, AircraftLabelAffiliation.Hostile, true), userScale * .8f);
                near(HudPresentationMath.AircraftLabelScale(userScale, AircraftLabelAffiliation.Other, true), userScale);
                foreach (var affiliation in Enum.GetValues<AircraftLabelAffiliation>())
                    near(HudPresentationMath.AircraftLabelScale(userScale, affiliation, false), 1);
            }
        });

        check("Symbol resizing preserves a nonzero aim anchor without cumulative shrink", () =>
        {
            foreach (var anchor in new[] { -180f, 0, 320f })
                foreach (var scale in new[] { .2f, .45f, 1f })
                {
                    near(HudPresentationMath.ScaleCoordinate(anchor, anchor, scale), anchor);
                    var a = HudPresentationMath.ScaleCoordinate(anchor - 20, anchor, scale);
                    var b = HudPresentationMath.ScaleCoordinate(anchor + 20, anchor, scale);
                    near((a + b) / 2, anchor);
                    near(b - a, 40 * scale);
                    for (var frame = 0; frame < 100; frame++)
                        near(HudPresentationMath.ScaleCoordinate(anchor + 20, anchor, scale), b);
                }
        });
        check("Range widgets fit the magnifier and respect the user's scale", () =>
        {
            foreach (var width in new[] { 1f, 180f, 1000f })
                foreach (var height in new[] { 1f, 350f, 1600f })
                {
                    var small = HudPresentationMath.WidgetFit(width, height, .25f);
                    var large = HudPresentationMath.WidgetFit(width, height, .5f);
                    near(large, small * 2);
                    yes(width * large <= 180.001f && height * large <= 350.001f);
                }
        });

    }
}
