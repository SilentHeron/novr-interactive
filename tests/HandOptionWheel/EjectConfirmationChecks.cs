using NOVR.MotionControls;

internal static class EjectConfirmationChecks
{
    internal static void Run(Action<bool, string> check)
    {
        const float dt = 1f / 90f;
        EjectConfirmationGesture Begin()
        {
            var gesture = new EjectConfirmationGesture(); gesture.Begin();
            check(gesture.Active && !gesture.Holding && gesture.Progress == 0, "eject begins idle, requiring release");
            return gesture;
        }
        EjectConfirmationGesture Hold(float x = 0, float y = 0)
        {
            var gesture = Begin();
            check(gesture.Update(true, false, 0, x, y, dt) == EjectConfirmationEvent.None, "release arms eject click");
            check(gesture.Update(true, true, 0, x, y, dt) == EjectConfirmationEvent.None &&
                gesture.Holding && gesture.Progress == 0, "fresh centered click begins without counting preceding frame");
            return gesture;
        }
        void RemainsDisarmed(EjectConfirmationGesture gesture)
        {
            for (var i = 0; i < 150; i++)
                check(gesture.Update(true, true, 0, 0, 0, dt) == EjectConfirmationEvent.None &&
                    gesture.Active && !gesture.Holding && gesture.Progress == 0, "held/recentered click never rearms eject");
        }

        foreach (var rate in new[] { 30, 60, 72, 90, 120, 144 })
        {
            var gesture = Hold(); var step = 1f / rate; double elapsed = 0;
            while (elapsed + step < 1.2d)
            {
                elapsed += step;
                check(gesture.Update(true, true, 0, 0, 0, step) == EjectConfirmationEvent.None && gesture.Holding,
                    "eject cannot confirm before 1.2 seconds at any headset rate");
                check(Math.Abs(gesture.Progress - elapsed / 1.2d) < .000001d,
                    "eject progress reflects uninterrupted elapsed hold time");
            }
            check(gesture.Update(true, true, 0, 0, 0, step) == EjectConfirmationEvent.Confirm &&
                !gesture.Active && !gesture.Holding && gesture.Progress == 1, "complete hold emits confirm once");
            for (var i = 0; i < 8; i++)
                check(gesture.Update(true, i % 2 == 0, 0, 0, 0, step) == EjectConfirmationEvent.None,
                    "confirmed gesture never repeats after held click or release");
        }

        var g = Begin(); RemainsDisarmed(g);
        g.Update(true, false, 0, 0, 0, dt);
        check(g.Update(true, true, 0, 0, 0, dt) == EjectConfirmationEvent.None && g.Holding,
            "a held click on confirmation entry needs release plus new press");

        foreach (var offCentre in new[] { .221f, .30f, .45f })
        {
            g = Begin(); g.Update(true, false, 0, offCentre, 0, dt);
            check(g.Update(true, true, 0, offCentre, 0, dt) == EjectConfirmationEvent.None && !g.Holding,
                "off-centre press cannot start eject");
            RemainsDisarmed(g);
            g.Update(true, false, 0, 0, 0, dt); g.Update(true, true, 0, 0, 0, dt);
            check(g.Holding, "off-centre rejection permits a fresh centered retry");
        }

        g = Hold(.22f);
        check(g.Update(true, true, 0, .30f, 0, .15f) == EjectConfirmationEvent.None && g.Holding && g.Progress > 0,
            "inclusive start and relaxed hold boundaries tolerate small drift");
        check(g.Update(true, true, 0, .301f, 0, dt) == EjectConfirmationEvent.None && g.Active &&
            !g.Holding && g.Progress == 0, "leaving relaxed center interrupts rather than completes hold");
        RemainsDisarmed(g);
        g = Hold();
        check(g.Update(true, true, 0, .22f, .22f, dt) == EjectConfirmationEvent.None && !g.Holding,
            "center tolerance uses radial distance, not independent square axes");

        g = Hold();
        for (var i = 0; i < 100; i++) g.Update(true, true, 0, 0, 0, dt);
        check(g.Progress > .9f, "early release case has substantial real progress");
        check(g.Update(true, false, 0, 0, 0, .15f) == EjectConfirmationEvent.None && g.Active &&
            !g.Holding && g.Progress == 0, "early release resets progress but keeps confirmation available");
        g.Update(true, true, 0, 0, 0, dt);
        for (var i = 0; i < 100; i++)
            check(g.Update(true, true, 0, 0, 0, dt) == EjectConfirmationEvent.None,
                "retry cannot inherit previous partial hold");

        foreach (var gap in new[] { .15001f, .5f, 5f })
        {
            g = Hold();
            for (var i = 0; i < 100; i++) g.Update(true, true, 0, 0, 0, dt);
            check(g.Update(true, true, 0, 0, 0, gap) == EjectConfirmationEvent.None && g.Active &&
                !g.Holding && g.Progress == 0, "stalled frame resets hold without ejecting or dismissing retry");
            RemainsDisarmed(g);
            g.Update(true, false, 0, 0, 0, dt); g.Update(true, true, 0, 0, 0, dt);
            check(g.Holding, "frame-gap recovery requires released click then new press");
        }
        g = Hold(); g.Update(true, true, 0, 0, 0, .1f); var progress = g.Progress;
        for (var i = 0; i < 20; i++)
            check(g.Update(true, true, 0, 0, 0, 0) == EjectConfirmationEvent.None && g.Progress == progress,
                "zero time cannot advance eject confirmation");

        void Cancels(bool available = true, float trigger = 0, float x = 0, float y = 0, float step = dt)
        {
            var gesture = Hold();
            for (var i = 0; i < 107; i++) gesture.Update(true, true, 0, 0, 0, dt);
            check(gesture.Update(available, true, trigger, x, y, step) == EjectConfirmationEvent.Cancel &&
                !gesture.Active && !gesture.Holding && gesture.Progress == 0, "unsafe input/context cancels and clears eject hold");
            check(gesture.Update(true, false, 0, 0, 0, dt) == EjectConfirmationEvent.None &&
                gesture.Update(true, true, 0, 0, 0, dt) == EjectConfirmationEvent.None && !gesture.Active,
                "cancel emits once and cannot silently resume after recovery");
        }
        Cancels(available:false); // Caller uses false for tracking, pause, menu and aircraft loss.
        foreach (var radius in new[] { .451f, 1f, -1f, float.MaxValue }) Cancels(x:radius);
        Cancels(x:.32f, y:.32f);
        foreach (var trigger in new[] { .75f, 1f, -1f }) Cancels(trigger:trigger);
        Cancels(step:-.001f);
        foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        { Cancels(trigger:bad); Cancels(x:bad); Cancels(y:bad); Cancels(step:bad); }

        g = Begin();
        check(g.Update(true, false, 1, 0, 0, dt) == EjectConfirmationEvent.Cancel,
            "trigger already down on entry also cancels safely");
        g = Hold(); g.Update(true, true, 0, 0, 0, .1f); g.Begin(); RemainsDisarmed(g);
        g = Hold(); g.Update(true, true, 0, 0, 0, .1f); g.Reset();
        check(!g.Active && !g.Holding && g.Progress == 0 &&
            g.Update(true, true, 0, 0, 0, dt) == EjectConfirmationEvent.None,
            "external reset removes pending confirmation and does not reopen");
    }
}
