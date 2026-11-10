using NOVR.MotionControls;

internal static class ThrottleSlideFeedbackChecks
{
    private static readonly int[] Rates = { 72, 90, 120, 144 };

    internal static void Run(Action<string, Action> check, Action<bool> truth)
    {
        check("Sliding the throttle ticks lightly every few percent, the same at any refresh rate or speed", () =>
        {
            foreach (var rate in Rates)
                foreach (var seconds in new[] { .6f, 1.5f, 4f })
                    foreach (var up in new[] { true, false })
                    {
                        var trace = new Trace(rate);
                        trace.Hold(up ? .2f : .6f, .2f);
                        trace.Slide(up ? .6f : .2f, seconds);
                        trace.Hold(up ? .6f : .2f, .3f);
                        // 40% of travel in 5% steps; float edges may add or drop one.
                        truth(trace.Ticks.Count >= 7 && trace.Ticks.Count <= 9);
                        truth(trace.Ticks.All(t => t.Strength == ThrottleSlideFeedback.TickStrength &&
                            t.Seconds == ThrottleSlideFeedback.TickSeconds && !t.Priority));
                        truth(trace.Ticks.All(t => t.Strength < ThrottleSlideFeedback.IdleStrength / 3));
                        truth(trace.Thumps.Count == 0);
                        truth(trace.LastPulseTime <= .2f + seconds + 1f / rate + .001f);
                    }
        });
        check("A held, trembling or slowly drifting lever stays quiet", () =>
        {
            foreach (var rate in Rates)
            {
                var trace = new Trace(rate);
                trace.Hold(.5f, .3f);
                for (var i = 0; i < rate * 3; i++)
                    trace.Step(.5f + (i % 2 == 0 ? .012f : -.012f));
                truth(trace.Ticks.Count == 0);
                trace.Slide(.53f, 2f);
                truth(trace.Ticks.Count == 0);
            }
        });
        check("A fast slide or a tracking jump gives single ticks, never a burst or a backlog", () =>
        {
            foreach (var rate in Rates)
            {
                var jump = new Trace(rate);
                jump.Hold(.1f, .2f);
                jump.Step(.9f);
                truth(jump.Ticks.Count == 1);
                jump.Hold(.9f, .5f);
                truth(jump.Ticks.Count == 1);
                var fast = new Trace(rate);
                fast.Hold(.1f, .2f);
                fast.Slide(.9f, .1f);
                fast.Hold(.9f, .3f);
                truth(fast.Ticks.Count >= 1 && fast.Ticks.Count <= 4);
                for (var i = 1; i < fast.Ticks.Count; i++)
                    truth(fast.Ticks[i].Time - fast.Ticks[i - 1].Time >= ThrottleSlideFeedback.MinInterval - .0001f);
            }
        });
        check("Reaching idle adds one stronger pulse shortly after the end stop, and only idle does", () =>
        {
            foreach (var rate in Rates)
            {
                var trace = new Trace(rate);
                trace.Hold(.3f, .2f);
                trace.Slide(0f, .4f);
                var arrival = trace.Time;
                truth(trace.Thumps.Count == 0);
                trace.Hold(0f, 1f);
                truth(trace.Thumps.Count == 1);
                var thump = trace.Thumps[0];
                truth(thump.Priority && thump.Strength == ThrottleSlideFeedback.IdleStrength && thump.Seconds == ThrottleSlideFeedback.IdleSeconds);
                truth(thump.Time - arrival >= ThrottleSlideFeedback.IdleDelay - 2f / rate && thump.Time - arrival <= ThrottleSlideFeedback.IdleDelay + 2f / rate);
                trace.Hold(0f, 2f);
                truth(trace.Thumps.Count == 1);
                var full = new Trace(rate);
                full.Hold(.7f, .2f);
                full.Slide(1f, .4f);
                full.Hold(1f, 1f);
                truth(full.Thumps.Count == 0);
            }
        });
        check("A hand resting on the idle stop cannot repeat the pulse; a real return to idle can", () =>
        {
            var trace = new Trace(90);
            trace.Hold(.3f, .2f);
            trace.Slide(0f, .3f);
            trace.Hold(0f, .5f);
            truth(trace.Thumps.Count == 1);
            for (var i = 0; i < 20; i++)
            {
                trace.Slide(.02f, .05f);
                trace.Slide(0f, .05f);
                trace.Hold(0f, .2f);
            }
            truth(trace.Thumps.Count == 1);
            trace.Slide(.2f, .3f);
            trace.Slide(0f, .3f);
            trace.Hold(0f, .5f);
            truth(trace.Thumps.Count == 2);
        });
        check("Leaving idle, letting go or regrabbing at idle cancels the pending pulse", () =>
        {
            var bounce = new Trace(90);
            bounce.Hold(.2f, .2f);
            bounce.Slide(0f, .2f);
            bounce.Step(.01f);
            bounce.Hold(.01f, .5f);
            truth(bounce.Thumps.Count == 0);
            var release = new Trace(90);
            release.Hold(.2f, .2f);
            release.Slide(0f, .2f);
            release.Release(.05f);
            release.Hold(0f, .5f);
            truth(release.Thumps.Count == 0 && release.Ticks.Count(t => t.Time > release.Time - .5f) == 0);
            var grab = new Trace(90);
            grab.Hold(0f, 1f);
            truth(grab.Thumps.Count == 0 && grab.Ticks.Count == 0);
        });
        check("Several samples in one frame, bad values and a clock rewind cannot add or repeat pulses", () =>
        {
            var feedback = new ThrottleSlideFeedback();
            feedback.Update(true, .2f, 1f);
            truth(feedback.Update(true, .3f, 1.1f).Strength > 0);
            truth(feedback.Update(true, .3f, 1.1f).Strength == 0 && feedback.Update(true, .3f, 1.1f).Strength == 0);
            foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                truth(feedback.Update(true, bad, 1.2f).Strength == 0);
                truth(feedback.Update(true, .9f, 1.3f).Strength == 0); // restarts from the next good sample
                truth(feedback.Update(true, .9f, bad).Strength == 0);
                truth(feedback.Update(true, .2f, 1.4f).Strength == 0);
            }
            feedback.Update(true, .5f, 5f);
            truth(feedback.Update(true, .9f, 2f).Strength == 0); // clock went backwards: a fresh start, not a tick
            truth(feedback.Update(true, 7f, 2.1f).Strength > 0 && feedback.Update(true, -3f, 2.2f).Strength > 0); // clamped, still one tick each
            truth(feedback.Update(false, .5f, 2.3f).Strength == 0 && feedback.Update(true, .9f, 2.4f).Strength == 0);
        });
    }

    private sealed class Trace
    {
        private readonly ThrottleSlideFeedback _feedback = new();
        private readonly float _dt;
        private float _value;
        internal float Time;
        internal float LastPulseTime;
        internal readonly List<(float Time, float Strength, float Seconds, bool Priority)> Ticks = new(), Thumps = new();
        internal Trace(int rate) => _dt = 1f / rate;
        internal void Step(float value, bool held = true)
        {
            Time += _dt;
            _value = value;
            var pulse = _feedback.Update(held, value, Time);
            if (pulse.Strength <= 0)
                return;
            LastPulseTime = Time;
            (pulse.Priority ? Thumps : Ticks).Add((Time, pulse.Strength, pulse.Seconds, pulse.Priority));
        }
        internal void Hold(float value, float seconds)
        {
            for (var t = 0f; t < seconds; t += _dt)
                Step(value);
        }
        internal void Slide(float to, float seconds)
        {
            var from = _value;
            var steps = Math.Max(1, (int)MathF.Round(seconds / _dt));
            for (var i = 1; i <= steps; i++)
                Step(from + (to - from) * i / steps);
        }
        internal void Release(float seconds)
        {
            for (var t = 0f; t < seconds; t += _dt)
                Step(_value, false);
        }
    }
}
