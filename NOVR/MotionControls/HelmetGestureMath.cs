using System;

namespace NOVR.MotionControls;

internal static class HelmetGestureMath
{
    // Grip position relative to the tracked eyes, in head-facing meters. This
    // tolerates the controller in front of the visor without requiring contact.
    internal static bool AtBrow(float x, float y, float z)
    {
        if (!FlightControlMath.IsFinite(x) || !FlightControlMath.IsFinite(y) || !FlightControlMath.IsFinite(z) || z < 0) return false;
        x /= .24f; y = (y - .07f) / .15f; z = (z - .14f) / .18f;
        return x*x + y*y + z*z <= 1;
    }
}
