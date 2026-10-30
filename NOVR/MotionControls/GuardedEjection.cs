namespace NOVR.MotionControls;

// A separate release and pull is required after opening the cover. Losing the
// interaction context resets the guard; a cancelled pull never ejects on release.
internal sealed class GuardedEjection
{
    public bool Open { get; private set; }
    private bool _ready, _pulling;
    public void LiftCover() { Open = true; _ready = _pulling = false; }
    public void Release() { _pulling = false; _ready = Open; }
    public bool BeginPull() { _pulling = Open && _ready; _ready = false; return _pulling; }
    public bool Pull(float displacement, float heldSeconds, bool direct)
    {
        if (!_pulling || !Open || !FlightControlMath.IsFinite(displacement) || !FlightControlMath.IsFinite(heldSeconds)) return false;
        if (direct && displacement < 0.045f) return false;
        Reset();
        return true;
    }
    public void Reset() { Open = _ready = _pulling = false; }
}
