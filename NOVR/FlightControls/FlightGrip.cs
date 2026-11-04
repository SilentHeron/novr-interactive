namespace NOVR.MotionControls;

// Hold uses continuous pressure; toggle changes only on a fresh squeeze.
// Tracking/menu loss disarms either mode until the controller is released.
internal sealed class FlightGrip
{
    private readonly GripLatch _press = new();
    private bool _toggleMode;
    private float _lastTime;
    public bool Held
    {
        get; private set;
    }
    public bool Latched
    {
        get; private set;
    }

    public bool Update(bool valid, float grip, bool withinReach, bool toggleMode, float time, bool withinRetention = true)
    {
        if (_toggleMode != toggleMode)
            Reset();
        _toggleMode = toggleMode;
        if (!valid || !FlightControlMath.IsFinite(grip) || !FlightControlMath.IsFinite(time) || time < _lastTime ||
            Held && toggleMode && !withinRetention)
        {
            Reset();
            return false;
        }
        _lastTime = time;
        var wasPressed = _press.Held;
        var pressed = _press.Update(true, grip, true);
        if (pressed && !wasPressed)
            Held = toggleMode && Held ? false : withinReach;
        if (!toggleMode && !pressed)
            Held = false;
        Latched = toggleMode && Held;
        return Held;
    }
    public void Reset()
    {
        _press.Reset();
        Held = Latched = false;
        _lastTime = 0;
    }
}
