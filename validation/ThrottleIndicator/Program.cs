using NOVR.MotionControls;

var assertions = 0;
void Check(bool condition, string label) { assertions++; if (!condition) throw new Exception(label); }
Check(ThrottleIndicatorMath.InboardSide(.22f, -.24f) == 1, "Left throttle indicates toward pilot center");
Check(ThrottleIndicatorMath.InboardSide(-.22f, .24f) == -1, "Mirrored throttle indicates toward pilot center");
Check(ThrottleIndicatorMath.InboardOffset > .10f, "Rail clears fitted 16 cm housing by at least 2 cm");
foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 0f })
    Check(ThrottleIndicatorMath.Percent(value) == 0, "Invalid/negative commands cannot generate invalid captions");
Check(ThrottleIndicatorMath.Percent(1.2f) == 100, "Overrange command clamps to 100 percent");
Check(ThrottleIndicatorMath.Percent(.755f) == 76, "Percent rounds to nearest whole command");
Check(ThrottleIndicatorMath.State(0, .8f, false, false) == "IDLE", "Idle readout");
Check(ThrottleIndicatorMath.State(.5f, .8f, false, false) == "THR", "Cruise readout");
Check(ThrottleIndicatorMath.State(ThrottleDetent.MilitaryCommand(.8f), .8f, false, false) == "MIL", "Dry-side military-power readout");
Check(ThrottleIndicatorMath.State(.8f, .8f, false, false) == "AB", "Exact native AB boundary is not mislabeled MIL");
Check(ThrottleIndicatorMath.State(.81f, .8f, false, false) == "AB", "Afterburner readout");
Check(ThrottleIndicatorMath.State(1, 1, false, false) == "MAX", "Aircraft without afterburner never invents AB");
Check(ThrottleIndicatorMath.State(.8f, -1, false, false) == "THR", "Unknown military threshold does not invent MIL");
Check(ThrottleIndicatorMath.State(.8f, .8f, true, false) == "COLL", "Helicopter uses collective caption");
Check(ThrottleIndicatorMath.State(.8f, .8f, true, true) == "HOVER", "Hover mode identifies command source");
foreach (var travel in new[] { .05f, .12f, .22f })
{
    var idle = ThrottleDetent.Position(0, .8f, travel);
    var mil = ThrottleDetent.Position(.8f, .8f, travel);
    var full = ThrottleDetent.Position(1, .8f, travel);
    Check(Math.Abs(idle) < .000001f && Math.Abs(full - 1) < .000001f && mil > 0 && mil < 1, "MIL cursor is inside physical end stops");
    Check(ThrottleIndicatorMath.Percent(ThrottleDetent.MilitaryCommand(.8f)) == 80 && ThrottleIndicatorMath.State(ThrottleDetent.MilitaryCommand(.8f), .8f, false, false) == "MIL",
        "Readout remains actual command rather than rescaled travel percentage");
}
foreach (var onset in new[] { .5f, .85f, .9f, .95f })
foreach (var travel in new[] { .05f, .12f, .22f })
{
    var mil = ThrottleDetent.MilitaryCommand(onset);
    var commandTravel = ThrottleDetent.CommandTravel(travel);
    var width = ThrottleDetent.TravelMeters / commandTravel;
    Check(mil < onset && onset - mil <= .000101f, "MIL shelf holds immediately below native AB onset");
    foreach (var shelfFraction in new[] { 0f, .01f, .5f, .99f, 1f })
    {
        var command = ThrottleDetent.Output(mil + width * shelfFraction, mil, width);
        // Native gauge chooses AB for >= onset; engine target is Clamp01 of
        // (command-onset)/(1-onset). Both must remain dry through the shelf.
        Check(command < onset && FlightControlMath.Clamp((command - onset) / (1 - onset), 0, 1) == 0,
            "Entire detent shelf stays outside native afterburner region and thrust ramp");
        var bindingRoundTrip = (FlightControlMath.ToSimulatedThrottle(command, true, false) + 1) * .5f;
        Check(bindingRoundTrip < onset, "Negative-axis binding conversion does not round the shelf back into AB");
        Check(Math.Abs(ThrottleDetent.Output(ThrottleDetent.Input(command, mil, width), mil, width) - command) < .000001f,
            "Release and regrab preserves protected MIL command");
    }
    Check(Math.Abs(ThrottleDetent.Output(travel / commandTravel, mil, width) - 1) < .000001f,
        "Protected detent retains full afterburner at end of travel");
    Check(ThrottleDetent.Output(mil + width + .001f, mil, width) > onset,
        "Moving through detent still enters afterburner");
}
var aircraft = new Aircraft
{
    engineStates = new object[] { new Turbojet { nozzles = new[] { new JetNozzle { afterburners = new[] { new Burner(.9f) } } } } },
    parameters = new() { HUDExtras = new() { gauges = new[] { new ThrottleGauge { throttleRegions = new[] { new GaugeRegion(.88f), new GaugeRegion(.9f) } } } } }
};
var shelf = MilitaryPower.Find(aircraft, out var nativeOnset);
Check(nativeOnset == .9f && shelf < .9f && shelf >= .88f, "Verified FS-20 native gauge remains in MIL region at detent");
aircraft.parameters.HUDExtras!.gauges[0].throttleRegions = new[] { new GaugeRegion(.8f), new GaugeRegion(.85f) };
shelf = MilitaryPower.Find(aircraft, out nativeOnset);
Check(nativeOnset == .85f && shelf < .85f, "An earlier aircraft-authored gauge boundary is respected");
aircraft.parameters.HUDExtras = null;
shelf = MilitaryPower.Find(aircraft, out nativeOnset);
Check(nativeOnset == .9f && shelf < .9f, "Missing HUD uses actual nozzle threshold safely");
aircraft.engineStates = new object[] { new Turbojet { nozzles = new[] { new JetNozzle { afterburners = new[] { new Burner(.9f), new Burner(.85f), new Burner(.3f, force:0), new Burner(.2f, force:float.PositiveInfinity) } } } } };
shelf = MilitaryPower.Find(aircraft, out nativeOnset);
Check(nativeOnset == .85f && shelf < .85f, "Earliest active burner wins; disabled/invalid burners are ignored");
aircraft.engineStates = Array.Empty<object>();
Check(MilitaryPower.Find(aircraft, out nativeOnset) == 1 && nativeOnset == 1, "Aircraft without afterburner gets no artificial MIL detent");
Console.WriteLine($"Throttle indicator and native MIL boundary checks passed: {assertions} assertions.");
