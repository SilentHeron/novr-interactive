using System;

namespace NOVR.VrCamera;

// Pure decision for the VR far-plane lease. The preference is a minimum: it
// never shortens the game's own far plane, and malformed values on either side
// leave the camera untouched.
internal static class VrFarClipPolicy
{
    internal const float Default = 250000f;
    internal const float Maximum = 1000000f;

    internal static bool TryExtend(float native, float requested, out float value)
    {
        value = native;
        if (!Finite(native) || native <= 0 || !Finite(requested) || requested <= 0)
            return false;
        var limited = Math.Min(requested, Maximum);
        if (limited <= native)
            return false;
        value = limited;
        return true;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
