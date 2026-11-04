using NOVR.MotionControls;

internal static class HandleReachFeedbackChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> assert)
    {
        check("Handle reach cue fires once at the real grab boundary and rejects tracking jitter", () =>
        {
            var gate = new HandleReachFeedback();
            assert(!gate.Update(true, false, .15f, .12f, 0));
            assert(gate.Update(true, false, .12f, .12f, .1f));
            for (var i = 0; i < 120; i++)
                assert(!gate.Update(true, false, i % 2 == 0 ? .119f : .125f, .12f, .1f + i / 90f));
            assert(!gate.Update(true, false, .15f, .12f, 2));
            assert(gate.Update(true, false, .1f, .12f, 2.1f));
        });
        check("Handle reach cue has cooldown and stays quiet while held or newly released", () =>
        {
            var gate = new HandleReachFeedback();
            assert(gate.Update(true, false, .1f, .12f, 0));
            assert(!gate.Update(true, false, .2f, .12f, .1f));
            assert(!gate.Update(true, false, .1f, .12f, .2f));
            assert(!gate.Update(true, true, .3f, .12f, 1));
            assert(!gate.Update(true, false, .1f, .12f, 2));
            assert(!gate.Update(true, false, .2f, .12f, 3));
            assert(gate.Update(true, false, .1f, .12f, 3.1f));
        });
        check("Handle reach cues reset on invalid tracking and remain independent per hand", () =>
        {
            var left = new HandleReachFeedback(); var right = new HandleReachFeedback();
            assert(left.Update(true, false, .1f, .12f, 1));
            assert(right.Update(true, false, .1f, .12f, 1));
            assert(!left.Update(false, false, .1f, .12f, 2));
            assert(!right.Update(true, false, .1f, .12f, 2));
            assert(!left.Update(true, false, float.NaN, .12f, 3));
            assert(!left.Update(true, false, .1f, 0, 3));
            assert(left.Update(true, false, .1f, .12f, 4));
            left.Reset(); assert(left.Update(true, false, .1f, .12f, 0));
        });
    }
}
