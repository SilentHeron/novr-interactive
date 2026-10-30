using System;

namespace NOVR.MotionControls;

// Persisted per-hand corrections are bounded separately from legacy settings.
// A malformed correction must not send a visible hand/touch point across the cockpit.
internal static class HandCalibrationMath
{
    internal static float Correction(float value, float limit) =>
        FlightControlMath.IsFinite(value) && FlightControlMath.IsFinite(limit) && limit > 0
            ? Math.Max(-limit, Math.Min(limit, value)) : 0;
}
