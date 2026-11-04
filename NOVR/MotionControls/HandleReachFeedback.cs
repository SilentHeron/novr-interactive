namespace NOVR.MotionControls;

// One tactile cue on entering the actual grab radius. A wider exit boundary
// prevents tracking tremor from repeating it while the hand rests nearby.
internal sealed class HandleReachFeedback
{
    private bool _inside;
    private float _nextPulse, _lastTime;

    internal bool Update(bool valid, bool held, float distance, float radius, float now)
    {
        if (!valid || !FlightControlMath.IsFinite(distance) || distance < 0 ||
            !FlightControlMath.IsFinite(radius) || radius <= 0 || !FlightControlMath.IsFinite(now))
        { Reset(); return false; }
        if (now < _lastTime) Reset();
        _lastTime = now;
        if (held) { _inside = true; return false; }
        if (_inside)
        {
            if (distance > radius + .025f) _inside = false;
            return false;
        }
        if (distance > radius) return false;
        _inside = true;
        if (now < _nextPulse) return false;
        _nextPulse = now + .4f;
        return true;
    }

    internal void Reset() { _inside = false; _nextPulse = _lastTime = 0; }
}
