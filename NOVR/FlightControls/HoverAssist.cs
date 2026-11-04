namespace NOVR.MotionControls;

internal static class HoverAssist
{
    // These are the aircraft's native capability and height restrictions. The
    // action still goes through PilotPlayerState, including its G-LOC checks.
    internal static string? UnavailableReason(Aircraft? aircraft)
    {
        if (aircraft?.GetControlsFilter()?.HasAutoHover() != true) return "HOVER ASSIST NOT FITTED";
        return aircraft.radarAlt < 1f ? "HOVER ASSIST NEEDS AT LEAST 1 m ABOVE GROUND" : null;
    }
}
