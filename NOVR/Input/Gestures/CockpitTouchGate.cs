namespace NOVR.MotionControls;

// Debounced direct touches for widget editor controls.
internal sealed class CockpitTouchGate
{
    private bool _initialized, _armed;
    internal bool Update(bool available, bool withinFace, float depth)
    {
        if (!available || !FlightControlMath.IsFinite(depth)) { Reset(); return false; }
        if (!_initialized) { _initialized = true; return false; }
        if (depth > .015f) _armed = true;
        if (!withinFace || depth > .004f || depth < -.025f) return false;
        var pressed = _armed; _armed = false; return pressed;
    }
    internal void Reset() { _initialized = _armed = false; }
}
