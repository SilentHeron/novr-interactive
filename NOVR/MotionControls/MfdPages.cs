namespace NOVR.MotionControls;

internal enum MfdPage { Weapons, Map, Flight, EngineFuel, Systems, Tactical, Sensor }

internal static class MfdPages
{
    internal const int Count = 7;
    internal static string Title(MfdPage page) => page switch
    {
        MfdPage.Weapons => "WEAPONS / CM", MfdPage.Map => "MAP", MfdPage.Flight => "FLIGHT",
        MfdPage.EngineFuel => "ENGINE / FUEL", MfdPage.Systems => "SYSTEMS", MfdPage.Tactical => "TACTICAL",
        MfdPage.Sensor => "SENSOR", _ => "DISPLAY"
    };
    internal static string ShortTitle(MfdPage page) => page switch
    {
        MfdPage.Weapons => "WEAPONS", MfdPage.EngineFuel => "ENG / FUEL", _ => Title(page)
    };
    internal static (MfdPage left, MfdPage right) Select(MfdPage left, MfdPage right, bool leftDisplay, MfdPage next)
    {
        if ((int)next < 0 || (int)next >= Count) return (left, right);
        var current = leftDisplay ? left : right;
        return leftDisplay ? (next, next == right ? current : right) : (next == left ? current : left, next);
    }
    // Native widgets have one owner. Selecting the other display's page swaps
    // them, so neither display goes blank and no native map/UI is duplicated.
    internal static (MfdPage left, MfdPage right) Cycle(MfdPage left, MfdPage right, bool leftDisplay, int direction, int available = (1 << Count) - 1)
    {
        var current = leftDisplay ? left : right;
        for (var step = 1; step < Count; step++)
        {
            var next = (MfdPage)(((int)current + (direction < 0 ? Count - step : step)) % Count);
            if ((available & (1 << (int)next)) != 0) return Select(left, right, leftDisplay, next);
        }
        return (left, right);
    }
}
