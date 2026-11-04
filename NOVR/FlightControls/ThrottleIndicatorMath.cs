using System;

namespace NOVR.MotionControls;

// Display-only values. The rail follows the physical lever position including
// its MIL shelf; the number describes the pilot's actual throttle command.
internal static class ThrottleIndicatorMath
{
    internal const float InboardOffset = .125f;
    internal static float InboardSide(float stickX, float throttleX) => stickX >= throttleX ? 1f : -1f;
    internal static float Command(float thrust) => FlightControlMath.IsFinite(thrust) ? FlightControlMath.Clamp(thrust, 0, 1) : 0;
    internal static int Percent(float thrust) => (int)Math.Round(Command(thrust) * 100f, MidpointRounding.AwayFromZero);
    internal static string State(float thrust, float militaryPower, bool collective, bool hover)
    {
        if (hover) return "HOVER";
        if (collective) return "COLL";
        var value = Command(thrust);
        if (value <= .005f) return "IDLE";
        if (militaryPower > 0 && militaryPower < 1)
        {
            if (value >= militaryPower) return "AB";
            if (value >= ThrottleDetent.MilitaryCommand(militaryPower) - .003f) return "MIL";
        }
        return value >= .995f ? "MAX" : "THR";
    }
}
