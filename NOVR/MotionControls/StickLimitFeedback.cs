using System;

namespace NOVR.MotionControls;

// Read the same normalized commands that drive the aircraft. Pitch, roll and
// physical stick twist share one cue; thumbstick yaw is deliberately excluded.
internal sealed class StickLimitFeedback
{
    private const float NearStart = .78f, NearExit = .70f, Limit = .999f, LimitRearm = .90f;
    private bool _active, _near, _limitLatched;
    private int _lastFrame = -1;
    private float _lastTime, _lastPulse, _nextPulse;

    internal (float strength, float seconds) Update(bool active, float pitch, float roll, float twist, float now, int frame)
    {
        if (!active || !FlightControlMath.IsFinite(pitch) || !FlightControlMath.IsFinite(roll) ||
            !FlightControlMath.IsFinite(twist) || !FlightControlMath.IsFinite(now))
        { Reset(); return default; }
        if (frame == _lastFrame) return default;
        // A diagonal is not a circular travel stop: either mapped axis must
        // approach its own limit. All sign combinations have identical feedback.
        var magnitude = Math.Min(1f, Math.Max(Math.Abs(pitch), Math.Max(Math.Abs(roll), Math.Abs(twist))));
        if (!_active || now < _lastTime || now - _lastTime > .25f)
        {
            _active = true; _lastFrame = frame; _lastTime = _lastPulse = now;
            _near = magnitude >= NearStart; _limitLatched = magnitude >= LimitRearm;
            _nextPulse = now + .18f;
            return default; // No new edge from a stale sample or grip-acquisition pulse.
        }
        _lastFrame = frame; _lastTime = now;
        if (magnitude < LimitRearm) _limitLatched = false;
        if (magnitude <= NearExit) { _near = false; return default; }
        if (magnitude >= NearStart) _near = true;
        if (!_near) return default;

        if (magnitude >= Limit && !_limitLatched && now - _lastPulse >= .08f)
        {
            _limitLatched = true; _lastPulse = now; _nextPulse = now + .35f;
            return (.42f, .06f);
        }
        // A held stop stays quiet, even if tracking jitters around full input.
        if (_limitLatched || now < _nextPulse) return default;
        var approach = FlightControlMath.Clamp((magnitude - NearStart) / (1f - NearStart), 0f, 1f);
        _lastPulse = now; _nextPulse = now + .42f - .18f * approach;
        return (.10f + .08f * approach, .018f + .008f * approach);
    }

    internal void Reset()
    {
        _active = _near = _limitLatched = false;
        _lastFrame = -1; _lastTime = _lastPulse = _nextPulse = 0;
    }
}
