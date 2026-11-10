using System;
using NOVR.VrCamera;

namespace NOVR.MotionControls;

internal readonly struct ExternalFlightMotion
{
    // Flying is false until the flight thumbstick has been centred once, so
    // entering the outside view with it deflected cannot move the aircraft.
    internal readonly bool Flying;
    internal readonly float Pitch, Roll, Yaw, ThrottleStep;
    internal readonly SpectatorStickMotion Camera;
    internal bool Rudder => Yaw != 0;
    internal ExternalFlightMotion(bool flying, float pitch, float roll, float yaw, float throttleStep, SpectatorStickMotion camera)
    {
        Flying = flying;
        Pitch = pitch;
        Roll = roll;
        Yaw = yaw;
        ThrottleStep = throttleStep;
        Camera = camera;
    }
}

// Flying from the outside view, laid out like the game's own gamepad defaults:
// left thumbstick pitch and roll, right thumbstick camera, left and right
// trigger rudder, and throttle down and up on the two shoulder inputs. Quest
// controllers have no bumpers, so the grips take that role on every controller.
// Squeezing both grips is no throttle change at all, which frees the camera
// thumbstick to move closer or farther. Sides are physical, as on a gamepad;
// cockpit handedness does not mirror them.
// No Unity input here: the caller owns tracking and menus.
internal sealed class ExternalFlightSticks
{
    internal const float ThrottlePerSecond = .6f, ResponseExponent = 1.5f;
    // One grip has to be held this long before it moves the throttle, so
    // squeezing both for the camera does not nudge the throttle on the way in.
    internal const float GripDelay = .12f;
    private const float TriggerRest = .15f, TriggerStart = .08f, GripRest = .35f, GripStart = .5f;
    private bool _flightArmed, _cameraArmed, _leftTriggerArmed, _rightTriggerArmed, _leftGripArmed, _rightGripArmed, _distance;
    private float _oneGripSeconds;

    internal void Reset()
    {
        _flightArmed = _cameraArmed = _leftTriggerArmed = _rightTriggerArmed = _leftGripArmed = _rightGripArmed = _distance = false;
        _oneGripSeconds = 0;
    }

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
        var throttleDown = Press(leftValid, leftGrip, ref _leftGripArmed, GripRest, GripStart);
        var throttleUp = Press(rightValid, rightGrip, ref _rightGripArmed, GripRest, GripStart);
        var dt = Math.Min(deltaSeconds, .05f);
        var distance = throttleDown > 0 && throttleUp > 0;
        if (distance != _distance)
        {
            // A deflection meant for looking around must not become a zoom
            // (or the reverse) when the grips change what the thumbstick does.
            _distance = distance;
            _cameraArmed = false;
            if (!distance)
            {
                // Letting go of one grip before the other must not move the
                // throttle: both start over from a release.
                _leftGripArmed = _rightGripArmed = false;
                throttleDown = throttleUp = 0;
            }
        }
        _oneGripSeconds = !distance && (throttleDown > 0 || throttleUp > 0) ? _oneGripSeconds + dt : 0;
        if (distance || _oneGripSeconds <= GripDelay)
            throttleDown = throttleUp = 0;
        SpectatorStickInput.Filter(leftValid, leftX, leftY, ref _flightArmed, out leftX, out leftY);
        SpectatorStickInput.Filter(rightValid, rightX, rightY, ref _cameraArmed, out rightX, out rightY);
        var flying = leftValid && _flightArmed;
        // Nuclear Option uses negative pitch for nose-up: pulling the thumbstick
        // back raises the nose, as the virtual stick does.
        var camera = distance
            ? new SpectatorStickMotion(rightX * 75f * dt, 0, -rightY * 1.5f * dt, 0, 0)
            : new SpectatorStickMotion(rightX * 75f * dt, -rightY * 60f * dt, 0, 0, 0);
        return new ExternalFlightMotion(flying, flying ? Curve(leftY) : 0, flying ? Curve(leftX) : 0,
            Curve(rudderRight) - Curve(rudderLeft), (throttleUp - throttleDown) * ThrottlePerSecond * dt, camera);
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
