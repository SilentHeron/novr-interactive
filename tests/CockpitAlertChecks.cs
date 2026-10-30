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
                var rear = CockpitAlertMath.Scope(0, -distance, 70); yes(rear == (0, -70));
                var right = CockpitAlertMath.Scope(distance, 0, 70); yes(right == (70, 0));
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
    }
}
