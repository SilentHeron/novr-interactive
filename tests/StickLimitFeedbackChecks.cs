using NOVR.MotionControls;

internal static class StickLimitFeedbackChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> truth)
    {
        check("Stick limit cue follows configured pitch and roll travel in either direction", () =>
        {
            foreach (var angle in new[] { 5f, 25f, 60f })
            foreach (var sign in new[] { -1f, 1f })
            foreach (var roll in new[] { false, true })
            {
                var radians = angle * MathF.PI / 180f;
                var command = FlightControlMath.Stick(roll ? sign * MathF.Sin(radians) : 0,
                    MathF.Cos(radians), roll ? 0 : sign * MathF.Sin(radians), angle, .06f, 1.5f);
                var feedback = new StickLimitFeedback(); feedback.Update(true, 0, 0, 0, 0, 0);
                var pulse = feedback.Update(true, command.pitch, command.roll, 0, .1f, 1);
                truth(pulse.strength == .42f && pulse.seconds == .06f);
            }
        });
        check("Diagonal stick movement does not invent a circular travel limit", () =>
        {
            var feedback = new StickLimitFeedback(); feedback.Update(true, 0, 0, 0, 0, 0);
            for (var frame = 1; frame < 200; frame++)
                truth(feedback.Update(true, .71f, -.71f, 0, frame / 90f, frame).strength == 0);
        });
        check("Approach cues increase gently and remain rate limited across headset refresh rates", () =>
        {
            foreach (var rate in new[] { 72, 90, 120, 144 })
            {
                var counts = new List<int>(); var strengths = new List<float>();
                foreach (var position in new[] { .8f, .95f })
                {
                    var feedback = new StickLimitFeedback(); feedback.Update(true, 0, 0, 0, 0, 0);
                    var count = 0; var last = -1f; var strength = 0f;
                    for (var frame = 1; frame <= rate * 5; frame++)
                    {
                        var now = frame / (float)rate;
                        var pulse = feedback.Update(true, position, 0, 0, now, frame);
                        if (pulse.strength <= 0) continue;
                        truth(now - last >= .24f && pulse.strength >= .10f && pulse.strength <= .18f);
                        truth(pulse.seconds >= .018f && pulse.seconds <= .026f);
                        truth(feedback.Update(true, position, 0, 0, now, frame).strength == 0);
                        last = now; strength = pulse.strength; count++;
                    }
                    counts.Add(count); strengths.Add(strength);
                }
                truth(counts[0] >= 11 && counts[0] <= 13 && counts[1] >= 16 && counts[1] <= 18);
                truth(strengths[1] > strengths[0] && counts[1] > counts[0]);
            }
        });
        check("Full travel pulses once and jitter cannot repeatedly retrigger the stop", () =>
        {
            var feedback = new StickLimitFeedback(); feedback.Update(true, 0, 0, 0, 0, 0);
            truth(feedback.Update(true, 1, 0, 0, .1f, 1).strength == .42f);
            for (var frame = 2; frame < 180; frame++)
                truth(feedback.Update(true, frame % 2 == 0 ? 1 : .95f, 0, 0, .1f + frame / 90f, frame).strength == 0);
            feedback.Update(true, .85f, 0, 0, 2.11f, 180);
            truth(feedback.Update(true, 1, 0, 0, 2.22f, 181).strength == .42f);
        });
        check("Physical yaw twist shares the same cue without stacking multiple saturated axes", () =>
        {
            var feedback = new StickLimitFeedback(); feedback.Update(true, 0, 0, 0, 0, 0);
            truth(feedback.Update(true, 0, 0, -1, .1f, 1).strength == .42f);
            truth(feedback.Update(true, 1, -1, -1, .2f, 2).strength == 0);
        });
        check("Unavailable input, tracking discontinuities and reset cannot synthesize a limit edge", () =>
        {
            var feedback = new StickLimitFeedback(); feedback.Update(true, 0, 0, 0, 0, 0);
            truth(feedback.Update(false, 1, 1, 1, .1f, 1).strength == 0);
            truth(feedback.Update(true, 1, 1, 1, .2f, 2).strength == 0);
            truth(feedback.Update(true, 1, 1, 1, .3f, 3).strength == 0);
            feedback.Reset(); feedback.Update(true, 0, 0, 0, 1, 4);
            truth(feedback.Update(true, 1, 1, 1, 2, 5).strength == 0);
            truth(feedback.Update(true, 1, 1, 1, 2.1f, 6).strength == 0);
            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                truth(feedback.Update(true, invalid, 0, 0, 3, 7).strength == 0);
            feedback.Reset(); feedback.Update(true, 0, 0, 0, 4, 8);
            truth(feedback.Update(true, 1, 0, 0, 4.1f, 9).strength == .42f);
        });
    }
}
