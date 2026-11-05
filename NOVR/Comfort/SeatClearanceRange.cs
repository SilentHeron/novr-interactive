using System;

namespace NOVR.Comfort;

// Keep config validation, the menu slider and camera-time sanitation aligned.
internal static class SeatClearanceRange
{
    internal const float Minimum = .05f, Maximum = .40f, Default = .25f;

    internal static bool TryClamp(float value, out float radius)
    {
        radius = 0;
        if (float.IsNaN(value) || float.IsInfinity(value))
            return false;
        radius = Math.Max(Minimum, Math.Min(Maximum, value));
        return true;
    }
}
