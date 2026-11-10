using System;
using NOVR.VrCamera;

namespace NOVR.MotionControls;

internal enum ExternalStickMode
{
    Look, ThrottleRudder, Distance
}

internal readonly struct ExternalFlightMotion
{
    // Flying is false until the stick hand has been centred once, so entering
    // the outside view with a deflected thumbstick cannot move the aircraft.
    internal readonly bool Flying, Rudder;
    internal readonly float Pitch, Roll, Yaw, ThrottleStep;
    internal readonly SpectatorStickMotion Camera;
    internal ExternalFlightMotion(bool flying, float pitch, float roll, bool rudder, float yaw, float throttleStep, SpectatorStickMotion camera)
    {
        Flying = flying;
        Pitch = pitch;
        Roll = roll;
        Rudder = rudder;
        Yaw = yaw;
        ThrottleStep = throttleStep;
        Camera = camera;
    }
}

// Flying from the outside view with thumbsticks, as on a gamepad: the stick
// hand's thumbstick is the flight stick and the other thumbstick looks around
// the aircraft. Squeezing the throttle hand turns its thumbstick into throttle
// (up/down) and rudder (left/right); squeezing the stick hand turns it into
// camera distance. No Unity input here: the caller owns tracking and menus.
internal sealed class ExternalFlightSticks
{
    internal const float ThrottlePerSecond = .6f, ResponseExponent = 1.5f;
    private bool _stickArmed, _otherArmed;
    private ExternalStickMode _mode;

    internal void Reset()
    {
        _stickArmed = _otherArmed = false;
        _mode = ExternalStickMode.Look;
    }

    internal ExternalFlightMotion Update(bool available, bool stickValid, bool otherValid,
        float stickX, float stickY, float otherX, float otherY, bool throttleGrip, bool distanceGrip, float deltaSeconds)
    {
        if (!available || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0)
        {
            Reset();
            return default;
        }
        // Flying matters more than framing, so the throttle grip wins.
        var mode = throttleGrip ? ExternalStickMode.ThrottleRudder : distanceGrip ? ExternalStickMode.Distance : ExternalStickMode.Look;
        if (mode != _mode)
        {
            // A deflection meant for the camera must not become a throttle slam
            // (or the reverse) when a grip changes what the thumbstick does.
            _mode = mode;
            _otherArmed = false;
        }
        SpectatorStickInput.Filter(stickValid, stickX, stickY, ref _stickArmed, out stickX, out stickY);
        SpectatorStickInput.Filter(otherValid, otherX, otherY, ref _otherArmed, out otherX, out otherY);
        var dt = Math.Min(deltaSeconds, .05f);
        var flying = stickValid && _stickArmed;
        // Nuclear Option uses negative pitch for nose-up: pulling the thumbstick
        // back raises the nose, as the virtual stick does.
        var pitch = flying ? Curve(stickY) : 0;
        var roll = flying ? Curve(stickX) : 0;
        if (mode == ExternalStickMode.ThrottleRudder)
            return new ExternalFlightMotion(flying, pitch, roll, otherValid && _otherArmed, Curve(otherX),
                otherY * ThrottlePerSecond * dt, default);
        var camera = mode == ExternalStickMode.Distance
            ? new SpectatorStickMotion(otherX * 75f * dt, 0, -otherY * 1.5f * dt, 0, 0)
            : new SpectatorStickMotion(otherX * 75f * dt, -otherY * 60f * dt, 0, 0, 0);
        return new ExternalFlightMotion(flying, pitch, roll, false, 0, 0, camera);
    }

    private static float Curve(float value) => Math.Sign(value) * (float)Math.Pow(Math.Abs(value), ResponseExponent);
}
