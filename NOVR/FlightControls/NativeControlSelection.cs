namespace NOVR.MotionControls;

// A helicopter changes the throttle assembly, never the shared flight stick.
internal static class NativeControlSelection
{
    internal static string Source(bool stick, bool collective) =>
        !stick && collective ? "UtilityHelo1" : "SmallFighter1";

    internal static bool FittedFs20(bool stick, bool collective, bool foundStandard) =>
        foundStandard && (stick || !collective);
}
