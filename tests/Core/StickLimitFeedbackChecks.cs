using NOVR.MotionControls;

internal static class StickLimitFeedbackChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> truth)
    {
        check("Short stick approach feedback follows configured pitch and roll in either direction", () =>
        {
            foreach (var angle in new[] { 5f, 25f, 60f })
            foreach (var sign in new[] { -1f, 1f })
            foreach (var roll in new[] { false, true })
            {
                var radians = angle * MathF.PI / 180f;
                var command = FlightControlMath.Stick(roll ? sign * MathF.Sin(radians) : 0,
                    MathF.Cos(radians), roll ? 0 : sign * MathF.Sin(radians), angle, .06f, 1.5f);
                var feedback = new StickLimitFeedback(); feedback.Update(true, 0, 0, 0, 0, 0);
                var peak = 0f;
                for (var frame = 1; frame <= 90; frame++)
                {
                    var cue = feedback.Update(true, command.pitch, command.roll, 0, frame / 90f, frame);
                    peak = Math.Max(peak, cue.strength);
                    if (frame >= 45) truth(cue == default);
                }
                truth(peak > .44f && peak <= .451f);
            }
        });
        check("Diagonal movement does not invent a circular travel limit", () =>
        {
            var feedback = new StickLimitFeedback(); feedback.Update(true, 0, 0, 0, 0, 0);
            for (var frame = 1; frame < 200; frame++)
                truth(feedback.Update(true, .71f, -.71f, 0, frame / 90f, frame) == default);
        });
        check("Overlapping approach impulses end within 450ms and stay silent at every held near-limit position", () =>
        {
            foreach (var rate in new[] { 72, 90, 120, 144 })
            {
                var peaks = new List<float>();
                foreach (var position in new[] { .8f, .90f, .95f, 1f })
                {
                    var feedback = new StickLimitFeedback(); feedback.Update(true, 0, 0, 0, 0, 0);
                    var count = 0; var last = -1f; var peak = 0f; var priorDuration = 0f;
                    var cutoff = 1f / rate + .45f;
                    for (var frame = 1; frame <= rate * 5; frame++)
                    {
                        var now = frame / (float)rate;
                        var cue = feedback.Update(true, position, 0, 0, now, frame);
                        truth(feedback.Update(true, position, 0, 0, now, frame) == default);
                        if (now >= cutoff) truth(cue == default);
                        if (cue.strength <= 0) continue;
                        truth(cue.strength <= .451f && cue.seconds > 0 && cue.seconds <= .045f);
                        truth(now + cue.seconds <= cutoff + .000001f);
                        if (last >= 0)
                        {
                            truth(now - last >= .025f - .000001f);
                            truth(now <= last + priorDuration + .000001f);
                            truth(cue.strength >= peak - .000001f);
                        }
                        last = now; peak = cue.strength; priorDuration = cue.seconds; count++;
                    }
                    truth(count >= 9 && count <= 18);
                    truth(Math.Abs(last + priorDuration - cutoff) < .000001f);
                    peaks.Add(peak);
                }
                truth(peaks.Zip(peaks.Skip(1), (a, b) => b > a).All(x => x));
                truth(peaks[0] < .04f && peaks[3] > .44f);
            }
        });
        check("An approaching stick produces a growing steady ramp without a separate arrival pulse", () =>
        {
            foreach (var rate in new[] { 72, 90, 120, 144 })
            {
                var feedback = new StickLimitFeedback(); feedback.Update(true, 0, 0, 0, 0, 0);
                var lastStrength = 0f; var lastEnd = 0f; var count = 0;
                for (var frame = 1; frame <= rate; frame++)
                {
                    var now = frame / (float)rate;
                    var position = .78f + .22f * Math.Min(now / .36f, 1f);
                    var cue = feedback.Update(true, position, 0, 0, now, frame);
                    if (now > .47f) truth(cue == default);
                    if (cue.strength == 0) continue;
                    truth(cue.strength > lastStrength);
                    if (count > 0) truth(now <= lastEnd + .000001f);
                    lastStrength = cue.strength; lastEnd = now + cue.seconds; count++;
                }
                truth(count >= 9 && lastStrength > .40f && lastStrength < .451f);
            }
        });
        check("Jitter, short retreats and changing saturated axes cannot rearm spent feedback", () =>
        {
            var feedback = new StickLimitFeedback(); feedback.Update(true, 1, 0, 0, 0, 0);
            for (var frame = 1; frame <= 90; frame++) feedback.Update(true, 1, 0, 0, frame / 90f, frame);
            for (var frame = 91; frame <= 900; frame++)
            {
                var magnitude = (frame % 7) switch { 0 => .69f, 1 => .71f, 2 => .75f, 3 => .79f, _ => .99f };
                var axis = frame % 3;
                truth(feedback.Update(true, axis == 0 ? magnitude : 0, axis == 1 ? -magnitude : 0,
                    axis == 2 ? magnitude : 0, frame / 90f, frame) == default);
            }
        });
        check("Only a deliberate retreat rearms a fresh bounded cue without stacking axes", () =>
        {
            foreach (var rate in new[] { 72, 90, 120, 144 })
            {
                var feedback = new StickLimitFeedback(); var frame = 0;
                float Advance(float magnitude, int frames)
                {
                    var peak = 0f;
                    for (var i = 0; i < frames; i++)
                    {
                        var now = frame / (float)rate;
                        var cue = feedback.Update(true, magnitude, -magnitude, magnitude, now, frame++);
                        peak = Math.Max(peak, cue.strength);
                    }
                    return peak;
                }
                truth(Advance(1, rate) > .44f);
                truth(Advance(.75f, rate / 2) == 0); // Hysteresis band is not a withdrawal.
                truth(Advance(1, rate / 2) == 0);
                truth(Advance(.69f, 2) == 0); // A brief low sample must not recharge the cue.
                truth(Advance(1, rate / 2) == 0);
                truth(Advance(.69f, (int)Math.Ceiling(rate * .12f)) == 0);
                truth(Advance(1, rate / 2) > .44f);
                truth(Advance(1, rate * 3) == 0);
            }
        });
        check("Physical twist shares the same bounded cue without adding saturated axes", () =>
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
        check("Timing gaps cannot recharge a held stick; release and tracking loss reset safely", () =>
        {
            var feedback = new StickLimitFeedback(); feedback.Update(true, 0, 0, 0, 0, 0);
            truth(feedback.Update(true, 1, 0, 0, .1f, 1).strength > 0);
            truth(feedback.Update(true, 1, 0, 0, 1f, 2) == default);
            truth(feedback.Update(true, 1, 0, 0, 1.1f, 3) == default);
            truth(feedback.Update(true, 1, 0, 0, .2f, 4) == default); // Clock rewind must not rearm.
            truth(feedback.Update(true, 1, 0, 0, .3f, 5) == default);
            truth(feedback.Update(false, 1, 1, 1, .31f, 6) == default);
            truth(feedback.Update(true, 1, 1, 1, .32f, 7) == default);
            truth(feedback.Update(true, 1, 1, 1, .33f, 8) == default);
            truth(feedback.Update(true, 1, 1, 1, .40f, 9).strength > 0);
            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            foreach (var channel in new[] { 0, 1, 2, 3 })
            {
                truth(feedback.Update(true, channel == 0 ? invalid : 1, channel == 1 ? invalid : 1,
                    channel == 2 ? invalid : 1, channel == 3 ? invalid : 3, 10) == default);
                truth(feedback.Update(true, 1, 1, 1, 4, 11) == default);
                truth(feedback.Update(true, 1, 1, 1, 4.1f, 12).strength > 0);
            }
            feedback.Reset(); truth(feedback.Update(true, 1, 0, 0, 5, 13) == default);
        });
    }
}
