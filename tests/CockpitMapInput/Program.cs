using NOVR.MotionControls;

var checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
void Quiet(CockpitMapInputState map, string message) => Check(map.PanX == 0 && map.PanY == 0 && !map.SelectPressed, message);
void Near(float actual, float expected, string message) => Check(Math.Abs(actual - expected) < 1e-5, message);

// Entering from ordinary flight must not inherit deflection or a held click.
var map = new CockpitMapInputState();
map.Update(true, false, true, .8f, 0, true);
Check(!map.Reserved, "An unused map does not reserve ordinary flight controls");
map.Update(true, true, true, .8f, 0, true);
Check(map.Reserved, "Selecting a map claims the stick immediately");
Quiet(map, "Selection does not inherit yaw or a target click");
for (var i = 0; i < 30; i++) { map.Update(true, true, true, .8f, 0, true); Quiet(map, "Held activation input remains disarmed"); }
map.Update(true, true, true, 0, 0, true);
Quiet(map, "Centered axes alone cannot arm a held click");
map.Update(true, true, true, .16f, .16f, false);
Quiet(map, "A diagonal outside the radial center cannot arm the map");
map.Update(true, true, true, .1f, .1f, false);
Quiet(map, "Neutral activation frame arms without moving the map");
map.Update(true, true, true, 1, 0, false);
Near(map.PanX, 1, "Full horizontal travel pans at full speed");
Near(map.PanY, 0, "Horizontal pan never adds vertical movement");
map.Update(true, true, true, 0, -1, false);
Near(map.PanX, 0, "Vertical pan never adds horizontal movement");
Near(map.PanY, -1, "Vertical pan preserves direction");
map.Update(true, true, true, .6f, .8f, false);
Near(map.PanX, .6f, "Unit diagonal preserves horizontal component");
Near(map.PanY, .8f, "Unit diagonal preserves vertical component");
map.Update(true, true, true, 2, -2, false);
Near(map.PanX * map.PanX + map.PanY * map.PanY, 1, "Oversized diagonal is normalized rather than faster than cardinal motion");
Check(map.PanX > 0 && map.PanY < 0, "Normalization preserves the intended quadrant");
map.Update(true, true, true, .59f, 0, false);
Near(map.PanX, .5f, "Radial deadzone remaps remaining travel continuously");
map.Update(true, true, true, .18f, 0, false);
Quiet(map, "Deadzone boundary is quiet");
map.Update(true, true, true, 0, 0, true);
Check(map.SelectPressed, "A fresh armed stick click selects once");
for (var i = 0; i < 30; i++) { map.Update(true, true, true, 0, 0, true); Check(!map.SelectPressed, "Holding a map click never repeats selection"); }
map.Update(true, true, true, 0, 0, false);
Check(!map.SelectPressed, "Click release does not select");
map.Update(true, true, true, 0, 0, true);
Check(map.SelectPressed, "Release rearms the next intentional click");

// Compare map ownership against the real normal button gates. This models the
// caller contract: Reserved suppresses flight yaw and invalidates target/wheel
// gates until a released neutral frame can arm the appropriate context again.
var routed = new RoutedInput();
routed.Frame(true, false, true, 0, 0, false);
routed.Frame(true, true, true, 0, 0, false);
routed.Frame(true, true, true, .8f, .3f, true);
Check(routed.Map.SelectPressed && routed.TargetPresses == 0 && routed.WheelPresses == 0 && routed.Yaw == 0,
    "Map click/pan cannot reach target, wheel or yaw while owned");
