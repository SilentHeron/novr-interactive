using NOVR.VrUi.SpecialBehavior;

internal static class WristDockChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var dock = new HudWidgetWristDock();
        check(!dock.Update(true, 0) && !dock.BecameReady, "Entering a wrist region does not instantly arm attachment");
        check(!dock.Update(true, .09f), "Brief wrist crossings do not arm attachment");
        check(dock.Update(true, .181f) && dock.BecameReady, "Stable wrist hover arms after its short dwell");
        check(dock.Update(true, .20f) && !dock.BecameReady, "Ready haptic is emitted once, not on every frame");
        check(dock.Release(true), "Deliberate grip release completes an armed attachment");
        check(!dock.Ready && !dock.BecameReady && !dock.Release(true), "A release consumes readiness exactly once");

        dock.Update(true, 1);
        dock.Update(true, 1.181f);
        check(!dock.Release(false) && !dock.Ready, "Forced release cancels an armed attachment instead of mounting it");
        dock.Update(true, 2);
        check(!dock.Release(true), "Releasing before the preview arms leaves the widget unattached");

        foreach (var loss in new[] { "outside wrist region", "wrist tracking lost", "target hand gripping", "two-hand resize", "quick wheel open", "edit mode ended" })
        {
            dock.Reset();
            dock.Update(true, 3);
            dock.Update(true, 3.181f);
            check(!dock.Update(false, 3.20f) && !dock.Ready && !dock.BecameReady,
                $"Ineligibility ({loss}) clears the preview immediately");
            check(!dock.Release(true), $"Release after {loss} cannot attach using stale readiness");
        }

        dock.Update(true, 4);
        dock.Update(true, 4.15f);
        dock.Update(false, 4.16f);
        check(!dock.Update(true, 4.17f) && !dock.Update(true, 4.25f), "Leaving and reentering starts a fresh dwell");
        check(dock.Update(true, 4.36f) && dock.BecameReady, "A new complete hover can arm again");
        dock.Reset();
        check(!dock.Ready && !dock.BecameReady && !dock.Release(true), "Context reset cannot be mistaken for a release");

        dock.Update(true, 5);
        check(!dock.Update(true, 25), "A long render stall cannot supply the missing stable hover samples");
        check(!dock.Update(true, 25.10f), "Post-stall dwell starts at the current observation");
        check(dock.Update(true, 25.19f), "Stable observations after a stall can arm normally");
        check(!dock.Update(true, 26) && !dock.BecameReady, "A stall also invalidates an already-ready preview");

        dock.Reset();
        dock.Update(true, 30);
        dock.Update(true, 30.19f);
        check(!dock.Update(true, 1) && !dock.Ready, "A backwards clock clears stale readiness");
        check(!dock.Update(true, 1.10f) && dock.Update(true, 1.19f), "A new monotonic interval re-arms after a clock reset");
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            check(!dock.Update(true, invalid) && !dock.Ready && !dock.BecameReady, "Nonfinite timestamps clear readiness");
            check(!dock.Update(true, 10), "Recovery from invalid time begins a fresh dwell");
            check(dock.Update(true, 10.19f), "Valid timestamps recover after invalid time");
        }
        dock.Reset();
        dock.Update(true, 50);
        for (var i = 0; i < 20; i++)
            check(!dock.Update(true, 50), "Repeated observations at one timestamp do not accumulate dwell");

        check(HudWidgetWristDock.IsNear(0, 0, 0, .08f, .06f), "Centered contact is eligible");
        check(HudWidgetWristDock.IsNear(.10f, .08f, .099f, .08f, .06f), "Small padded-edge contact is eligible within the plane distance");
        check(HudWidgetWristDock.IsNear(-.10f, -.08f, -.099f, .08f, .06f), "Proximity treats opposite edges and both plane sides consistently");
        check(!HudWidgetWristDock.IsNear(.111f, 0, 0, .08f, .06f), "Beyond the padded horizontal edge cannot attach");
        check(!HudWidgetWristDock.IsNear(0, .091f, 0, .08f, .06f), "Beyond the padded vertical edge cannot attach");
        check(!HudWidgetWristDock.IsNear(0, 0, .101f, .08f, .06f), "Passing farther in front of a wrist cannot attach");
        check(!HudWidgetWristDock.IsNear(.221f, 0, 0, 2, 2), "Large panels cannot attach from a far corner");
        check(!HudWidgetWristDock.IsNear(.16f, .16f, 0, 2, 2), "Center distance is radial, not a box");
        check(!HudWidgetWristDock.IsNear(.20f, 0, .10f, 2, 2), "Plane distance contributes to the center-distance cap");
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            check(!HudWidgetWristDock.IsNear(invalid, 0, 0, .1f, .1f), "Invalid local X cannot attach");
            check(!HudWidgetWristDock.IsNear(0, invalid, 0, .1f, .1f), "Invalid local Y cannot attach");
            check(!HudWidgetWristDock.IsNear(0, 0, invalid, .1f, .1f), "Invalid local Z cannot attach");
            check(!HudWidgetWristDock.IsNear(0, 0, 0, invalid, .1f), "Invalid width cannot attach");
            check(!HudWidgetWristDock.IsNear(0, 0, 0, .1f, invalid), "Invalid height cannot attach");
        }
        check(!HudWidgetWristDock.IsNear(0, 0, 0, 0, .1f) && !HudWidgetWristDock.IsNear(0, 0, 0, .1f, -.1f), "Invalid rectangle dimensions cannot attach");
        check(!HudWidgetWristDock.IsNear(float.MaxValue, 0, 0, float.MaxValue, .1f), "Overflowing distances fail closed");
    }
}
