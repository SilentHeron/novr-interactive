using System.Globalization;
using NOVR.MotionControls;

var checks = 0;
void Check(string name, Action body) { body(); checks++; Console.WriteLine("PASS " + name); }
void Assert(bool value) { if (!value) throw new InvalidOperationException("MFD context assertion failed"); }
void Near(float value, float expected) => Assert(Math.Abs(value - expected) < .00001f);
MfdContextState Ready() => new()
{
    Available = (1 << MfdPages.Count) - 1, MapReady = true, MapZoom = 2,
    SensorReady = true, OpticalEnabled = true, HeadFeed = true, CanLookToAim = true,
    CanTrackSelected = true, SensorZoom = 2, HasTargets = true, LocalHudReady = true,
    WeaponActionsReady = true, CountermeasureActionsReady = true, CanSwap = true
};
MfdContextKey Key(MfdPage page, int key, MfdContextState? state = null) => MfdContextControls.Key(page, key, state ?? Ready());

Check("Every valid page has four short labelled actions and an explanation", () =>
{
    for (var page = 0; page < MfdPages.Count; page++) for (var key = 0; key < MfdContextControls.KeyCount; key++)
    {
        var item = Key((MfdPage)page, key);
        Assert(item.label.Length is > 0 and <= 8 && !item.label.Contains('\n'));
        Assert(item.hint.Length > 0 && item.action != MfdContextAction.None && item.enabled);
    }
});
Check("Invalid key indices and page values never expose an action", () =>
{
    foreach (var page in new[] { (MfdPage)(-1), (MfdPage)7, (MfdPage)int.MaxValue })
        for (var key = -1; key <= 4; key++) Assert(!Key(page, key).enabled && Key(page, key).action == MfdContextAction.None);
    foreach (var key in new[] { -1, 4, int.MinValue, int.MaxValue })
        Assert(!Key(MfdPage.Map, key).enabled && Key(MfdPage.Map, key).label.Length == 0);
});
Check("Map keys map to zoom, center and orientation", () =>
{
    var expected = new[] { MfdContextAction.ZoomIn, MfdContextAction.ZoomOut, MfdContextAction.RecenterMap, MfdContextAction.ToggleMapOrientation };
    for (var i = 0; i < expected.Length; i++) Assert(Key(MfdPage.Map, i).action == expected[i]);
});
Check("Unavailable map disables every map operation with an honest reason", () =>
{
    var state = Ready(); state.MapReady = false;
    for (var i = 0; i < 4; i++) Assert(!Key(MfdPage.Map, i, state).enabled && Key(MfdPage.Map, i, state).hint == "MAP NOT READY");
});
Check("Map orientation labels report the current orientation", () =>
{
    var state = Ready(); Assert(Key(MfdPage.Map, 3, state).label == "HDG UP");
    Assert(Key(MfdPage.Map, 3, state).hint == "CHANGE TO NORTH UP");
    state.MapNorthUp = true; Assert(Key(MfdPage.Map, 3, state).label == "NORTH UP");
    Assert(Key(MfdPage.Map, 3, state).hint == "CHANGE TO HEADING UP");
});
Check("Map zoom bounds disable only the exhausted direction", () =>
{
    var state = Ready(); state.MapZoom = 1;
    Assert(Key(MfdPage.Map, 0, state).enabled && !Key(MfdPage.Map, 1, state).enabled);
    state.MapZoom = 12; Assert(!Key(MfdPage.Map, 0, state).enabled && Key(MfdPage.Map, 1, state).enabled);
});
Check("Map zoom keeps multiplicative steps and clamps to supported bounds", () =>
{
    Near(MfdContextControls.MapZoom(2, 1), 2.7f); Near(MfdContextControls.MapZoom(2.7f, -1), 2);
    Near(MfdContextControls.MapZoom(12, 1), 12); Near(MfdContextControls.MapZoom(1, -1), 1);
});
Check("Sensor manual zoom requires an actual fresh HEAD feed", () =>
{
    var state = Ready(); state.HeadFeed = false;
    for (var i = 0; i < 2; i++) Assert(!Key(MfdPage.Sensor, i, state).enabled && Key(MfdPage.Sensor, i, state).hint == "HEAD FEED REQUIRED");
});
Check("TRACK mode retains automatic framing despite a stale HEAD flag", () =>
{
    var state = Ready(); state.Tracking = true;
    for (var i = 0; i < 2; i++) Assert(!Key(MfdPage.Sensor, i, state).enabled && Key(MfdPage.Sensor, i, state).hint == "TRACK USES AUTO ZOOM");
});
Check("All optical capability combinations enforce fresh HEAD-only zoom", () =>
{
    for (var mask = 0; mask < 16; mask++)
    {
        var state = Ready(); state.SensorReady = (mask & 1) != 0; state.OpticalEnabled = (mask & 2) != 0;
        state.Tracking = (mask & 4) != 0; state.HeadFeed = (mask & 8) != 0;
        var expected = state.SensorReady && state.OpticalEnabled && !state.Tracking && state.HeadFeed;
        Assert(Key(MfdPage.Sensor, 0, state).enabled == expected && Key(MfdPage.Sensor, 1, state).enabled == expected);
    }
});
Check("Sensor zoom uses half-step increments and directional limits", () =>
{
    Near(MfdContextControls.SensorZoom(2, 1), 2.5f); Near(MfdContextControls.SensorZoom(2, -1), 1.5f);
    var state = Ready(); state.SensorZoom = 6;
    Assert(!Key(MfdPage.Sensor, 0, state).enabled && Key(MfdPage.Sensor, 1, state).enabled);
    state.SensorZoom = 1; Assert(Key(MfdPage.Sensor, 0, state).enabled && !Key(MfdPage.Sensor, 1, state).enabled);
});
Check("Disabled optical HUD offers a real HEAD activation action", () =>
{
    var state = Ready(); state.OpticalEnabled = false; state.HeadFeed = false;
    var key = Key(MfdPage.Sensor, 2, state);
    Assert(key.enabled && key.action == MfdContextAction.HeadAim && key.label == "HEAD" && key.state == "OFF");
});
Check("HEAD offers tracking only when a selected target exists", () =>
{
    var state = Ready(); var key = Key(MfdPage.Sensor, 2, state);
    Assert(key.enabled && key.action == MfdContextAction.TrackSelected && key.label == "TRACK" && key.state == "HEAD");
    state.HasTargets = false; key = Key(MfdPage.Sensor, 2, state);
    Assert(!key.enabled && key.hint == "SELECT A TARGET FIRST");
});
Check("TRACK offers HEAD without clearing existing targets", () =>
{
    var state = Ready(); state.Tracking = true;
    var key = Key(MfdPage.Sensor, 2, state);
    Assert(key.enabled && key.action == MfdContextAction.HeadAim && key.label == "HEAD" && key.state == "TRACK");
});
Check("Optical mode keys respect verified native mode capabilities", () =>
{
    for (var mask = 0; mask < 64; mask++)
    {
        var state = Ready(); state.SensorReady = (mask & 1) != 0; state.OpticalEnabled = (mask & 2) != 0;
        state.Tracking = (mask & 4) != 0; state.CanLookToAim = (mask & 8) != 0;
        state.CanTrackSelected = (mask & 16) != 0; state.HasTargets = (mask & 32) != 0;
        var head = state.Tracking || !state.OpticalEnabled;
        Assert(Key(MfdPage.Sensor, 2, state).enabled == (state.SensorReady && (head ? state.CanLookToAim : state.CanTrackSelected && state.HasTargets)));
    }
});
Check("Clear requires the local target HUD and at least one selected target", () =>
{
    for (var mask = 0; mask < 4; mask++)
    {
        var state = Ready(); state.LocalHudReady = (mask & 1) != 0; state.HasTargets = (mask & 2) != 0;
        var key = Key(MfdPage.Sensor, 3, state);
        Assert(key.action == MfdContextAction.ClearTargets && key.enabled == (mask == 3));
    }
});
Check("Weapons keys use existing weapon and countermeasure operations", () =>
{
    var expected = new[] { MfdContextAction.PreviousWeapon, MfdContextAction.NextWeapon, MfdContextAction.Map, MfdContextAction.NextCountermeasure };
    for (var i = 0; i < 4; i++) Assert(Key(MfdPage.Weapons, i).action == expected[i]);
    Assert(!Enum.GetNames<MfdContextAction>().Any(name => name.Contains("Safety")));
});
Check("Weapon selection and countermeasure capability are independent", () =>
{
    for (var mask = 0; mask < 4; mask++)
    {
        var state = Ready(); state.WeaponActionsReady = (mask & 1) != 0; state.CountermeasureActionsReady = (mask & 2) != 0;
        Assert(Key(MfdPage.Weapons, 0, state).enabled == state.WeaponActionsReady);
        Assert(Key(MfdPage.Weapons, 1, state).enabled == state.WeaponActionsReady);
        Assert(Key(MfdPage.Weapons, 3, state).enabled == state.CountermeasureActionsReady);
        Assert(Key(MfdPage.Weapons, 2, state).enabled);
    }
});
Check("Read-only pages link to useful related pages", () =>
{
    var cases = new[] {
        (MfdPage.Flight, MfdContextAction.Map, MfdContextAction.EngineFuel),
        (MfdPage.EngineFuel, MfdContextAction.Flight, MfdContextAction.Systems),
        (MfdPage.Systems, MfdContextAction.Flight, MfdContextAction.Tactical),
        (MfdPage.Tactical, MfdContextAction.Map, MfdContextAction.Sensor)
    };
    foreach (var (page, first, second) in cases)
    {
        Assert(Key(page, 0).action == first && Key(page, 1).action == second);
        Assert(Key(page, 2).action == MfdContextAction.PageMenu && Key(page, 3).action == MfdContextAction.SwapDisplays);
    }
});
Check("Every page shortcut respects its destination availability bit", () =>
{
    var destinations = new Dictionary<MfdContextAction, MfdPage> {
        [MfdContextAction.Map] = MfdPage.Map, [MfdContextAction.Sensor] = MfdPage.Sensor,
        [MfdContextAction.Flight] = MfdPage.Flight, [MfdContextAction.EngineFuel] = MfdPage.EngineFuel,
        [MfdContextAction.Systems] = MfdPage.Systems, [MfdContextAction.Tactical] = MfdPage.Tactical,
        [MfdContextAction.Weapons] = MfdPage.Weapons
    };
    for (var available = 0; available < 1 << MfdPages.Count; available++)
    {
        var state = Ready(); state.Available = available;
        for (var page = 0; page < MfdPages.Count; page++) for (var index = 0; index < 4; index++)
        {
            var key = Key((MfdPage)page, index, state);
            if (destinations.TryGetValue(key.action, out var target)) Assert(key.enabled == ((available & (1 << (int)target)) != 0));
        }
    }
});
Check("MENU and SWAP report their actual availability", () =>
{
    var state = Ready(); state.Available = 0; state.CanSwap = false;
    Assert(!Key(MfdPage.Flight, 2, state).enabled && !Key(MfdPage.Flight, 3, state).enabled);
    state.Available = 1 << (int)MfdPage.Map;
    Assert(Key(MfdPage.Flight, 2, state).enabled && !Key(MfdPage.Flight, 3, state).enabled);
    state.CanSwap = true; Assert(Key(MfdPage.Flight, 3, state).enabled);
});
Check("Malformed numeric settings are sanitized before values and bounds are shown", () =>
{
    foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MinValue, float.MaxValue })
        foreach (var direction in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
        {
            var map = MfdContextControls.MapZoom(value, direction); var sensor = MfdContextControls.SensorZoom(value, direction);
            Assert(float.IsFinite(map) && map >= 1 && map <= 12);
            Assert(float.IsFinite(sensor) && sensor >= 1 && sensor <= 6);
        }
});
Check("State labels keep their short stable format under non-English cultures", () =>
{
    var previous = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        var state = Ready(); state.MapZoom = 2.5f; state.SensorZoom = 1.5f;
        Assert(Key(MfdPage.Map, 0, state).state == "2.5×");
        Assert(Key(MfdPage.Sensor, 0, state).state == "1.5×");
        Assert(Key(MfdPage.Flight, 2, state).label == "MENU");
    }
    finally { CultureInfo.CurrentCulture = previous; }
});
Check("A stale enabled label becomes disabled on fresh capability evaluation", () =>
{
    var state = Ready(); Assert(Key(MfdPage.Sensor, 0, state).enabled);
    state.HeadFeed = false; Assert(!Key(MfdPage.Sensor, 0, state).enabled);
    state = Ready(); Assert(Key(MfdPage.Weapons, 0, state).enabled);
    state.WeaponActionsReady = false; Assert(!Key(MfdPage.Weapons, 0, state).enabled);
});
Check("Open picker remaps all four keys without exposing underlying page actions", () =>
{
    var state = Ready(); state.PickerOpen = true; state.PickerSelection = MfdPage.Flight;
    var actions = new[] { MfdContextAction.PickerPrevious, MfdContextAction.PickerNext, MfdContextAction.PickerOpen, MfdContextAction.PickerBack };
    var labels = new[] { "PREV", "NEXT", "OPEN", "BACK" };
    for (var page = 0; page < MfdPages.Count; page++) for (var key = 0; key < 4; key++)
    {
        var actual = Key((MfdPage)page, key, state);
        Assert(actual.action == actions[key] && actual.label == labels[key] && actual.enabled);
    }
});
Check("Picker directional and OPEN keys reflect one or zero fitted pages", () =>
{
    var state = Ready(); state.PickerOpen = true; state.PickerSelection = MfdPage.Map;
    state.Available = 1 << (int)MfdPage.Map;
    Assert(!Key(MfdPage.Flight, 0, state).enabled && !Key(MfdPage.Flight, 1, state).enabled);
    Assert(Key(MfdPage.Flight, 2, state).enabled && Key(MfdPage.Flight, 3, state).enabled);
    state.Available = 0;
    Assert(!Key(MfdPage.Flight, 2, state).enabled && Key(MfdPage.Flight, 3, state).enabled);
});
Check("Picker opens at current page and skips every unavailable page in both directions", () =>
{
    for (var mask = 1; mask < 1 << MfdPages.Count; mask++)
    for (var page = 0; page < MfdPages.Count; page++)
    {
        var picker = new MfdPagePicker(); Assert(picker.Show((MfdPage)page, mask));
        if ((mask & (1 << page)) != 0) Assert(picker.Selection == (MfdPage)page);
        var start = picker.Selection; var count = MfdPagePicker.Count(mask);
        var seen = new HashSet<MfdPage> { start };
        for (var step = 0; step < count; step++)
        {
            Assert(picker.Move(1) == (count > 1)); Assert(MfdPagePicker.Fitted(picker.Selection, mask)); seen.Add(picker.Selection);
        }
        Assert(picker.Selection == start && seen.Count == count);
        for (var step = 0; step < count; step++) { picker.Move(-1); Assert(MfdPagePicker.Fitted(picker.Selection, mask)); }
        Assert(picker.Selection == start);
    }
});
Check("Picker cancellation preserves pages and generations change on each action context", () =>
{
    var picker = new MfdPagePicker(); var all = (1 << MfdPages.Count) - 1; var version = picker.Generation;
    Assert(picker.Show(MfdPage.Systems, all) && picker.Generation != version); version = picker.Generation;
    Assert(picker.Move(1) && picker.Generation != version); version = picker.Generation;
    picker.Close(); Assert(!picker.Open && picker.Generation != version); version = picker.Generation;
    picker.Close(); Assert(picker.Generation == version && !picker.Confirm(out _));
    Assert(picker.Show(MfdPage.Systems, all) && picker.Selection == MfdPage.Systems);
    Assert(picker.Confirm(out var chosen) && chosen == MfdPage.Systems && !picker.Open);
});
Check("Picker confirmation uses existing single-owner page selection", () =>
{
    var picker = new MfdPagePicker(); picker.Show(MfdPage.Flight, (1 << (int)MfdPage.Flight) | (1 << (int)MfdPage.Systems));
    picker.Move(1); Assert(picker.Confirm(out var chosen));
    Assert(MfdPages.Select(MfdPage.Flight, MfdPage.Systems, true, chosen) == (MfdPage.Systems, MfdPage.Flight));
    Assert(!picker.Confirm(out _));
});
Check("Losing a highlighted page safely selects another fitted page and invalidates OPEN", () =>
{
    var picker = new MfdPagePicker(); picker.Show(MfdPage.EngineFuel, (1 << MfdPages.Count) - 1); var version = picker.Generation;
    picker.Refresh((1 << (int)MfdPage.Map) | (1 << (int)MfdPage.Sensor));
    Assert(picker.Generation != version && picker.Selection == MfdPage.Sensor); version = picker.Generation;
    picker.Refresh(0); Assert(picker.Generation != version && !picker.Confirm(out _));
    picker.Close(); Assert(!picker.Open && !picker.Show(MfdPage.Map, 0));
});
Check("Independent display pickers cannot inherit each other's selection", () =>
{
    var left = new MfdPagePicker(); var right = new MfdPagePicker(); var all = (1 << MfdPages.Count) - 1;
    left.Show(MfdPage.Flight, all); right.Show(MfdPage.Map, all); left.Move(1);
    Assert(left.Selection == MfdPage.EngineFuel && right.Selection == MfdPage.Map);
    left.Close(); Assert(!left.Open && right.Open);
});
Console.WriteLine($"{checks} MFD context checks passed (pure production matrix; no native controls or game launch).");