routed.Frame(true, false, true, .8f, .3f, true);
Check(routed.Map.Reserved, "Deselecting a deflected/clicked map retains ownership for handoff");
Quiet(routed.Map, "Deselecting stops map movement immediately");
routed.Frame(true, false, true, 0, 0, true);
Check(routed.Map.Reserved && routed.TargetPresses == 0, "Centered axes cannot hand off a still-held map click");
routed.Frame(true, false, true, 0, 0, false);
Check(!routed.Map.Reserved && routed.TargetPresses == 0 && routed.Yaw == 0, "Neutral release hands back flight without inventing a click");
routed.Frame(true, false, true, .8f, 0, true);
Check(routed.TargetPresses == 1 && routed.Yaw > .7f, "Next deliberate flight command works after neutral handoff");
foreach (var residual in new[] { -.179f, -.15f, -.1201f, .1201f, .15f, .179f })
{
    var handoff = new RoutedInput();
    handoff.Frame(true, true, true, 0, 0, false);
    handoff.Frame(true, true, true, .7f, 0, false);
    handoff.Frame(true, false, true, residual, 0, false);
    Check(handoff.Map.Reserved && handoff.Yaw == 0, "Deselecting within map deadzone but outside flight deadzone cannot leak residual yaw");
    handoff.Frame(true, false, true, residual, 0, false);
    Check(handoff.Map.Reserved && handoff.Yaw == 0, "Residual deflection remains reserved on subsequent frames");
    handoff.Frame(true, false, true, .12f, 0, true);
    Check(handoff.Map.Reserved && handoff.TargetPresses == 0, "Flight-neutral axes still require map-click release");
    handoff.Frame(true, false, true, .12f, 0, false);
    Check(!handoff.Map.Reserved && handoff.Yaw == 0 && handoff.TargetPresses == 0, "Exact flight deadzone boundary returns ownership without yaw or clicks");
    handoff.Frame(true, false, true, residual, 0, false);
    Check(!handoff.Map.Reserved && Math.Abs(handoff.Yaw) > 0, "New deflection after flight-neutral handoff resumes ordinary yaw");
}
var diagonalHandoff = new RoutedInput();
diagonalHandoff.Frame(true, true, true, 0, 0, false);
diagonalHandoff.Frame(true, false, false, .1f, .1f, false);
Check(diagonalHandoff.Map.Reserved, "Diagonal handoff uses radial flight-neutral threshold");
diagonalHandoff.Frame(true, false, false, .08f, .08f, false);
Check(!diagonalHandoff.Map.Reserved && diagonalHandoff.WheelPresses == 0, "Centered diagonal release returns free-hand ownership without opening wheel");
routed.Frame(true, true, true, 0, 0, false);
routed.Frame(true, true, true, .7f, 0, true);
routed.Frame(true, true, false, .7f, 0, true);
Check(routed.Map.Reserved && routed.WheelPresses == 0, "Releasing the grip cannot open the wheel with a map click");
routed.Frame(true, true, false, 0, 0, true);
Check(routed.Map.Reserved, "Free-hand handoff also requires releasing the click");
routed.Frame(true, true, false, 0, 0, false);
Check(!routed.Map.Reserved && routed.WheelPresses == 0, "Free-hand neutral release finishes without a wheel action");
routed.Frame(true, true, false, 0, 0, true);
Check(routed.WheelPresses == 1, "A fresh free-hand click can open the wheel after handoff");
routed.Frame(true, true, true, .8f, 0, true);
Quiet(routed.Map, "Regripping requires neutral even if map remains selected");
routed.Frame(true, true, true, 0, 0, false);
routed.Frame(true, true, true, -.8f, 0, false);
Check(routed.Map.PanX < 0 && routed.Yaw == 0, "Regripped neutral map can pan again without yaw");

// Tracking/data loss cancels outputs but remembers prior ownership. A map
// closing during the interruption must not turn its old input into flight.
foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    foreach (var axis in new[] { 0, 1 })
    {
        var state = new CockpitMapInputState();
        state.Update(true, true, true, 0, 0, false);
        state.Update(true, true, true, .7f, .2f, true);
        state.Update(true, true, true, axis == 0 ? invalid : .7f, axis == 1 ? invalid : .2f, true);
        Quiet(state, "Invalid axis cancels map outputs and selection");
        state.Update(true, false, false, .7f, .2f, true);
        Check(state.Reserved, "Invalid-data recovery preserves handoff until neutral, even after deselection/release");
        state.Update(true, false, false, 0, 0, true);
        Check(state.Reserved, "Invalid-data handoff waits for click release");
        state.Update(true, false, false, 0, 0, false);
        Check(!state.Reserved, "Valid neutral frame clears invalid-data handoff");
    }
