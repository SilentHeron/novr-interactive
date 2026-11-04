using NOVR.MotionControls;

internal static class StickLimitFeedbackChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> truth)
    {
        check("Continuous stick resistance follows configured pitch and roll in either direction", () =>
        {
            foreach (var angle in new[] { 5f, 25f, 60f })
            foreach (var sign in new[] { -1f, 1f })
            foreach (var roll in new[] { false, true })
            {
                var radians = angle * MathF.PI / 180f;
                var command = FlightControlMath.Stick(roll ? sign * MathF.Sin(radians) : 0,
                    MathF.Cos(radians), roll ? 0 : sign * MathF.Sin(radians), angle, .06f, 1.5f);
                var feedback = new StickLimitFeedback(); feedback.Update(true, 0, 0, 0, 0, 0);
                var strength = 0f;
                for (var frame = 1; frame <= 90; frame++)
                {
                    var cue = feedback.Update(true, command.pitch, command.roll, 0, frame / 90f, frame);
                    if (cue.strength > 0) strength = cue.strength;
                }
                truth(strength > .64f && strength <= .651f);
            }
        });
        check("Diagonal movement does not invent a circular travel limit", () =>
        {
            var feedback = new StickLimitFeedback(); feedback.Update(true, 0, 0, 0, 0, 0);
            for (var frame = 1; frame < 200; frame++)
                truth(feedback.Update(true, .71f, -.71f, 0, frame / 90f, frame).strength == 0);
        });
        check("Overlapping stick impulses stay continuous, bounded and progressively stronger", () =>
        {
            foreach (var rate in new[] { 72, 90, 120, 144 })
            {
                var strengths = new List<float>();
                foreach (var position in new[] { .8f, .90f, .95f, 1f })
                {
                    var feedback = new StickLimitFeedback(); feedback.Update(true, 0, 0, 0, 0, 0);
                    var count = 0; var last = -1f; var strength = 0f; var priorDuration = 0f;
                    for (var frame = 1; frame <= rate * 5; frame++)
                    {
                        var now = frame / (float)rate;
                        var cue = feedback.Update(true, position, 0, 0, now, frame);
                        if (cue.strength <= 0) continue;
                        truth(cue.strength <= .651f && cue.seconds == .065f);
                        if (last >= 0)
                        {
                            truth(now - last >= .025f - .000001f);
                            truth(now <= last + priorDuration);
                            truth(cue.strength >= strength - .000001f);
                        }
                        truth(feedback.Update(true, position, 0, 0, now, frame).strength == 0);
                        last = now; strength = cue.strength; priorDuration = cue.seconds; count++;
                    }
                    truth(count >= 145 && count <= 200);
                    strengths.Add(strength);
                }
                truth(strengths.Zip(strengths.Skip(1), (a, b) => b > a).All(x => x));
                truth(strengths[0] < .06f && strengths[3] > .64f);
            }
        });
        check("Full travel stays steady under tracking jitter instead of retriggering a stop pulse", () =>
        {
            var feedback = new StickLimitFeedback(); feedback.Update(true, 1, 0, 0, 0, 0);
            var last = 0f; var strength = 0f;
            for (var frame = 1; frame <= 270; frame++)
            {
                var now = frame / 90f;
                var cue = feedback.Update(true, frame % 2 == 0 ? 1 : .99f, 0, 0, now, frame);
                if (cue.strength == 0) continue;
                if (now > .5f) { truth(cue.strength > .64f); truth(Math.Abs(cue.strength - strength) < .01f); }
                if (last > 0) truth(now - last < .065f);
                last = now; strength = cue.strength;
            }
            truth(last > 2.95f);
        });
        check("Physical twist shares one resistance cue without stacking saturated axes", () =>
        {
            var twist = new StickLimitFeedback(); var all = new StickLimitFeedback();
            twist.Update(true, 0, 0, 0, 0, 0); all.Update(true, 0, 0, 0, 0, 0);
            for (var frame = 1; frame <= 180; frame++)
            {
                var a = twist.Update(true, 0, 0, -1, frame / 90f, frame);
                var b = all.Update(true, 1, -1, -1, frame / 90f, frame);
                truth(a == b);
            }
        });
        check("Grip loss, tracking loss, discontinuity and withdrawal reset continuous feedback", () =>
        {
            var feedback = new StickLimitFeedback(); feedback.Update(true, 0, 0, 0, 0, 0);
            truth(feedback.Update(true, 1, 0, 0, .1f, 1).strength > 0);
            truth(feedback.Update(false, 1, 1, 1, .11f, 2).strength == 0);
            truth(feedback.Update(true, 1, 1, 1, .12f, 3).strength == 0);
            truth(feedback.Update(true, 1, 1, 1, .13f, 4).strength == 0);
            truth(feedback.Update(true, 1, 1, 1, .20f, 5).strength > 0);
            truth(feedback.Update(true, .70f, 0, 0, .21f, 6).strength == 0);
            truth(feedback.Update(true, .75f, 0, 0, .25f, 7).strength == 0);
            truth(feedback.Update(true, 1, 0, 0, 2, 8).strength == 0);
            truth(feedback.Update(true, 1, 0, 0, 1, 9).strength == 0);
            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                truth(feedback.Update(true, invalid, 0, 0, 3, 10).strength == 0);
            feedback.Reset(); truth(feedback.Update(true, 1, 0, 0, 4, 11).strength == 0);
        });
    }
}
