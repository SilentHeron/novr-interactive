using System;

namespace NOVR.MotionControls;

// Engine-independent control rules, also exercised by NOVR.Tests without the game or a headset.
internal static class FlightControlMath
{
    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));

    // Some runtimes expose squeeze click independently of squeeze pressure.
    // A real click is intentional input; capacitive presence is never passed here.
    public static float GripValue(bool hasAnalog, float analog, bool pressed) =>
        pressed ? 1f : !hasAnalog ? 0f : IsFinite(analog) ? Clamp(analog, 0f, 1f) : float.NaN;

    public static float Axis(float value, float deadzone, float exponent)
    {
        if (!IsFinite(value)) return 0f;
        deadzone = IsFinite(deadzone) ? Clamp(deadzone, 0f, 0.95f) : 0.06f;
        exponent = IsFinite(exponent) ? Clamp(exponent, 1f, 3f) : 1.5f;
        var magnitude = Clamp((Math.Abs(value) - deadzone) / (1f - deadzone), 0f, 1f);
        return Math.Sign(value) * (float)Math.Pow(magnitude, exponent);
    }

    public static float Throttle(float initialValue, float initialPosition, float position, float travel)
    {
        if (!IsFinite(initialValue) || !IsFinite(initialPosition) || !IsFinite(position) ||
            !IsFinite(travel) || travel < 0.05f) return Clamp(IsFinite(initialValue) ? initialValue : 0f, 0f, 1f);
        return Clamp(initialValue + (position - initialPosition) / travel, 0f, 1f);
    }

    // Both input hit testing and the model use this path. Collective input is
    // measured vertically; the small fore/aft arc is visual, not another axis.
    public static float CollectiveArmLength(float travel) => Math.Max(.28f, travel);

    public static (float height, float forward) ThrottleOffset(bool collective, float value, float travel)
    {
        var displacement = (Clamp(value, 0f, 1f) - .5f) * travel;
        if (!collective) return (0f, displacement);
        var length = CollectiveArmLength(travel);
        return (displacement, (float)Math.Sqrt(length * length - displacement * displacement) - length);
    }

    public static (float pitch, float roll) Stick(float tiltX, float tiltY, float tiltZ,
        float angle, float deadzone, float exponent)
    {
        if (!IsFinite(tiltX) || !IsFinite(tiltY) || !IsFinite(tiltZ) || !IsFinite(angle) || angle <= 0f)
            return (0f, 0f);
        const float radiansToDegrees = 180f / (float)Math.PI;
        // Nuclear Option uses negative pitch for nose-up (negative local X rotation).
        // Tilting the handle toward the pilot gives negative Z, matching that convention.
        return (Axis((float)Math.Atan2(tiltZ, tiltY) * radiansToDegrees / angle, deadzone, exponent),
                Axis((float)Math.Atan2(tiltX, tiltY) * radiansToDegrees / angle, deadzone, exponent));
    }

    public static float StickTwist(float rotationY, float rotationW, float angle, float deadzone, float exponent)
    {
        // Swing/twist decomposition around cockpit up: project the relative
        // quaternion onto Y. Pure pitch/roll swing contributes no rudder.
        if (!IsFinite(rotationY) || !IsFinite(rotationW) || !IsFinite(angle) || angle <= 0f ||
            rotationY * rotationY + rotationW * rotationW < 0.000001f) return 0f;
        var degrees = 2f * (float)Math.Atan2(rotationY, rotationW) * (180f / (float)Math.PI);
        if (degrees > 180f) degrees -= 360f;
        if (degrees < -180f) degrees += 360f;
        return Axis(degrees / angle, deadzone, exponent);
    }

    // The game's simulatedThrottle is in binding space, before negative-range/collective conversion.
    public static float ToSimulatedThrottle(float throttle, bool negativeRange, bool invertedCollective)
    {
        var value = Clamp(throttle, 0f, 1f);
        if (invertedCollective) value = 1f - value;
        return negativeRange ? value * 2f - 1f : value;
    }

    public static float SmoothingWeight(float elapsed, float response)
    {
        if (!IsFinite(elapsed) || !IsFinite(response) || elapsed <= 0f || response <= 0f) return 0f;
        return 1f - (float)Math.Exp(-elapsed * response);
    }
}

internal sealed class GripLatch
{
    private bool _armed;
    public bool Held { get; private set; }

    // A release is required after activation, tracking loss, or a press outside the handle.
    // This avoids grabbing a control unexpectedly when tracking/menu focus returns.
    public bool Update(bool valid, float grip, bool withinReach)
    {
        if (!valid || !FlightControlMath.IsFinite(grip))
        {
            Reset();
            return false;
        }
        if (grip <= 0.35f)
        {
            Held = false;
            _armed = true;
        }
        else if (grip >= 0.65f && !Held)
        {
            Held = _armed && withinReach;
            _armed = false;
        }
        return Held;
    }

    public void Reset()
    {
        Held = false;
        _armed = false;
    }
}
