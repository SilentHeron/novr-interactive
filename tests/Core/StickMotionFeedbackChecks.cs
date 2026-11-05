using NOVR.MotionControls;

internal static class StickMotionFeedbackChecks
{
    private static readonly int[] Rates = { 72, 90, 120, 144 };

    internal static void Run(Action<string, Action> check, Action<bool> truth)
    {
        check("Small quick stick strokes give a faint, bounded cue in either direction and axis", () =>
        {
            foreach (var rate in Rates)
                foreach (var sign in new[] { -1f, 1f })
                    foreach (var roll in new[] { false, true })
                        foreach (var degrees in new[] { 3f, 5f })
                        {
                            var trace = new Trace(rate, truth);
                            trace.Hold(.3f);
                            trace.Move(.1f, roll ? 0 : sign * degrees, roll ? sign * degrees : 0);
                            trace.Hold(.5f);
                            truth(trace.Cues.Count > 0);
                            truth(trace.Cues.All(c => c.strength > 0 && c.strength <= .02501f));
                            truth(trace.Cues.Max(c => c.time + c.seconds) - trace.Cues[0].time <= .121f);
                            truth(trace.Cues.All(c => c.time < .6f));
                        }
        });
        check("Tracking tremor and slow deliberate drift do not buzz the stick", () =>
        {
            foreach (var rate in Rates)
            {
                var jitter = new Trace(rate, truth);
                jitter.Hold(.3f);
                for (var i = 0; i < rate * 3; i++)
                    jitter.Sample(.15f * MathF.Sin(i * 2.13f), .12f * MathF.Cos(i * 1.79f));
                truth(jitter.Cues.Count == 0);

                var drift = new Trace(rate, truth);
                drift.Hold(.3f);
                drift.Move(3f, 9f, -9f);
                drift.Hold(.5f);
                truth(drift.Cues.Count == 0);
            }
        });
        check("A held stick stays quiet and uninterrupted movement cannot sustain or repeat the cue", () =>
        {
            foreach (var rate in Rates)
            {
                var held = new Trace(rate, truth);
                held.Hold(2f);
                truth(held.Cues.Count == 0);
                held.Move(.1f, 4f, 0);
                held.Hold(.5f);
                var count = held.Cues.Count;
                truth(count > 0);
                held.Hold(4f);
                truth(held.Cues.Count == count);

                var moving = new Trace(rate, truth);
                moving.Hold(.3f);
                moving.Move(3f, 60f, 0);
                truth(moving.Cues.Count > 0);
                truth(moving.Cues.All(c => c.time < .65f));
            }
        });
        check("A pause rearms a fresh small gesture without stacking diagonal motion", () =>
        {
            foreach (var rate in Rates)
            {
                var trace = new Trace(rate, truth);
                trace.Hold(.3f);
                trace.Move(.1f, 4f, 0);
                trace.Hold(.5f);
                var count = trace.Cues.Count;
                truth(count > 0);
                trace.Move(.1f, 0, 0);
                trace.Hold(.5f);
                truth(trace.Cues.Count > count);

                var diagonal = new Trace(rate, truth);
                var singleAxis = new Trace(rate, truth);
                diagonal.Hold(.3f);
                singleAxis.Hold(.3f);
                diagonal.Move(.1f, 5f, -5f);
                singleAxis.Move(.1f, 5f, 0);
                diagonal.Hold(.5f);
                singleAxis.Hold(.5f);
                truth(diagonal.Cues.Count > 0);
                truth(diagonal.Cues.All(c => c.strength <= .02501f));
                truth(diagonal.Cues.Max(c => c.time + c.seconds) - diagonal.Cues[0].time <= .121f);
                truth(diagonal.Cues.SequenceEqual(singleAxis.Cues));
            }
        });
        check("The motion cue yields completely to the existing near-limit feedback", () =>
        {
            foreach (var rate in Rates)
                foreach (var magnitude in new[] { .70f, .78f, 1f })
                {
                    var trace = new Trace(rate, truth) { CommandMagnitude = magnitude };
                    trace.Hold(.3f);
                    trace.Move(.1f, 5f, -5f);
                    trace.Hold(.5f);
                    truth(trace.Cues.Count == 0);

                    trace.CommandMagnitude = .3f;
                    trace.Hold(.3f);
                    trace.Move(.1f, 0, 0);
                    trace.Hold(.5f);
                    truth(trace.Cues.Count > 0);
                }
        });
        check("Grip acquisition and recovery do not interpret a pose jump as stick weight", () =>
        {
            foreach (var rate in Rates)
            {
                var trace = new Trace(rate, truth);
                trace.Move(.08f, 5f, -5f);
                trace.Hold(.4f);
                truth(trace.Cues.Count == 0);
                trace.Move(.1f, 0, 0);
                trace.Hold(.4f);
                truth(trace.Cues.Count > 0);

                trace.Feedback.Reset();
                trace.Cues.Clear();
                trace.Sample(50f, -50f);
                trace.Hold(.5f);
                truth(trace.Cues.Count == 0);
                trace.Sample(80f, -80f, active: false);
                trace.Sample(90f, -90f);
                trace.Hold(.5f);
                truth(trace.Cues.Count == 0);
            }
        });
        check("Invalid tracking samples, time gaps and clock rewinds clear motion history safely", () =>
        {
            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                foreach (var channel in new[] { 0, 1, 2, 3 })
                {
                    var trace = new Trace(90, truth);
                    trace.Hold(.3f);
                    truth(trace.Feedback.Update(true, channel == 0 ? invalid : 0,
                        channel == 1 ? invalid : 0, channel == 2 ? invalid : .3f,
                        channel == 3 ? invalid : trace.Time, trace.Frame) == default);
                    trace.Sample(20f, -20f);
                    trace.Hold(.5f);
                    truth(trace.Cues.Count == 0);
                }

            foreach (var jump in new[] { 2f, -2f })
            {
                var trace = new Trace(90, truth) { Time = 10f };
                trace.Hold(.3f);
                trace.Time += jump;
                trace.Sample(30f, -30f);
                trace.Hold(.5f);
                truth(trace.Cues.Count == 0);
                trace.Move(.1f, 34f, -30f);
                trace.Hold(.4f);
                truth(trace.Cues.Count > 0);
            }
        });
        check("Repeated rapid corrections without a pause do not become sustained rumble", () =>
        {
            foreach (var rate in Rates)
            {
                var trace = new Trace(rate, truth);
                trace.Hold(.3f);
                for (var i = 0; i < 20; i++)
                    trace.Move(.15f, i % 2 == 0 ? 4f : -4f, 0);
                truth(trace.Cues.Count > 0);
                truth(trace.Cues.All(c => c.time < .65f));
            }
        });
    }

