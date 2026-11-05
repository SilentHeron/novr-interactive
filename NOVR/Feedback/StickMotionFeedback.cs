using System;

namespace NOVR.MotionControls;

// A faint, bounded resistance cue for quick pitch/roll corrections. Measure
// physical angles before the response curve so sensitivity cannot amplify it.
internal sealed class StickMotionFeedback
{
    private const float SpeedStart = 12f, SpeedRest = 6f, Excursion = .8f;
    private const float AcquisitionDelay = .12f, RestSeconds = .12f, CueSeconds = .12f;
    private const float RefreshInterval = .02f, ImpulseSeconds = .025f;
    private bool _active, _armed;
    private int _lastFrame = -1;
    private float _lastTime, _lastPitch, _lastRoll, _anchorPitch, _anchorRoll;
    private float _pitchSpeed, _rollSpeed, _readyAt, _quietSince = -1f;
    private float _cueStart = -1f, _nextPulse;

    internal (float strength, float seconds) Update(bool active, float pitchDegrees, float rollDegrees,
        float commandMagnitude, float now, int frame)
    {
        // The existing travel-limit feedback owns the outer range, including
        // its quiet held-at-limit state. Never restart it with movement cues.
        if (!active || !FlightControlMath.IsFinite(pitchDegrees) || !FlightControlMath.IsFinite(rollDegrees) ||
            !FlightControlMath.IsFinite(commandMagnitude) || !FlightControlMath.IsFinite(now) || commandMagnitude >= .70f)
        {
            Reset();
            return default;
        }
        if (frame == _lastFrame)
            return default;
        var elapsed = now - _lastTime;
        if (!_active || elapsed <= 0f || elapsed > .12f)
        {
            Begin(pitchDegrees, rollDegrees, now, frame);
            return default;
        }
        _lastFrame = frame;
        _lastTime = now;
        var pitchSpeed = (pitchDegrees - _lastPitch) / elapsed;
        var rollSpeed = (rollDegrees - _lastRoll) / elapsed;
        _lastPitch = pitchDegrees;
        _lastRoll = rollDegrees;
        // A tracking jump or angle wrap is not intentional stick resistance.
        if (Math.Abs(pitchSpeed) > 720f || Math.Abs(rollSpeed) > 720f)
        {
            Begin(pitchDegrees, rollDegrees, now, frame);
            return default;
        }
        var weight = FlightControlMath.SmoothingWeight(elapsed, 25f);
        _pitchSpeed += (pitchSpeed - _pitchSpeed) * weight;
        _rollSpeed += (rollSpeed - _rollSpeed) * weight;
        // Max-axis speed avoids making diagonal corrections stronger.
        var speed = Math.Max(Math.Abs(_pitchSpeed), Math.Abs(_rollSpeed));
        if (now < _readyAt)
        {
            _anchorPitch = pitchDegrees;
            _anchorRoll = rollDegrees;
            return default;
        }
        if (speed < SpeedRest)
        {
            _cueStart = -1f;
            _anchorPitch = pitchDegrees;
            _anchorRoll = rollDegrees;
            if (_quietSince < 0f)
                _quietSince = now;
            if (now - _quietSince >= RestSeconds)
                _armed = true;
            return default;
        }
        _quietSince = -1f;
        var excursion = Math.Max(Math.Abs(pitchDegrees - _anchorPitch), Math.Abs(rollDegrees - _anchorRoll));
        if (_armed && speed >= SpeedStart && excursion >= Excursion)
        {
            _armed = false;
            _cueStart = now;
            _nextPulse = now;
        }
        if (_cueStart < 0f)
            return default;
        var age = now - _cueStart;
        var remaining = CueSeconds - age;
        if (remaining <= 0f || now < _nextPulse)
            return default;
        var envelope = Math.Min(FlightControlMath.Clamp(age / .02f, 0f, 1f),
            FlightControlMath.Clamp(remaining / .04f, 0f, 1f));
        var movement = FlightControlMath.Clamp((speed - SpeedRest) / (65f - SpeedRest), 0f, 1f);
        var strength = (.008f + .014f * movement) * envelope;
        if (strength <= 0f)
            return default;
        _nextPulse = now + RefreshInterval;
        return (strength, Math.Min(ImpulseSeconds, remaining));
    }

    private void Begin(float pitch, float roll, float now, int frame)
    {
        Reset();
        _active = _armed = true;
        _lastFrame = frame;
        _lastTime = now;
        _lastPitch = _anchorPitch = pitch;
        _lastRoll = _anchorRoll = roll;
        _readyAt = now + AcquisitionDelay;
    }

    internal void Reset()
    {
        _active = _armed = false;
        _lastFrame = -1;
        _lastTime = _lastPitch = _lastRoll = _anchorPitch = _anchorRoll = 0f;
        _pitchSpeed = _rollSpeed = _readyAt = _nextPulse = 0f;
        _cueStart = _quietSince = -1f;
    }
}
