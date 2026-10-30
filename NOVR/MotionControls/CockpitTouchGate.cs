namespace NOVR.MotionControls;

// Acquisition follows the animated switch cap, not the label/mounting plate.
// Inputs are real tracked fingertip offsets in cap axes; positive depth is toward
// the pilot. The plate guard prevents reaching through the cockpit to grab.
internal static class SwitchGrabGeometry
{
    internal static bool Contains(float x, float y, float depth, float plateDepth)
    {
        if (!FlightControlMath.IsFinite(x) || !FlightControlMath.IsFinite(y) ||
            !FlightControlMath.IsFinite(depth) || !FlightControlMath.IsFinite(plateDepth)) return false;
        var horizontal = x / .040f; var vertical = y / .060f;
        return horizontal * horizontal + vertical * vertical <= 1 &&
            depth >= -.025f && depth <= .100f && plateDepth >= -.008f;
    }
}

// Switches take a light squeeze, with a short allowance for pressing just
// before the fingertip arrives. This never applies to flight or eject handles.
internal sealed class SwitchGripGate
{
    private bool _armed, _consumed;
    private float _expires = float.NegativeInfinity;
    internal bool Held { get; private set; }
    internal void Update(bool available, float grip, float time)
    {
        if (!available || !FlightControlMath.IsFinite(grip) || !FlightControlMath.IsFinite(time))
        { Reset(); return; }
        if (grip <= .25f)
        { Held = false; _armed = true; _consumed = false; _expires = float.NegativeInfinity; }
        else if (grip >= .45f && !Held)
        {
            Held = _armed; _armed = false;
            _expires = Held ? time + .30f : float.NegativeInfinity;
        }
    }
    internal bool CanAcquire(float time) => Held && !_consumed && time <= _expires;
    internal void Consume() => _consumed = true;
    internal void Reset()
    { Held = _armed = _consumed = false; _expires = float.NegativeInfinity; }
}

// A hand must approach from in front. Starting inside a button, teleporting,
// recovering tracking or leaving a menu cannot produce a phantom touch.
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

// A visual/input guide for one push button. It never emits a press: the real
// tracked fingertip depth still goes through CockpitTouchGate unchanged.
internal sealed class ButtonContactGuide
{
    internal bool Captured { get; private set; }
    internal bool WaitingForWithdrawal { get; private set; }
    internal float X { get; private set; }
    internal float Y { get; private set; }
    internal float Depth { get; private set; }
    internal float VisualDepth => FlightControlMath.Clamp(Depth, -.003f, .035f);
    internal bool HoldsPress => Captured && Depth <= .012f;

    internal bool Update(bool available, float x, float y, float depth, float halfWidth, float halfHeight, bool tracked = true)
    {
        if (!tracked || !FlightControlMath.IsFinite(x) || !FlightControlMath.IsFinite(y) ||
            !FlightControlMath.IsFinite(depth) || !FlightControlMath.IsFinite(halfWidth) ||
            !FlightControlMath.IsFinite(halfHeight) || halfWidth <= 0 || halfHeight <= 0)
        { Cancel(); return false; }
        var dx = x - X; var dy = y - Y;
        var outside = dx * dx + dy * dy > .04f * .04f ||
            System.Math.Abs(x) > halfWidth + .02f || System.Math.Abs(y) > halfHeight + .02f;
        var withdrawn = outside || depth > .07f;
        // Losing an action (for example CLEAR after its last target) cancels
        // guidance, but a valid tracked hand can still withdraw from that key.
        // Invalid/default tracking coordinates never count as a withdrawal.
        if (!available)
        {
            Cancel();
            if (WaitingForWithdrawal && withdrawn) Reset();
            return false;
        }
        if (WaitingForWithdrawal)
        {
            if (withdrawn) Reset();
            return false; // Never transfer to another button in the release frame.
        }
        if (Captured)
        {
            if (withdrawn || depth < -.025f) { Cancel(); return false; }
            Depth = depth; return true;
        }
        if (System.Math.Abs(x) > halfWidth || System.Math.Abs(y) > halfHeight || depth > .035f || depth < -.025f)
            return false;
        // Center at most 20 mm. Large seat buttons can retain an off-center
        // contact axis rather than dragging the whole hand across their face.
        var radius = (float)System.Math.Sqrt(x * x + y * y);
        var factor = radius > .02f ? 1f - .02f / radius : 0;
        X = x * factor; Y = y * factor; Depth = depth; Captured = true;
        return true;
    }
    internal void Cancel()
    {
        if (Captured) WaitingForWithdrawal = true;
        Captured = false;
    }
    internal void Reset() { Captured = WaitingForWithdrawal = false; X = Y = Depth = 0; }
}

internal sealed class SeatHandlePull
{
    private bool _held;
    internal void Begin() => _held = true;
    internal bool Update(bool held, float upward, float sideways)
    {
        if (!held || !FlightControlMath.IsFinite(upward) || !FlightControlMath.IsFinite(sideways) || sideways > .12f)
        { Reset(); return false; }
        if (!_held || upward < .045f) return false;
        Reset(); return true;
    }
    internal void Reset() => _held = false;
}