    private sealed class Trace(int rate, Action<bool> truth)
    {
        internal readonly StickMotionFeedback Feedback = new();
        internal readonly List<(float time, float strength, float seconds)> Cues = new();
        internal float Time, CommandMagnitude = .3f;
        internal int Frame;
        private float _pitch, _roll;

        internal void Sample(float pitch, float roll, bool active = true)
        {
            var cue = Feedback.Update(active, pitch, roll, CommandMagnitude, Time, Frame);
            truth(float.IsFinite(cue.strength) && float.IsFinite(cue.seconds));
            truth(Feedback.Update(active, pitch, roll, CommandMagnitude, Time, Frame) == default);
            if (cue.strength > 0)
            {
                truth(cue.seconds > 0 && cue.seconds <= .12f && cue.strength <= .02501f);
                Cues.Add((Time, cue.strength, cue.seconds));
            }
            else
                truth(cue == default);
            _pitch = pitch;
            _roll = roll;
            Time += 1f / rate;
            Frame++;
        }

        internal void Hold(float seconds)
        {
            for (var i = 0; i < (int)MathF.Ceiling(seconds * rate); i++)
                Sample(_pitch, _roll);
        }

        internal void Move(float seconds, float pitch, float roll)
        {
            var startPitch = _pitch;
            var startRoll = _roll;
            var frames = (int)MathF.Ceiling(seconds * rate);
            for (var i = 1; i <= frames; i++)
            {
                var fraction = i / (float)frames;
                Sample(startPitch + (pitch - startPitch) * fraction, startRoll + (roll - startRoll) * fraction);
            }
        }
    }
}
