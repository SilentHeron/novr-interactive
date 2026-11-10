using System;

namespace NOVR.MotionControls;

internal readonly struct ThrottleSlidePulse
{
    internal readonly float Strength, Seconds;
    // A slide tick gives way to any stronger cue already playing on the motor;
    // the idle pulse does not.
    internal readonly bool Priority;
    internal ThrottleSlidePulse(float strength, float seconds, bool priority)
    {
        Strength = strength;
        Seconds = seconds;
        Priority = priority;
    }
}

// The virtual throttle has no resistance, so the hand cannot feel it move.
// Sliding it gives a light tick for every few percent of travel, like a
// notched slider. Reaching idle already has the end-stop pulse; a second one
// follows it a moment later, so 0% feels different from every other position
// and from the single pulse at full power.
internal sealed class ThrottleSlideFeedback
{
    internal const float Step = .05f, TickStrength = .09f, TickSeconds = .015f, MinInterval = .03f;
    internal const float IdleStrength = .3f, IdleSeconds = .045f, IdleDelay = .09f, IdleRearm = .03f;
    private bool _active, _idleArmed;
    private float _anchor, _lastTick, _idleAt = -1f, _lastTime;

    internal ThrottleSlidePulse Update(bool held, float throttle, float now)
    {
        if (!held || !FlightControlMath.IsFinite(throttle) || !FlightControlMath.IsFinite(now))
        {
            Reset();
            return default;
        }
        throttle = FlightControlMath.Clamp(throttle, 0f, 1f);
        if (!_active || now < _lastTime)
        {
            // A fresh grab, or a clock that went backwards: start from here.
            // A lever grabbed at idle has not arrived there.
            Reset();
            _active = true;
            _anchor = throttle;
            _lastTime = now;
            _lastTick = now - MinInterval;
            _idleArmed = throttle > IdleRearm;
            return default;
        }
        _lastTime = now;
        if (throttle > 0f)
            _idleAt = -1f; // left idle again before the second pulse
        if (throttle > IdleRearm)
            _idleArmed = true;
        else if (throttle <= 0f && _idleArmed)
        {
            // Rearms only once the lever is clearly above idle, so a hand
            // resting on the stop cannot repeat it.
            _idleArmed = false;
            _idleAt = now + IdleDelay;
        }
        if (_idleAt >= 0f && now >= _idleAt)
        {
            _idleAt = -1f;
            return new ThrottleSlidePulse(IdleStrength, IdleSeconds, true);
        }
        var moved = throttle - _anchor;
        if (Math.Abs(moved) < Step)
            return default;
        // Whole steps only: the remainder stays, so a slow and a fast slide
        // tick at the same lever positions and a jump gives one tick, not a burst.
        _anchor += (float)Math.Truncate(moved / Step) * Step;
        if (now - _lastTick < MinInterval)
            return default;
        _lastTick = now;
        return new ThrottleSlidePulse(TickStrength, TickSeconds, false);
    }

    internal void Reset()
    {
        _active = _idleArmed = false;
        _anchor = _lastTick = _lastTime = 0f;
        _idleAt = -1f;
    }
}
