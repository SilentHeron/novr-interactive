namespace NOVR.MotionControls;

internal enum GazeLockPriority
{
    Nearest, UnitsBeforeMissiles, AircraftFirst,
    // Appended: the config file stores the names, and an older build reading
    // a newer file must not find its own values renumbered.
    Game
}

// Ranking for the gaze lock. Game hands the choice inside the cue's cone to
// the game's own priority score, which is what a target-select tap does on a
// flat screen. The other three are NOVR's rules: a lower tier always wins and
// the nearest angle only decides within one tier.
internal static class TargetCuePriority
{
    internal const GazeLockPriority Default = GazeLockPriority.Game;

    // The menus list the default first; the stored value keeps its own number.
    internal static readonly GazeLockPriority[] MenuOrder =
    {
        GazeLockPriority.Game, GazeLockPriority.Nearest, GazeLockPriority.UnitsBeforeMissiles, GazeLockPriority.AircraftFirst
    };
    internal static readonly string[] MenuLabels = { "Game", "Nearest", "Units first", "Aircraft first" };
    internal static int MenuIndex(GazeLockPriority value) => System.Math.Max(0, System.Array.IndexOf(MenuOrder, Normalize(value)));
    internal static GazeLockPriority FromMenu(int index) => (uint)index < (uint)MenuOrder.Length ? MenuOrder[index] : Default;

    // Invalid persisted values must never silently become 'nearest'.
    internal static GazeLockPriority Normalize(GazeLockPriority value) =>
        (uint)value <= (uint)GazeLockPriority.Game ? value : Default;

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
