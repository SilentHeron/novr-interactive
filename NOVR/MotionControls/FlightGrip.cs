namespace NOVR.MotionControls;

// Flight handles can optionally latch after a quick tap. UI buttons keep the
// ordinary GripLatch so they never inherit flight-control latching behavior.
internal sealed class FlightGrip
{
    private readonly GripLatch _press = new();
    private bool _tapMode, _allowTap;
    private float _pressStarted, _lastTime;
    public bool Held { get; private set; }
    public bool Latched { get; private set; }

    public bool Update(bool valid, float grip, bool withinReach, bool tapMode, float time, bool withinRetention = true)
    {
        if (_tapMode != tapMode) Reset();
        _tapMode = tapMode;
        if (!valid || !FlightControlMath.IsFinite(grip) || !FlightControlMath.IsFinite(time) || time < _lastTime)
        { Reset(); return false; }
        _lastTime = time;
        // A latched hand may leave the handle, but not stay attached while the
        // pilot reaches far away. A fresh release/press is required afterward.
        if (Held && tapMode && !withinRetention) { Reset(); return false; }
        var previous = _press.Held;
        var pressed = _press.Update(true, grip, true);
        if (pressed && !previous)
        {
            if (Latched)
            {
                Held = Latched = _allowTap = false;
            }
            else
            {
                Held = withinReach;
                _allowTap = Held && tapMode;
                _pressStarted = time;
            }
        }
        if (!pressed && previous && Held)
        {
            Latched = _allowTap && time - _pressStarted <= .3f;
            Held = Latched;
        }
        if (!tapMode && !pressed) Held = Latched = false;
        return Held;
    }

    public void Reset()
    {
        _press.Reset();
        Held = Latched = _allowTap = false;
        _lastTime = 0;
    }
}