foreach (var stillSelected in new[] { false, true })
    foreach (var stillGripped in new[] { false, true })
    {
        var state = new CockpitMapInputState();
        state.Update(true, true, true, 0, 0, false);
        state.Update(true, true, true, .8f, 0, true);
        state.Update(false, stillSelected, stillGripped, 0, 0, false);
        Quiet(state, "Tracking loss cancels outputs without a release action");
        for (var i = 0; i < 3; i++)
            state.Update(false, stillSelected, stillGripped, 0, 0, false);
        state.Update(true, stillSelected, stillGripped, .8f, 0, true);
        Check(state.Reserved, "Reacquisition retains prior map ownership regardless of current selection/grip");
        Quiet(state, "Reacquisition cannot pan or select from held tracking samples");
        state.Update(true, stillSelected, stillGripped, 0, 0, false);
        Check(state.Reserved == (stillSelected && stillGripped), "Neutral reacquisition returns ownership only to the current map context");
        state.Update(true, stillSelected, stillGripped, .8f, 0, true);
        Check(state.SelectPressed == (stillSelected && stillGripped), "Only a new click in an armed map context can select after reacquisition");
    }
var pending = new CockpitMapInputState();
pending.Update(true, true, true, 0, 0, false);
pending.Update(true, false, false, .8f, 0, true);
pending.Update(false, false, false, 0, 0, false);
pending.Update(true, false, false, .8f, 0, true);
Check(pending.Reserved, "Tracking loss during an existing handoff cannot erase its guard");
pending.Reset();
Check(!pending.Reserved, "Explicit lifecycle reset releases disposed ownership");
Quiet(pending, "Lifecycle reset clears all output fields");
pending.Update(true, true, true, 1, 0, true);
Quiet(pending, "Fresh lifecycle still requires neutral before map input");
var unowned = new CockpitMapInputState();
unowned.Update(false, false, false, 0, 0, false);
unowned.Update(true, false, true, .8f, 0, true);
Check(!unowned.Reserved, "Tracking loss without prior ownership does not invent a map handoff");

// Sweep ordinary axes, including diagonal ranges beyond typical device output.
var sweep = new CockpitMapInputState();
sweep.Update(true, true, true, 0, 0, false);
for (var x = -20; x <= 20; x++)
    for (var y = -20; y <= 20; y++)
    {
        sweep.Update(true, true, true, x / 10f, y / 10f, false);
        Check(float.IsFinite(sweep.PanX) && float.IsFinite(sweep.PanY) && sweep.PanX * sweep.PanX + sweep.PanY * sweep.PanY <= 1.00001f,
            "Every finite axis pair yields bounded finite radial pan");
        Check(!sweep.SelectPressed && sweep.Reserved, "Axis motion cannot create selections or lose ownership");
    }
foreach (var huge in new[] { float.MaxValue, -float.MaxValue })
{
    sweep.Update(true, true, true, huge, huge, false);
    Check(float.IsFinite(sweep.PanX) && float.IsFinite(sweep.PanY), "Extreme finite device values cannot produce NaN pan");
}
Console.WriteLine($"Cockpit map input checks passed: {checks} assertions (real ownership/gate classes; no game or headset required).");

internal sealed class RoutedInput
{
    internal readonly CockpitMapInputState Map = new();
    private readonly ControllerButtonGate _target = new(), _wheel = new();
    internal int TargetPresses, WheelPresses;
    internal float Yaw;
    internal void Frame(bool available, bool selected, bool held, float x, float y, bool clicked)
    {
        Map.Update(available, selected, held, x, y, clicked);
        var ordinary = available && !Map.Reserved;
        _target.Update(ordinary && held, clicked ? 1 : 0);
        _wheel.Update(ordinary && !held, clicked ? 1 : 0);
        if (_target.Pressed)
            TargetPresses++;
        if (_wheel.Pressed)
            WheelPresses++;
        Yaw = ordinary && held ? FlightControlMath.Axis(x, .12f, 1) : 0;
    }
}
