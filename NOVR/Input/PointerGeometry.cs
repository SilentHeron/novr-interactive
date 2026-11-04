using System;

namespace NOVR.MotionControls;

internal static class PointerGeometry
{
    // The ray is transformed into a rectangle's local coordinates without
    // renormalizing its direction, so t remains a distance in world meters.
    public static bool IntersectRect(float ox, float oy, float oz, float dx, float dy, float dz,
        float minX, float minY, float maxX, float maxY, out float distance)
    {
        distance = 0;
        if (!FlightControlMath.IsFinite(ox) || !FlightControlMath.IsFinite(oy) || !FlightControlMath.IsFinite(oz) ||
            !FlightControlMath.IsFinite(dx) || !FlightControlMath.IsFinite(dy) || !FlightControlMath.IsFinite(dz) || Math.Abs(dz) < 0.000001f) return false;
        var t = -oz / dz;
        if (!FlightControlMath.IsFinite(t) || t < 0.02f || t > 20f) return false;
        var x = ox + dx * t;
        var y = oy + dy * t;
        if (x < minX || x > maxX || y < minY || y > maxY) return false;
        distance = t;
        return true;
    }

    public static float Zoom(float current, float axis, float deltaTime)
    {
        if (!FlightControlMath.IsFinite(current)) current = 1;
        if (!FlightControlMath.IsFinite(axis) || !FlightControlMath.IsFinite(deltaTime) || deltaTime <= 0) return FlightControlMath.Clamp(current, 1, 40);
        return FlightControlMath.Clamp(current * (float)Math.Exp(FlightControlMath.Axis(axis, 0.2f, 1) * Math.Min(deltaTime, 0.05f) * 1.8f), 1, 40);
    }
}
