using System;

namespace NOVR.MotionControls;

// Read the same normalized commands that drive the aircraft. Pitch, roll and
// physical stick twist share one cue; thumbstick yaw is deliberately excluded.
internal sealed class StickLimitFeedback
{
    private const float NearStart = .78f, NearExit = .70f;
    private const float RefreshInterval = .025f, ImpulseDuration = .045f, Response = 18f;
    private const float CueBudget = .45f, RetreatSeconds = .10f;
    private bool _active, _near, _retreating;
    private int _lastFrame = -1;
    private float _lastTime, _nextPulse, _strength, _cutoffAt, _retreatSince;

    internal (float strength, float seconds) Update(bool active, float pitch, float roll, float twist, float now, int frame)
    {
        if (!active || !FlightControlMath.IsFinite(pitch) || !FlightControlMath.IsFinite(roll) ||
            !FlightControlMath.IsFinite(twist) || !FlightControlMath.IsFinite(now))
        {
            Reset();
            return default;
        }
        if (frame == _lastFrame)
            return default;
        // A diagonal is not a circular travel stop: either mapped axis must
        // approach its own limit. All sign combinations have identical feedback.
        var magnitude = Math.Min(1f, Math.Max(Math.Abs(pitch), Math.Max(Math.Abs(roll), Math.Abs(twist))));
        if (!_active)
        {
            _active = true;
            _lastFrame = frame;
            _lastTime = now;
            _strength = 0;
            _near = magnitude >= NearStart;
            _cutoffAt = now + CueBudget;
            _nextPulse = now + .06f;
            return default; // Let the grip-acquisition cue finish before movement feedback starts.
        }
        var elapsed = now - _lastTime;
        _lastFrame = frame;
        _lastTime = now;
        if (elapsed < 0f)
        {
            // A clock discontinuity must not buy another burst while held at
            // the stop. Forward gaps consume the existing budget naturally.
            _cutoffAt = now;
            _strength = 0;
            _retreating = false;
            _nextPulse = now + .06f;
            return default;
        }
        if (magnitude <= NearExit)
        {
            _strength = 0;
            if (!_retreating)
            {
                _retreating = true;
                _retreatSince = now;
            }
            if (now - _retreatSince >= RetreatSeconds)
                _near = false;
            return default;
        }
        _retreating = false;
        if (magnitude >= NearStart && !_near)
        {
            _near = true;
            _cutoffAt = now + CueBudget;
        }
        if (!_near)
            return default;
        var remaining = _cutoffAt - now;
        if (remaining <= 0f)
        {
            _strength = 0;
            return default;
        }

        var approach = FlightControlMath.Clamp((magnitude - NearStart) / (1f - NearStart), 0f, 1f);
        approach = approach * approach * (3f - 2f * approach);
        var fade = FlightControlMath.Clamp((magnitude - NearExit) / (NearStart - NearExit), 0f, 1f);
        // Position controls the short approach ramp. Its budget is shared by
        // every axis and never restarts from tremor or a sustained tight turn.
        var target = (.025f + .425f * approach) * fade;
        _strength += (target - _strength) * FlightControlMath.SmoothingWeight(elapsed, Response);
        if (now < _nextPulse)
            return default;
        // Overlap short impulses for a steady ramp, then cut out completely.
        // Even the final impulse must end within this approach's time budget.
        _nextPulse = now + RefreshInterval;
        return (_strength, Math.Min(ImpulseDuration, remaining));
    }

    internal void Reset()
    {
        _active = _near = _retreating = false;
        _lastFrame = -1;
        _lastTime = _nextPulse = _strength = _cutoffAt = _retreatSince = 0;
    }
}
