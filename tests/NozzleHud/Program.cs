using NOVR.VrUi.SpecialBehavior;

var passed = 0;
void Check(bool result) { if (!result) throw new Exception("Nozzle visibility assertion failed at " + passed); passed++; }
var state = new NozzleHudContext();
Check(!state.Update(true, true, 0, 1, false, 0));
Check(!state.Update(true, true, 0, 1.2f, false, 10)); // Native forward-mode pitch input can exceed 1.
Check(!state.Update(true, false, 90, 0, true, 11)); // No fitted nozzle system.
Check(state.Update(true, true, 0, .5f, false, 12)); // Command appears before the mechanism moves.
Check(state.Update(true, true, 45, .5f, false, 20));
Check(state.Update(true, true, 90, 0, false, 100)); // Stable hover must not use native five-second fade.
Check(state.Update(true, true, 120, 0, false, 110)); // Ducted reverse range.
Check(state.Update(true, true, 0, 1, false, 111));
Check(state.Update(true, true, 0, 1, false, 112.49f));
Check(!state.Update(true, true, 0, 1, false, 112.51f)); // Settled forward hides after the hold.
Check(state.Update(true, true, 0, 1, true, 113)); // Auto-hover engaging, before visible rotation.
Check(!state.Update(false, true, 90, 0, true, 113.1f)); // Crash/menu overrides every hold.
Check(!state.Update(true, true, 0, 1, false, 113.2f)); // No stale hover state after returning.

state.Reset();
Check(!state.Update(true, true, .1f, .999f, false, 0));
for (var i = 1; i < 20; i++)
    Check(!state.Update(true, true, i % 2 == 0 ? 0 : .1f, .999f, false, i));
Check(state.Update(true, true, 1, 1, false, 20));
Check(state.Update(true, true, .5f, 1, false, 50)); // Hysteresis preserves meaningful near-forward tilt.
Check(state.Update(true, true, .2f, 1, false, 51));
Check(!state.Update(true, true, .2f, 1, false, 53));

foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    Check(state.Update(true, true, 90, 0, false, 60));
    Check(!state.Update(true, true, invalid, 0, false, 61));
    Check(!state.Update(true, true, 90, invalid, false, 61));
    Check(!state.Update(true, true, 90, 0, false, invalid));
    Check(!state.Update(true, true, 0, 1, false, 62));
}
Check(state.Update(true, true, 90, 0, false, 100));
Check(!state.Update(true, true, 0, 1, false, 0)); // Scene/time reset cannot inherit a hold.
Console.WriteLine($"{passed} source-linked nozzle context assertions passed (visibility policy; no Unity/game launch).");
