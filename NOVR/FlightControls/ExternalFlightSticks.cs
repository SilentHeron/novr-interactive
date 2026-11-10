using System;
using NOVR.VrCamera;

namespace NOVR.MotionControls;

internal readonly struct ExternalFlightMotion
{
    // Flying is false until the flight thumbstick has been centred once, so
    // entering the outside view with it deflected cannot move the aircraft.
    internal readonly bool Flying;
    internal readonly float Pitch, Roll, Yaw;
    internal readonly SpectatorStickMotion Camera;
    internal bool Rudder => Yaw != 0;
    internal ExternalFlightMotion(bool flying, float pitch, float roll, float yaw, SpectatorStickMotion camera)
    {
        Flying = flying;
        Pitch = pitch;
        Roll = roll;
        Yaw = yaw;
        Camera = camera;
    }
}

// Flying from the outside view, laid out like the game's own gamepad defaults:
// left thumbstick pitch and roll, right thumbstick camera, left and right
// trigger rudder. It is for looking at the aircraft while keeping it under
// control, not a second cockpit: the throttle stays where the cockpit left it
// and there is no brake. Holding a grip turns the camera thumbstick's up and
// down into camera distance. Sides are physical, as on a gamepad; cockpit
// handedness does not mirror them.
// No Unity input here: the caller owns tracking and menus.
internal sealed class ExternalFlightSticks
{
    internal const float ResponseExponent = 1.5f;
    private const float TriggerRest = .15f, TriggerStart = .08f, GripRest = .35f, GripStart = .5f;
    private bool _flightArmed, _cameraArmed, _leftTriggerArmed, _rightTriggerArmed, _leftGripArmed, _rightGripArmed, _distance;

    internal void Reset() =>
        _flightArmed = _cameraArmed = _leftTriggerArmed = _rightTriggerArmed = _leftGripArmed = _rightGripArmed = _distance = false;

    internal ExternalFlightMotion Update(bool available, bool leftValid, bool rightValid,
        float leftX, float leftY, float rightX, float rightY,
        float leftTrigger, float rightTrigger, float leftGrip, float rightGrip, float deltaSeconds)
    {
        if (!available || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0)
        {
            Reset();
            return default;
        }
        var rudderLeft = Press(leftValid, leftTrigger, ref _leftTriggerArmed, TriggerRest, TriggerStart);
        var rudderRight = Press(rightValid, rightTrigger, ref _rightTriggerArmed, TriggerRest, TriggerStart);
        var leftHeld = Press(leftValid, leftGrip, ref _leftGripArmed, GripRest, GripStart) > 0;
        var rightHeld = Press(rightValid, rightGrip, ref _rightGripArmed, GripRest, GripStart) > 0;
        var distance = leftHeld || rightHeld;
        if (distance != _distance)
        {
            // A deflection meant for looking around must not become a zoom
            // (or the reverse) when a grip changes what the thumbstick does.
            _distance = distance;
            _cameraArmed = false;
        }
        SpectatorStickInput.Filter(leftValid, leftX, leftY, ref _flightArmed, out leftX, out leftY);
        SpectatorStickInput.Filter(rightValid, rightX, rightY, ref _cameraArmed, out rightX, out rightY);
        var dt = Math.Min(deltaSeconds, .05f);
        var flying = leftValid && _flightArmed;
        var camera = distance
            ? new SpectatorStickMotion(rightX * 75f * dt, 0, -rightY * 1.5f * dt, 0, 0)
            : new SpectatorStickMotion(rightX * 75f * dt, -rightY * 60f * dt, 0, 0, 0);
        // Nuclear Option uses negative pitch for nose-up: pulling the thumbstick
        // back raises the nose, as the virtual stick does.
        return new ExternalFlightMotion(flying, flying ? Curve(leftY) : 0, flying ? Curve(leftX) : 0,
            Curve(rudderRight) - Curve(rudderLeft), camera);
    }

    // A trigger or grip that is already squeezed when the outside view starts
    // (the hand that held a virtual handle, a finger on the guns) does nothing
    // until it has been let go once. Past its start point it is proportional.
    private static float Press(bool valid, float value, ref bool armed, float rest, float start)
    {
        if (!valid || float.IsNaN(value) || float.IsInfinity(value))
        {
            armed = false;
            return 0;
        }
        value = Math.Max(0, Math.Min(1, value));
        if (!armed)
        {
            armed = value <= rest;
            return 0;
        }
        return value <= start ? 0 : (value - start) / (1 - start);
    }

    private static float Curve(float value) => Math.Sign(value) * (float)Math.Pow(Math.Abs(value), ResponseExponent);
}
