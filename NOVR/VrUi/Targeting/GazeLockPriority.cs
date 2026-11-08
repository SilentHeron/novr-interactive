namespace NOVR.MotionControls;

internal enum GazeLockPriority
{
    Nearest, UnitsBeforeMissiles, AircraftFirst
}

// Pure ranking for the look-direction cue and the gaze lock. Inside the cue's
// angular cone a lower tier always wins; the nearest angle only decides within
// one tier. Both the visible cue and the Select action use the same answer.
internal static class TargetCuePriority
{
    internal const GazeLockPriority Default = GazeLockPriority.UnitsBeforeMissiles;

    // Invalid persisted values must never silently become 'nearest'.
    internal static GazeLockPriority Normalize(GazeLockPriority value) =>
        (uint)value <= (uint)GazeLockPriority.AircraftFirst ? value : Default;

    internal static int Tier(GazeLockPriority priority, bool aircraft, bool missile) => Normalize(priority) switch
    {
        GazeLockPriority.UnitsBeforeMissiles => missile ? 1 : 0,
        GazeLockPriority.AircraftFirst => aircraft ? 0 : missile ? 2 : 1,
        _ => 0
    };

    // Friendly units are not a weapon target by default. The game's own target
    // filters (factions, unit types) still apply on top of this choice.
    internal static bool Eligible(bool allowFriendly, bool friendly) => allowFriendly || !friendly;
}
