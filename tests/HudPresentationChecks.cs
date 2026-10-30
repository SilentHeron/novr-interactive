using NOVR.VrUi.SpecialBehavior;

internal static class HudPresentationChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> yes)
    {
        void near(float a, float b) => yes(Math.Abs(a - b) < .002f);
        check("Symbol resizing preserves a nonzero aim anchor without cumulative shrink", () =>
        {
            foreach (var anchor in new[] { -180f, 0, 320f })
            foreach (var scale in new[] { .2f, .45f, 1f })
            {
                near(HudPresentationMath.ScaleCoordinate(anchor, anchor, scale), anchor);
                var a = HudPresentationMath.ScaleCoordinate(anchor - 20, anchor, scale);
                var b = HudPresentationMath.ScaleCoordinate(anchor + 20, anchor, scale);
                near((a + b) / 2, anchor); near(b - a, 40 * scale);
                for (var frame = 0; frame < 100; frame++) near(HudPresentationMath.ScaleCoordinate(anchor + 20, anchor, scale), b);
            }
        });
        check("Zoom moves target anchors with magnification but keeps glyph size constant", () =>
        {
            foreach (var depth in new[] { 500f, 1000f, 1800f })
            foreach (var zoom in new[] { 1f, 2f, 4f, 8f })
            {
                var anchor = HudPresentationMath.Project(50, depth, 3, zoom);
                var a = HudPresentationMath.Symbol(HudPresentationMath.Project(45, depth, 3, zoom), anchor, zoom);
                var b = HudPresentationMath.Symbol(HudPresentationMath.Project(55, depth, 3, zoom), anchor, zoom);
                near((a + b) / 2, anchor);
                near(b - a, HudPresentationMath.Project(10, depth, 3, 1));
            }
        });
        check("Zoom projected lines retain their world endpoint separation", () =>
        {
            foreach (var zoom in new[] { 1f, 2f, 4f })
            {
                var a = HudPresentationMath.Project(-50, 1000, 3, zoom);
                var b = HudPresentationMath.Project(80, 1000, 3, zoom);
                near(b - a, 390 * zoom);
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
        check("Dashboard widening preserves button and lettering widths", () =>
        {
            foreach (var aspect in new[] { .85f, 1f, 1.4f, 1.75f })
            foreach (var center in new[] { -.315f, 0f, .315f })
            {
                var a = HudPresentationMath.PanelVertex(center - .02f, center, aspect, false);
                var b = HudPresentationMath.PanelVertex(center + .02f, center, aspect, false);
                near(b - a, .04f); near((a + b) / 2, center * aspect);
                near(HudPresentationMath.PanelVertex(.385f, 0, aspect, true) - HudPresentationMath.PanelVertex(-.385f, 0, aspect, true), .77f * aspect);
            }
        });
    }
}
