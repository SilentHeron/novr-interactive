using System;

namespace NOVR.MotionControls;

// Read the same normalized commands that drive the aircraft. Pitch, roll and
// physical stick twist share one cue; thumbstick yaw is deliberately excluded.
internal sealed class StickLimitFeedback
{
    private const float NearStart = .78f, NearExit = .70f;
    private const float RefreshInterval = .025f, ImpulseDuration = .065f, Response = 18f;
    private const float FullCueSeconds = 1f, FadeSeconds = 1.5f, HoldingGain = .2f;
    private bool _active, _near;
    private int _lastFrame = -1;
    private float _lastTime, _nextPulse, _strength, _nearSince;

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
            _active = true; _lastFrame = frame; _lastTime = now; _strength = 0;
            _near = magnitude >= NearStart; _nearSince = now; _nextPulse = now + .06f;
            return default; // Let the grip-acquisition cue finish before sustained feedback starts.
        }
        var elapsed = now - _lastTime;
        _lastFrame = frame; _lastTime = now;
        if (magnitude <= NearExit) { _near = false; _strength = 0; return default; }
        if (magnitude >= NearStart && !_near) { _near = true; _nearSince = now; }
        if (!_near) return default;

        var approach = FlightControlMath.Clamp((magnitude - NearStart) / (1f - NearStart), 0f, 1f);
        approach = approach * approach * (3f - 2f * approach);
        var fade = FlightControlMath.Clamp((magnitude - NearExit) / (NearStart - NearExit), 0f, 1f);
        // A clear arrival cue becomes a light holding sensation. The timer
        // survives tracking tremor and changes between saturated axes; only
        // withdrawing below NearExit (or releasing the grip) rearms it.
        var held = FlightControlMath.Clamp((now - _nearSince - FullCueSeconds) / FadeSeconds, 0f, 1f);
        held = held * held * (3f - 2f * held);
        var gain = 1f - (1f - HoldingGain) * held;
        var target = (.04f + .61f * approach) * fade * gain;
        _strength += (target - _strength) * FlightControlMath.SmoothingWeight(elapsed, Response);
        if (now < _nextPulse) return default;
        // Overlapping impulses remove the slow on/off rhythm. Common headset
        // rates send 30-40 updates/sec, never more than 40, while amplitude
        // changes smoothly through both approach and sustained-hold fatigue relief.
        _nextPulse = now + RefreshInterval;
        return (_strength, ImpulseDuration);
    }

    internal void Reset()
    {
        _active = _near = false;
        _lastFrame = -1; _lastTime = _nextPulse = _strength = _nearSince = 0;
    }
}
