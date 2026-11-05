using NOVR.VrUi.SpecialBehavior;

internal static class CockpitAlertChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> yes)
    {
        check("Threat clock bearings preserve forward, aft and both sides across all sectors", () =>
        {
            for (var hour = 0; hour < 12; hour++)
                foreach (var offset in new[] { -14.9, 0, 14.9 })
                {
                    var angle = (hour * 30 + offset) * Math.PI / 180;
                    yes(CockpitAlertMath.Clock((float)Math.Sin(angle), (float)Math.Cos(angle)) == (hour == 0 ? 12 : hour));
                }
        });
        check("Threat scope retains rear bearings and never presents radius as distance", () =>
        {
            foreach (var distance in new[] { .01f, 1, 1000, 100000 })
            {
                var rear = CockpitAlertMath.Scope(0, -distance, 70);
                yes(rear == (0, -70));
                var right = CockpitAlertMath.Scope(distance, 0, 70);
                yes(right == (70, 0));
                var oblique = CockpitAlertMath.Scope(distance, -distance, 70);
                yes(Math.Abs(oblique.x - 49.497475f) < .001 && Math.Abs(oblique.y + 49.497475f) < .001);
            }
            yes(CockpitAlertMath.Scope(0, 0, 70) == (0, 0));
            yes(CockpitAlertMath.Scope(float.NaN, 0, 70) == (0, 0));
        });
        check("Compact feed keeps newest entries, strips size markup and handles expiry to empty", () =>
        {
            yes(CockpitAlertMath.LatestFeed("old\n<color=red>one</color>\n<size=400>two</size>\nthree") == "one\ntwo\nthree");
            yes(CockpitAlertMath.LatestFeed("a\r\n\r\nb\nc\nd\n") == "b\nc\nd");
            yes(CockpitAlertMath.LatestFeed("<b>one</b>") == "one");
            yes(CockpitAlertMath.LatestFeed("") == "");
            yes(CockpitAlertMath.LatestFeed("one", 0) == "");
        });
        check("Crash and lost-aircraft notifications remain compact until a cockpit returns", () =>
        {
            yes(CockpitFeedPresentation.Choose(true, true, true, true, true, false) == CockpitFeedMode.Cockpit);
            // Disabled aircraft, detached cockpit, then a cleared local aircraft
            // all use the same recovery mode; flight-controls readiness is false.
            yes(CockpitFeedPresentation.Choose(true, true, true, false, true, true) == CockpitFeedMode.Recovery);
            yes(CockpitFeedPresentation.Choose(true, true, true, true, true, false) == CockpitFeedMode.Cockpit);
            yes(CockpitFeedPresentation.Choose(true, true, true, false, true, false) == CockpitFeedMode.Native);
        });
        check("Expanded recovery interfaces keep compact notifications; living cockpit menus retain native history", () =>
        {
            yes(CockpitFeedPresentation.Choose(true, true, false, false, true, true) == CockpitFeedMode.Recovery);
            yes(CockpitFeedPresentation.Choose(true, true, true, false, true, true) == CockpitFeedMode.Recovery);
            yes(CockpitFeedPresentation.Choose(true, true, false, true, true, false) == CockpitFeedMode.Native);
        });
        check("Recovery feed cannot activate outside VR gameplay or without an eligible view", () =>
        {
            yes(CockpitFeedPresentation.Choose(false, true, true, false, true, true) == CockpitFeedMode.Native);
            yes(CockpitFeedPresentation.Choose(true, false, true, false, true, true) == CockpitFeedMode.Native);
            yes(CockpitFeedPresentation.Choose(true, true, true, false, false, true) == CockpitFeedMode.Native);
            yes(CockpitFeedPresentation.Choose(false, true, true, true, true, false) == CockpitFeedMode.Native);
        });
        check("Direct spectator join owns notifications before any cockpit and hides presentation under the map", () =>
        {
            yes(CockpitFeedPresentation.Choose(true, true, true, false, false, true, true) == CockpitFeedMode.Spectator);
            yes(CockpitFeedPresentation.Choose(true, true, false, false, false, true, true) == CockpitFeedMode.Native);
            yes(CockpitFeedPresentation.SuppressNative(true, true, false, true));
            yes(!CockpitFeedPresentation.SuppressNative(false, true, false, true));
            yes(!CockpitFeedPresentation.SuppressNative(true, false, false, true));
            yes(CockpitFeedPresentation.Choose(false, true, true, false, false, true, true) == CockpitFeedMode.Native);
            yes(CockpitFeedPresentation.Choose(true, false, true, false, false, true, true) == CockpitFeedMode.Native);
            yes(CockpitFeedPresentation.Choose(true, true, true, true, true, false, true) == CockpitFeedMode.Cockpit);
        });
        check("Spectator notification panel stays below-left and readable throughout its independent overlay scale", () =>
        {
            var normal = CockpitFeedPresentation.ViewPlacement(1);
            var previousDefaultAngularScale = .0007f * .65f * (460f / 520f) / .85f;
            yes(normal.scale / normal.z > previousDefaultAngularScale * 1.35f);
            yes(normal.scale / normal.z < previousDefaultAngularScale * 1.5f);
            foreach (var scale in new[] { -10f, .7f, 1f, 1.3f, 100f, float.NaN, float.NegativeInfinity, float.PositiveInfinity })
            {
                var p = CockpitFeedPresentation.ViewPlacement(scale);
                // Test full panel bounds, not only the text's origin: even the
                // largest panel must leave the forward aiming region clear.
                yes(float.IsFinite(p.scale) && p.scale > 0 && p.z > 0);
                yes(p.x + 520 * p.scale * .5f < -.1f);
                yes(p.y + 300 * p.scale * .5f < -.1f);
                yes(p.x == normal.x && p.y == normal.y && p.z == normal.z);
            }
            yes(CockpitFeedPresentation.ViewPlacement(float.NaN) == normal);
            yes(CockpitFeedPresentation.ViewPlacement(100) == CockpitFeedPresentation.ViewPlacement(1.3f));
        });
    }
}
