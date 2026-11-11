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
            near(HudPresentationMath.SymbolFollowFactor(true, true, .38f), 1);
            near(HudPresentationMath.SymbolFollowFactor(true, true, .57f), 1.5f);
            near(HudPresentationMath.SymbolFollowFactor(false, true, .57f), 1);
            near(HudPresentationMath.SymbolFollowFactor(true, false, .57f), 1);
            foreach (var bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                near(HudPresentationMath.SymbolFollowFactor(true, true, bad), 1);
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
        check("A jammed map icon shivers inside the map instead of flying metres through the cockpit", () =>
        {
            // The game's own step: up to 2.5 world units per frame, which are metres in VR.
            foreach (var unit in new[] { .0003f, .003f })
                foreach (var (nx, ny) in new[] { (2.5f, 0f), (0f, -2.5f), (1.5f, 2f), (-.4f, .3f) })
                {
                    HudPresentationMath.MapJamOffset(nx, ny, unit, out var x, out var y);
                    yes(Math.Abs(x - nx * unit) < 1e-7f && Math.Abs(y - ny * unit) < 1e-7f);
                    yes(Math.Sqrt(x * x + y * y) <= 2.5f * unit + 1e-7f && Math.Sqrt(x * x + y * y) <= .0076f);
                }
            // A map that reports a world-sized scale still cannot throw an icon further than the cap, and keeps the direction.
            foreach (var unit in new[] { 1f, 40f, -1f })
            {
                HudPresentationMath.MapJamOffset(1.5f, 2f, unit, out var x, out var y);
                yes(Math.Abs(Math.Sqrt(x * x + y * y) - HudPresentationMath.MapJamMaxMetres) < 1e-6f && Math.Abs(x / y - .75f) < 1e-4f && x > 0 && y > 0);
            }
            // Five steps between two of the game's own icon refreshes stay within a hand's width.
            yes(5 * HudPresentationMath.MapJamMaxMetres <= .08f);
            foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                HudPresentationMath.MapJamOffset(bad, 1, .003f, out var x, out var y);
                yes(x == 0 && y == 0);
                HudPresentationMath.MapJamOffset(1, bad, .003f, out x, out y);
                yes(x == 0 && y == 0);
                HudPresentationMath.MapJamOffset(1, 1, bad, out x, out y);
                yes(x == 0 && y == 0);
            }
            HudPresentationMath.MapJamOffset(2, 2, 0, out var zx, out var zy);
            yes(zx == 0 && zy == 0);
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
