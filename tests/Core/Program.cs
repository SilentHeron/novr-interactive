using NOVR.MotionControls;

var passed = 0;
TexturePreparationChecks.Run(Check, True);
HudPresentationChecks.Run(Check, True);
CockpitAlertChecks.Run(Check, True);
StickLimitFeedbackChecks.Run(Check, True);
HandleReachFeedbackChecks.Run(Check, True);
ControlMirrorChecks.Run(Check, True);
ControlOutlineChecks.Run(Check, True);
FrameActionQueueChecks.Run(Check, True);
NativeControllerActionChecks.Run(Check, True);

Check("Menu history returns shared help to its source and clears confirmations on Home", () =>
{
    var h = new MenuHistory<string>("Home");
    h.Back();
    True(h.Current == "Home" && !h.CanBack);
    foreach (var parent in new[] { "Controls", "Bindings", "Comfort" })
    {
        h.Push(parent);
        h.Push("Guide");
        h.Back();
        True(h.Current == parent);
        h.Push("Confirm");
        h.Home();
        h.Back();
        True(h.Current == "Home" && !h.CanBack);
    }
    h.Push("Controls");
    h.Push("Bindings");
    h.Push("Guide");
    h.Push("Controls");
    True(h.Trail.SequenceEqual(new[] { "Home", "Controls" }));
    h.Push("Controls");
    True(h.Trail.Count == 2);
});
Check("Physical button touch needs an approach and rearms only after withdrawal", () =>
{
    var gate = new CockpitTouchGate();
    True(!gate.Update(true, true, 0));
    True(!gate.Update(true, true, 0));
    True(!gate.Update(true, true, .04f));
    True(gate.Update(true, true, .003f));
    True(!gate.Update(true, true, .002f));
    True(!gate.Update(true, true, .008f));
    True(!gate.Update(true, true, -.08f));
    True(!gate.Update(true, true, 0));
    True(!gate.Update(true, false, .04f));
    True(!gate.Update(true, false, 0));
    True(gate.Update(true, true, 0));
    gate.Update(false, true, .04f);
    True(!gate.Update(true, true, 0));
    gate.Update(true, true, .04f);
    gate.Update(false, true, 0);
    True(!gate.Update(true, true, 0));
});

Check("MIL shelf fits entirely inside the chosen throttle range and preserves regrabs", () =>
{
    foreach (var travel in new[] { .05f, .12f, .22f, .6f })
        foreach (var mil in new[] { .5f, .8f, .95f })
        {
            var commandTravel = ThrottleDetent.CommandTravel(travel);
            var width = ThrottleDetent.TravelMeters / commandTravel;
            Near(ThrottleDetent.Position(1, mil, travel) - ThrottleDetent.Position(0, mil, travel), 1);
            Near(ThrottleDetent.Output(travel / commandTravel, mil, width), 1);
            Near(width * commandTravel, .016f);
            var previous = -1f;
            for (var i = 0; i <= 100; i++)
            {
                var command = i / 100f;
                var fraction = ThrottleDetent.Position(command, mil, travel);
                True(fraction >= previous && fraction >= 0 && fraction <= 1.00001f);
                previous = fraction;
                Near(ThrottleDetent.Output(fraction * travel / commandTravel, mil, width), command);
                foreach (var delta in new[] { -.5f, -.01f, 0f, .01f, .5f })
                    Near(ThrottleDetent.Output(ThrottleDetent.Input(command, mil, width) + delta / commandTravel, mil, width),
                        ThrottleDetent.Output((fraction * travel + delta) / commandTravel, mil, width));
            }
        }
});
Check("Pointer ownership changes only on a fresh button edge", () =>
{
    var p = new PointerHandSelection();
    p.Update(true, 0, 0, 0, true, 0, 0, 0);
    True(!p.Left);
    for (var i = 0; i < 60; i++)
        p.Update(true, 0, 0, 0, true, 0, 0, 0);
    True(!p.Left);
    p.Update(true, 0, 0, 1, true, 0, 0, 0);
    True(p.Left);
    p.Update(true, 0, 0, 1, true, 0, 0, 2);
    True(!p.Left);
    p.Update(true, 0, 0, 1, true, 0, 0, 0);
    True(!p.Left);
    p.Update(true, 0, 0, 0, true, 0, 0, 0);
    p.Update(true, 0, 0, 1, true, 0, 0, 0);
    True(p.Left);
    p.Reset();
    p.Update(true, 0, 0, 0, true, 0, 0, 0);
    True(p.Left);
});
Check("Pointer drag owns its release frame and consumes ignored other-hand presses", () =>
{
    var p = new PointerHandSelection();
    p.Update(true, 0, 0, 0, true, 0, 0, 0);
    p.Update(true, 1, 0, 0, true, 0, 0, 0);
    True(p.Left && p.Trigger.Pressed);
    p.Update(true, 1, 0, 0, true, 1, 1, 1);
    True(p.Left && p.Trigger.Held);
    p.Update(true, 0, 0, 0, true, 1, 1, 2);
    True(p.Left && p.Trigger.Released);
    p.Update(true, 0, 0, 0, true, 1, 1, 2);
    True(p.Left && !p.Trigger.Held);
    p.Update(true, 0, 0, 0, true, 0, 0, 0);
    p.Update(true, 0, 0, 0, true, 1, 0, 0);
    True(!p.Left && p.Trigger.Pressed);
});
Check("Pointer tracking and focus loss require fresh input and retain the chosen hand", () =>
{
    var p = new PointerHandSelection();
    p.Update(true, 0, 0, 0, true, 0, 0, 0);
    p.Update(true, 0, 1, 0, true, 0, 0, 0);
    True(p.Left);
    p.Update(false, 0, 0, 0, true, 0, 0, 0);
    True(p.Left && !p.Trigger.Released);
    p.Update(false, 0, 0, 0, true, 0, 0, 1);
    True(!p.Left);
    p.Reset();
    p.Update(true, 1, 1, 127, true, 1, 1, 127);
    True(!p.Left && !p.Trigger.Held && !p.Trigger.Pressed);
    p.Update(true, 0, 0, 0, true, 0, 0, 127);
    p.Update(true, 1, 0, 0, true, 0, 0, 127);
    True(p.Left && p.Trigger.Pressed);
});
Check("Interaction cues are finite, brief, peak bounded and fade to silence", () =>
{
    foreach (var cue in Enum.GetValues<InteractionCue>())
    {
        var samples = InteractionSoundWave.Create(cue);
        True(samples.Length >= 1000 && samples.Length <= 3000);
        True(samples.All(x => float.IsFinite(x) && Math.Abs(x) <= 1));
        Near(samples[0], 0);
        Near(samples[^1], 0);
        True(samples.Any(x => Math.Abs(x) > .05f));
        True(samples.TakeLast(100).Max(x => Math.Abs(x)) < .01f);
    }
});

Check("Stick twist steers right and left with symmetric full travel", () =>
{
    foreach (var degrees in new[] { -60f, -30f, -15f, 0f, 15f, 30f, 60f })
    {
        var q = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, degrees * MathF.PI / 180f);
        Near(FlightControlMath.StickTwist(q.Y, q.W, 30, 0, 1), Math.Clamp(degrees / 30f, -1, 1));
        Near(FlightControlMath.StickTwist(-q.Y, -q.W, 30, 0, 1), Math.Clamp(degrees / 30f, -1, 1));
    }
});
Check("Pitch and roll swing do not introduce stick twist", () =>
{
    foreach (var axis in new[] { System.Numerics.Vector3.UnitX, System.Numerics.Vector3.UnitZ, System.Numerics.Vector3.Normalize(new(1, 0, 1)) })
    {
        var swing = System.Numerics.Quaternion.CreateFromAxisAngle(axis, .6f);
        Near(FlightControlMath.StickTwist(swing.Y, swing.W, 30, .06f, 1.5f), 0);
        var twist = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY, MathF.PI / 12);
        var combined = swing * twist;
        Near(FlightControlMath.StickTwist(combined.Y, combined.W, 30, 0, 1), .5f);
    }
});
Check("Twist respects deadzone and handles invalid or ambiguous rotations", () =>
{
    Near(FlightControlMath.StickTwist(MathF.Sin(MathF.PI / 360), MathF.Cos(MathF.PI / 360), 30, .06f, 1.5f), 0);
    Near(FlightControlMath.StickTwist(0, 0, 30, 0, 1), 0);
    Near(FlightControlMath.StickTwist(float.NaN, 1, 30, 0, 1), 0);
    Near(FlightControlMath.StickTwist(1, float.PositiveInfinity, 30, 0, 1), 0);
    Near(FlightControlMath.StickTwist(1, 1, 0, 0, 1), 0);
});
Check("Larger yaw deadzone rejects small wrist twist while preserving pitch and full steering", () =>
{
    foreach (var sign in new[] { -1f, 1f })
    {
        float Twist(float degrees, float deadzone)
        {
            var halfAngle = sign * degrees * MathF.PI / 360f;
            return FlightControlMath.StickTwist(MathF.Sin(halfAngle), MathF.Cos(halfAngle), 30, deadzone, 1.5f);
        }
        True(Math.Abs(Twist(4, .06f)) > 0);
        Near(Twist(4, .15f), 0);
        Near(Twist(4.5f, .15f), 0);
        True(sign * Twist(5, .15f) > 0);
        Near(Twist(30, .15f), sign);
        Near(Twist(40, .15f), sign);
        var pitch = FlightControlMath.Stick(0, MathF.Cos(4 * MathF.PI / 180f), sign * MathF.Sin(4 * MathF.PI / 180f), 25, .06f, 1.5f);
        True(sign * pitch.pitch > 0 && pitch.roll == 0);
    }
});
Check("VTOL-style throttle leaves target selection to the stick hand", () =>
{
    var layout = ThrottleBindingProfiles.Get(ThrottleBindingLayout.VtolStyle);
    True(layout.Brake == ControllerButton.Trigger && layout.Countermeasures == ControllerButton.Primary);
    True(layout.Target == ControllerButton.None && layout.NextCountermeasure == ControllerButton.Secondary);
    True(!ThrottleBindingProfiles.Conflicts(layout, true) && !ThrottleBindingProfiles.Conflicts(layout, false));
});
Check("Frame B unselects and Y cycles without changing Touch B weapon cycling", () =>
{
    True(ControllerButtonNames.Describe(ControllerButton.OutsideOrNone, false, true) == "B");
    True(ControllerButtonNames.Describe(ControllerButton.TopOrSecondary, false, true) == "Y");
    True(!ControllerButtonNames.SamePhysicalButton(ControllerButton.OutsideOrNone, ControllerButton.TopOrSecondary, true));
    True(ControllerButtonNames.Describe(ControllerButton.TopOrSecondary, false, false) == "B");
    True(ControllerButtonNames.Resolve(ControllerButton.OutsideOrNone, false) == ControllerButton.None);
    True(!ControllerButtonNames.SamePhysicalButton(ControllerButton.None, ControllerButton.OutsideOrNone, false));
    foreach (var frame in new[] { true, false })
    {
        True(!ControllerButtonNames.SamePhysicalButton(ControllerButton.TopOrSecondary, ControllerButton.Trigger, frame));
        True(!ControllerButtonNames.SamePhysicalButton(ControllerButton.TopOrSecondary, ControllerButton.Primary, frame));
    }
});
Check("Shared B cannot open the map while unselecting or carry a held press across grip release", () =>
{
    var map = new ControllerButtonGate();
    var unselect = new ControllerButtonGate();
    True(ControllerButtonNames.SamePhysicalButton(ControllerButton.OutsideOrSecondary, ControllerButton.OutsideOrNone, true));
    unselect.Update(true, 0);
    map.Update(false, 0);
    unselect.Update(true, 1);
    map.Update(false, 1);
    True(unselect.Pressed && !map.Pressed);
    unselect.Update(true, 0);
    map.Update(false, 0);
    True(unselect.Released && !map.Pressed);
    unselect.Update(true, 1);
    map.Update(false, 1);
    unselect.Update(false, 1);
    map.Update(true, 1);
    True(!unselect.Released && !map.Pressed);
    unselect.Update(false, 0);
    map.Update(true, 0);
    map.Update(true, 1);
    True(map.Pressed);
});
var payloadTestDir = Path.Combine(Path.GetTempPath(), "novr-payload-" + Guid.NewGuid());
Directory.CreateDirectory(payloadTestDir);
try
{
    var source = Path.Combine(payloadTestDir, "source.dll");
    var destination = Path.Combine(payloadTestDir, "installed.dll");
    var bytes = Enumerable.Range(0, 17003).Select(i => (byte)(i % 251)).ToArray();
    File.WriteAllBytes(source, bytes);
    Check("runtime payload installs missing files", () =>
    {
        True(PayloadFile.CopyIfChanged(source, destination));
        True(File.ReadAllBytes(destination).SequenceEqual(bytes));
    });
    Check("repeat installation leaves an identical locked assembly untouched", () =>
    {
        var stamp = File.GetLastWriteTimeUtc(destination);
        using var mappedAssembly = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
        True(!PayloadFile.CopyIfChanged(source, destination));
        True(File.GetLastWriteTimeUtc(destination) == stamp);
    });
    Check("runtime payload replaces a changed file even when sizes match", () =>
    {
        bytes[9000] ^= 0xff;
        File.WriteAllBytes(source, bytes);
        True(PayloadFile.CopyIfChanged(source, destination));
        True(File.ReadAllBytes(destination).SequenceEqual(bytes));
    });
}
finally { Directory.Delete(payloadTestDir, true); }
void Check(string name, Action test)
{
    test();
    Console.WriteLine($"PASS {name}");
    passed++;
}
void True(bool condition) { if (!condition) throw new Exception("Expected true"); }
void Near(float actual, float expected, float tolerance = 0.00001f)
{
    if (!float.IsFinite(actual) || Math.Abs(actual - expected) > tolerance)
        throw new Exception($"Expected {expected}, got {actual}");
}






Check("digital squeeze works when a runtime also exposes an idle or partial grip axis", () =>
{
    foreach (var analog in new[] { 0f, .2f, .5f, 1f, float.NaN, float.PositiveInfinity })
        True(FlightControlMath.GripValue(true, analog, true) == 1f);
    True(FlightControlMath.GripValue(false, 0, true) == 1f);
});
Check("analog squeeze is preserved and clamped without an intentional digital press", () =>
{
    foreach (var analog in new[] { 0f, .2f, .35f, .5f, .65f, 1f })
        True(FlightControlMath.GripValue(true, analog, false) == analog);
    True(FlightControlMath.GripValue(true, -1, false) == 0);
    True(FlightControlMath.GripValue(true, 2, false) == 1);
    True(FlightControlMath.GripValue(false, float.NaN, false) == 0);
});
Check("invalid analog squeeze without a digital press releases and disarms the grab", () =>
{
    foreach (var analog in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    {
        var latch = new GripLatch();
        latch.Update(true, 0, true);
        True(latch.Update(true, 1, true));
        True(!latch.Update(true, FlightControlMath.GripValue(true, analog, false), true));
        True(!latch.Update(true, 1, true));
    }
});
Check("digital squeeze requires a fresh edge and releases when pressure and click clear", () =>
{
    var latch = new GripLatch();
    True(!latch.Update(true, FlightControlMath.GripValue(true, .1f, true), true));
    True(!latch.Update(true, FlightControlMath.GripValue(true, .1f, false), true));
    True(latch.Update(true, FlightControlMath.GripValue(true, .1f, true), true));
    True(latch.Update(true, FlightControlMath.GripValue(true, .5f, false), true));
    True(!latch.Update(true, FlightControlMath.GripValue(true, .1f, false), true));
});

GripLatch Grab()
{
    var grip = new GripLatch();
    grip.Update(true, 0, true);
    True(grip.Update(true, 1, true));
    return grip;
}

Check("held grip at startup cannot grab until released", () =>
{
    var grip = new GripLatch();
    True(!grip.Update(true, 1, true));
    True(!grip.Update(true, 0, true));
    True(grip.Update(true, 1, true));
});
Check("pressing away then moving into a handle does not grab", () =>
{
    var grip = new GripLatch();
    grip.Update(true, 0, false);
    True(!grip.Update(true, 1, false));
    True(!grip.Update(true, 1, true));
    grip.Update(true, 0, true);
    True(grip.Update(true, 1, true));
});
Check("grip hysteresis avoids repeated grab/release at threshold", () =>
{
    var grip = Grab();
    True(grip.Update(true, 0.5f, true));
    True(!grip.Update(true, 0.35f, true));
    True(!grip.Update(true, 0.5f, true));
    True(grip.Update(true, 0.65f, true));
});
Check("moving beyond grab radius while holding retains control", () =>
    True(Grab().Update(true, 1, false)));
Check("tracking loss releases and requires a new grip edge", () =>
{
    var grip = Grab();
    True(!grip.Update(false, 1, true));
    True(!grip.Update(true, 1, true));
    grip.Update(true, 0, true);
    True(grip.Update(true, 1, true));
});
Check("pause/recenter reset cannot automatically regrab", () =>
{
    var grip = Grab();
    grip.Reset();
    True(!grip.Update(true, 1, true));
});
Check("non-finite grip input releases and disarms", () =>
{
    foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    {
        var grip = Grab();
        True(!grip.Update(true, value, true));
        True(!grip.Update(true, 1, true));
    }
});
Check("stick and throttle release independently", () =>
{
    var stick = Grab();
    var throttle = Grab();
    stick.Update(false, 1, false);
    True(!stick.Held && throttle.Held);
});
FlightGrip TapGrab()
{
    var grip = new FlightGrip();
    grip.Update(true, 0, true, true, 0);
    True(grip.Update(true, 1, true, true, .1f));
    True(grip.Update(true, 0, true, true, .2f));
    True(grip.Latched);
    return grip;
}
Check("quick flight grip tap latches without continuous pressure", () =>
    True(TapGrab().Update(true, 0, true, true, 1)));
Check("toggle grip stays latched after a long squeeze", () =>
{
    var grip = new FlightGrip();
    grip.Update(true, 0, true, true, 0);
    grip.Update(true, 1, true, true, .1f);
    True(grip.Update(true, 0, true, true, .5f));
    True(grip.Latched);
});
Check("a fresh press releases a latched handle without regrabbing", () =>
{
    var grip = TapGrab();
    True(!grip.Update(true, 1, true, true, 1));
    True(!grip.Update(true, 1, true, true, 1));
    True(!grip.Update(true, 0, true, true, 1.1f));
    True(grip.Update(true, 1, true, true, 1.2f));
});
Check("tap grip cannot grab on startup or after pressing outside reach", () =>
{
    var grip = new FlightGrip();
    True(!grip.Update(true, 1, true, true, 0));
    grip.Update(true, 0, false, true, .1f);
    True(!grip.Update(true, 1, false, true, .2f));
    True(!grip.Update(true, 1, true, true, .3f));
    True(!grip.Update(true, 0, true, true, .4f));
});
Check("latched flight grip cancels on tracking loss and requires a fresh edge", () =>
{
    var grip = TapGrab();
    True(!grip.Update(false, 1, true, true, 1));
    True(!grip.Update(true, 1, true, true, 1.1f));
    grip.Update(true, 0, true, true, 1.2f);
    True(grip.Update(true, 1, true, true, 1.3f));
});
Check("flight grip breakaway releases and disarms the held input", () =>
{
    var grip = TapGrab();
    True(!grip.Update(true, 1, false, true, 1, false));
    True(!grip.Update(true, 1, true, true, 1.1f));
});
Check("changing flight grip mode does not silently keep the handle latched", () =>
{
    var grip = TapGrab();
    True(!grip.Update(true, 1, true, false, 1));
    True(!grip.Latched);
});
Check("ordinary hold mode retains control until release", () =>
{
    var grip = new FlightGrip();
    grip.Update(true, 0, true, false, 0);
    True(grip.Update(true, 1, true, false, .1f));
    True(grip.Update(true, 1, false, false, .2f, false));
    True(!grip.Update(true, 0, true, false, .25f));
});
Check("invalid flight grip samples and clock rollback cancel a latch", () =>
{
    True(!TapGrab().Update(true, float.NaN, true, true, 1));
    True(!TapGrab().Update(true, 0, true, true, float.NaN));
    True(!TapGrab().Update(true, 0, true, true, .1f));
});
Check("paused or recentered flight handles cannot resume their old latch", () =>
{
    var grip = TapGrab();
    grip.Reset();
    True(!grip.Update(true, 1, true, true, 1));
});
Check("controller help follows Frame hand and physical button labels", () =>
{
    True(ControllerButtonNames.Describe(ControllerButton.Secondary, true, true) == "D-pad right");
    True(ControllerButtonNames.Describe(ControllerButton.Secondary, false, true) == "X");
    True(ControllerButtonNames.Describe(ControllerButton.Menu, true, true) == "View");
    True(ControllerButtonNames.Describe(ControllerButton.OutsideOrSecondary, false, true) == "B");
});
Check("controller help resolves fallback bindings and unavailable controls", () =>
{
    True(ControllerButtonNames.Describe(ControllerButton.ShoulderOrPrimary, true, false) == "X");
    True(ControllerButtonNames.Describe(ControllerButton.OutsideOrSecondary, false, false) == "B");
    True(ControllerButtonNames.Describe(ControllerButton.Top, false, false) == "Unavailable");
    True(ControllerButtonNames.Describe(ControllerButton.None, false, true) == "Unassigned");
});
Check("both throttle presets have distinct buttons on Frame and Touch profiles", () =>
{
    foreach (var layout in new[] { ThrottleBindingLayout.CountermeasureTrigger, ThrottleBindingLayout.BrakeTrigger, ThrottleBindingLayout.VtolStyle })
        foreach (var frame in new[] { true, false })
        {
            var preset = ThrottleBindingProfiles.Get(layout);
            True(!ThrottleBindingProfiles.Conflicts(preset, frame));
            True(ThrottleBindingProfiles.Identify(preset) == layout);
        }
});
Check("default brake is D-pad down on Frame and thumbstick click on Touch", () =>
{
    var button = ThrottleBindingProfiles.Get(ThrottleBindingLayout.CountermeasureTrigger).Brake;
    True(ControllerButtonNames.Describe(button, true, true) == "D-pad down");
    True(ControllerButtonNames.Describe(button, true, false) == "Thumbstick click");
});
Check("trigger brake preset separates braking from countermeasure deployment", () =>
{
    var buttons = ThrottleBindingProfiles.Get(ThrottleBindingLayout.BrakeTrigger);
    True(buttons.Brake == ControllerButton.Trigger);
    True(ControllerButtonNames.Resolve(buttons.Countermeasures, true) == ControllerButton.Shoulder);
    True(ControllerButtonNames.Resolve(buttons.Countermeasures, false) == ControllerButton.Primary);
    True(ControllerButtonNames.Resolve(buttons.Target, false) == ControllerButton.Secondary);
});
Check("custom throttle conflicts are detected without treating unassigned as a button", () =>
{
    var custom = new ThrottleButtons(ControllerButton.Trigger, ControllerButton.Trigger, ControllerButton.None, ControllerButton.None);
    True(ThrottleBindingProfiles.Identify(custom) == ThrottleBindingLayout.Custom);
    True(ThrottleBindingProfiles.Conflicts(custom, true));
    True(!ThrottleBindingProfiles.SharesButton(ControllerButton.None, false, ControllerButton.None));
    True(ThrottleBindingProfiles.SharesButton(ControllerButton.PrimaryOrStickClick, true, ControllerButton.Primary));
    True(ThrottleBindingProfiles.SharesButton(ControllerButton.PrimaryOrStickClick, false, ControllerButton.StickClick));
});
Check("deadzone suppresses small movements", () =>
{
    Near(FlightControlMath.Axis(0.05f, 0.06f, 1.5f), 0);
    Near(FlightControlMath.Axis(-0.05f, 0.06f, 1.5f), 0);
});
Check("axis range clamps symmetrically", () =>
{
    Near(FlightControlMath.Axis(5, 0.06f, 1.5f), 1);
    Near(FlightControlMath.Axis(-5, 0.06f, 1.5f), -1);
});
Check("response curve is bounded, symmetric and monotonic", () =>
{
    float previous = 0;
    for (int i = 0; i <= 1000; i++)
    {
        var input = i / 1000f;
        var output = FlightControlMath.Axis(input, 0.06f, 1.5f);
        True(output >= previous && output <= 1);
        Near(output, -FlightControlMath.Axis(-input, 0.06f, 1.5f));
        previous = output;
    }
});
Check("axis rejects invalid tracking values", () =>
{
    Near(FlightControlMath.Axis(float.NaN, 0.06f, 1.5f), 0);
    Near(FlightControlMath.Axis(float.PositiveInfinity, 0.06f, 1.5f), 0);
    True(float.IsFinite(FlightControlMath.Axis(0.5f, float.NaN, float.NaN)));
});
Check("higher response exponent reduces center sensitivity", () =>
    True(FlightControlMath.Axis(0.5f, 0.06f, 2) < FlightControlMath.Axis(0.5f, 0.06f, 1)));
Check("pulling the stick commands nose up, pushing commands nose down", () =>
{
    var sine = (float)Math.Sin(25 * Math.PI / 180);
    var cosine = (float)Math.Cos(25 * Math.PI / 180);
    // The game's fly-by-wire target angular velocity and cockpit stick animation
    // both use pitch directly on local X: negative is nose-up, positive nose-down.
    Near(FlightControlMath.Stick(0, cosine, -sine, 25, 0.06f, 1.5f).pitch, -1);
    Near(FlightControlMath.Stick(0, cosine, sine, 25, 0.06f, 1.5f).pitch, 1);
});
Check("diagonal stick movement keeps pitch and roll directions independent", () =>
{
    var tilt = (float)Math.Tan(12.5 * Math.PI / 180);
    var upRight = FlightControlMath.Stick(tilt, 1, -tilt, 25, 0.06f, 1.5f);
    var downLeft = FlightControlMath.Stick(-tilt, 1, tilt, 25, 0.06f, 1.5f);
    True(upRight.pitch < 0 && upRight.roll > 0);
    Near(upRight.pitch, -upRight.roll);
    Near(downLeft.pitch, -upRight.pitch);
    Near(downLeft.roll, -upRight.roll);
});
Check("leaning stick right commands right roll without pitch", () =>
{
    var axes = FlightControlMath.Stick((float)Math.Sin(25 * Math.PI / 180),
        (float)Math.Cos(25 * Math.PI / 180), 0, 25, 0.06f, 1.5f);
    Near(axes.roll, 1);
    Near(axes.pitch, 0);
});
Check("neutral or invalid stick poses cannot deflect controls", () =>
{
    var neutral = FlightControlMath.Stick(0, 1, 0, 25, 0.06f, 1.5f);
    Near(neutral.pitch, 0);
    Near(neutral.roll, 0);
    var invalid = FlightControlMath.Stick(float.NaN, 1, 0, 25, 0.06f, 1.5f);
    Near(invalid.pitch, 0);
    Near(invalid.roll, 0);
});
Check("grabbing throttle preserves current thrust", () =>
    Near(FlightControlMath.Throttle(0.73f, 1.2f, 1.2f, 0.25f), 0.73f));
Check("collective has a rigid arm and monotonic lift through the full configured travel", () =>
{
    foreach (var travel in new[] { .05f, .25f, .6f })
    {
        var length = FlightControlMath.CollectiveArmLength(travel);
        var lastHeight = float.NegativeInfinity;
        for (var i = 0; i <= 100; i++)
        {
            var p = FlightControlMath.ThrottleOffset(true, i / 100f, travel);
            True(p.height > lastHeight);
            var fromPivotHeight = p.height + FlightControlMath.CollectiveGripHeight;
            var fromPivotForward = p.forward + FlightControlMath.CollectiveNeutralForward(travel);
            Near(fromPivotHeight * fromPivotHeight + fromPivotForward * fromPivotForward, length * length);
            Near(FlightControlMath.Throttle(0, -travel * .5f, p.height, travel), i / 100f);
            lastHeight = p.height;
        }
        Near(FlightControlMath.ThrottleOffset(true, 1, travel).height -
             FlightControlMath.ThrottleOffset(true, 0, travel).height, travel);
        Near(FlightControlMath.ThrottleOffset(true, .5f, travel).forward, 0);
        // The visible native hinge and every command share one fixed circle;
        // retain the actual grip dimensions instead of shrinking the whole lever.
        Near(FlightControlMath.CollectiveNeutralForward(travel), FlightControlMath.CollectiveGripForward);
    }
});
Check("collective travel stays finite at command limits", () =>
{
    foreach (var travel in new[] { .05f, .25f, .6f })
    {
        Near(FlightControlMath.ThrottleOffset(true, -1, travel).height, -travel * .5f);
        Near(FlightControlMath.ThrottleOffset(true, 2, travel).height, travel * .5f);
    }
});
Check("jet throttle retains its straight forward path at every command", () =>
{
    foreach (var travel in new[] { .05f, .25f, .6f })
        for (var i = 0; i <= 100; i++)
        {
            var p = FlightControlMath.ThrottleOffset(false, i / 100f, travel);
            Near(p.height, 0);
            Near(p.forward, (i / 100f - .5f) * travel);
        }
});
Check("forward throttle movement increases thrust proportionately", () =>
    Near(FlightControlMath.Throttle(0.3f, 0.2f, 0.3f, 0.25f), 0.7f));
Check("throttle clamps at both physical travel limits", () =>
{
    Near(FlightControlMath.Throttle(0.5f, 0, 10, 0.25f), 1);
    Near(FlightControlMath.Throttle(0.5f, 0, -10, 0.25f), 0);
});
Check("regrabbing throttle at another location causes no jump", () =>
{
    var first = FlightControlMath.Throttle(0.3f, 0.2f, 0.3f, 0.25f);
    Near(FlightControlMath.Throttle(first, -0.3f, -0.3f, 0.25f), first);
});
Check("invalid throttle samples or zero travel retain thrust", () =>
{
    Near(FlightControlMath.Throttle(0.6f, 0, float.NaN, 0.25f), 0.6f);
    Near(FlightControlMath.Throttle(0.6f, 0, 1, 0), 0.6f);
    Near(FlightControlMath.Throttle(0.6f, 0, 1, float.PositiveInfinity), 0.6f);
});
Check("native throttle handoff preserves every binding/collective convention", () =>
{
    foreach (var negativeRange in new[] { false, true })
        foreach (var invertedCollective in new[] { false, true })
            for (int i = 0; i <= 100; i++)
            {
                var thrust = i / 100f;
                var native = FlightControlMath.ToSimulatedThrottle(thrust, negativeRange, invertedCollective);
                var restored = negativeRange ? 0.5f * (native + 1f) : native;
                if (invertedCollective)
                    restored = 1f - restored;
                Near(restored, thrust);
            }
});
Check("HUD smoothing follows identically at 72, 90, and 120 Hz", () =>
{
    float Simulate(int frames)
    {
        float remaining = 1;
        for (int i = 0; i < frames; i++)
            remaining *= 1 - FlightControlMath.SmoothingWeight(1f / frames, 10);
        return remaining;
    }
    Near(Simulate(72), Simulate(90));
    Near(Simulate(90), Simulate(120));
    Near(Simulate(90), (float)Math.Exp(-10));
});
Check("HUD smoothing remains bounded after a frame stall", () =>
{
    var weight = FlightControlMath.SmoothingWeight(5, 10);
    True(weight >= 0 && weight <= 1);
    Near(FlightControlMath.SmoothingWeight(0, 10), 0);
});

Check("a held flight trigger cannot turn into a menu click", () =>
{
    var flight = new ControllerButtonGate();
    var menu = new ControllerButtonGate();
    flight.Update(true, 0);
    flight.Update(true, 1);
    True(flight.Held && flight.Pressed);
    flight.Update(false, 1);
    menu.Update(true, 1);
    True(!flight.Held && !flight.Released && !menu.Pressed && !menu.Held);
    menu.Update(true, 0);
    menu.Update(true, 1);
    True(menu.Pressed);
});
Check("closing a menu with trigger held cannot start firing", () =>
{
    var flight = new ControllerButtonGate();
    flight.Update(false, 1);
    flight.Update(true, 1);
    True(!flight.Held && !flight.Pressed);
    flight.Update(true, 0);
    flight.Update(true, 1);
    True(flight.Held && flight.Pressed);
});
Check("tracking loss cancels release actions instead of clicking", () =>
{
    var button = new ControllerButtonGate();
    button.Update(true, 0);
    button.Update(true, 1);
    button.Update(false, 0);
    True(!button.Released && !button.Held);
    button.Update(true, 1);
    True(!button.Pressed);
});
Check("non-finite trigger data never produces a release action", () =>
{
    var button = new ControllerButtonGate();
    button.Update(true, 0);
    button.Update(true, 1);
    button.Update(true, float.NaN);
    True(!button.Released && !button.Held);
});
Check("controller taps create exactly one press and one release", () =>
{
    var button = new ControllerButtonGate();
    button.Update(true, 0);
    button.Update(true, 1);
    True(button.Pressed);
    button.Update(true, 1);
    True(button.Held && !button.Pressed && !button.Released);
    button.Update(true, 0);
    True(button.Released && !button.Pressed);
    button.Update(true, 0);
    True(!button.Released);
});
Check("hand-origin ray hits the surface without centre-eye parallax", () =>
{
    True(PointerGeometry.IntersectRect(0.3f, 0, -2, -0.1f, 0, 1, -0.2f, -0.2f, 0.2f, 0.2f, out var distance));
    Near(distance, 2);
    Near(0.3f - 0.1f * distance, 0.1f);
    // A centre-eye ray with the same direction would point at a different location.
    True(Math.Abs(-0.1f * distance - 0.1f) > 0.2f);
});
Check("surface distances survive scaled canvas coordinates", () =>
{
    True(PointerGeometry.IntersectRect(300, 0, -2000, -100, 0, 1000, -200, -200, 200, 200, out var distance));
    Near(distance, 2);
});
Check("parallel, rearward, non-finite and off-panel rays do not click", () =>
{
    True(!PointerGeometry.IntersectRect(0, 0, -2, 1, 0, 0, -1, -1, 1, 1, out _));
    True(!PointerGeometry.IntersectRect(0, 0, 2, 0, 0, 1, -1, -1, 1, 1, out _));
    True(!PointerGeometry.IntersectRect(float.NaN, 0, -2, 0, 0, 1, -1, -1, 1, 1, out _));
    True(!PointerGeometry.IntersectRect(5, 0, -2, 0, 0, 1, -1, -1, 1, 1, out _));
});
Check("distant HUD symbology is excluded from menu hit testing", () =>
    True(!PointerGeometry.IntersectRect(0, 0, -1000, 0, 0, 1, -100, -100, 100, 100, out _)));
Check("map zoom rate is equal at 72, 90, 120 and 144 Hz", () =>
{
    float Simulate(int frames)
    {
        float zoom = 2;
        for (int i = 0; i < frames; i++)
            zoom = PointerGeometry.Zoom(zoom, 0.6f, 1f / frames);
        return zoom;
    }
    Near(Simulate(72), Simulate(90), 0.0001f);
    Near(Simulate(90), Simulate(120), 0.0001f);
    Near(Simulate(120), Simulate(144), 0.0001f);
});
Check("map zoom ignores invalid input and stays inside game limits", () =>
{
    Near(PointerGeometry.Zoom(2, float.NaN, 1), 2);
    Near(PointerGeometry.Zoom(2, 1, -1), 2);
    Near(PointerGeometry.Zoom(40, 1, 1), 40);
    Near(PointerGeometry.Zoom(1, -1, 1), 1);
    True(PointerGeometry.Zoom(2, 1, 50) < 2.2f);
});

Check("helmet gesture is limited to the brow in head-relative meters", () =>
{
    True(HelmetGestureMath.AtBrow(.12f, .07f, .14f));
    True(HelmetGestureMath.AtBrow(-.12f, .07f, .14f));
    True(!HelmetGestureMath.AtBrow(0, -.3f, .14f));
    True(!HelmetGestureMath.AtBrow(0, .07f, -.14f));
    True(!HelmetGestureMath.AtBrow(.4f, .07f, .14f));
    True(!HelmetGestureMath.AtBrow(float.NaN, 0, 0));
});
Check("helmet trigger requires release after arrival and tracking recovery", () =>
{
    var gate = new ControllerButtonGate();
    gate.Update(false, 1);
    gate.Update(true, 1);
    True(!gate.Pressed);
    gate.Update(true, 0);
    gate.Update(true, 1);
    True(gate.Pressed);
    gate.Update(true, 1);
    True(!gate.Pressed);
    gate.Update(false, 1);
    gate.Update(true, 1);
    True(!gate.Pressed);
});

Check("MIL detent preserves every regrab command and reaches idle/full afterburner", () =>
{
    foreach (var mil in new[] { .8f, .9f, .998f })
        foreach (var width in new[] { .016f / .05f, .016f / .25f, .016f / .6f })
        {
            for (var i = 0; i <= 100; i++)
                Near(ThrottleDetent.Output(ThrottleDetent.Input(i / 100f, mil, width), mil, width), i / 100f);
            Near(ThrottleDetent.Output(mil + width * .01f, mil, width), mil);
            Near(ThrottleDetent.Output(mil + width * .99f, mil, width), mil);
            True(ThrottleDetent.Output(mil + width + .0001f, mil, width) > mil);
            True(ThrottleDetent.Output(mil - .0001f, mil, width) < mil);
            Near(ThrottleDetent.Output(-1, mil, width), 0);
            Near(ThrottleDetent.Output(2, mil, width), 1);
        }
});















if (args.Length == 2)
{

    Check("Harmony patch targets and injected fields match the installed game", () =>
        PatchCompatibility.Verify(args[0], args[1]));
    Check("Proton input workaround matches Rewired pre-initialization hooks and public configuration", () =>
        PatchCompatibility.VerifyInputBackend(args[1]));
}

if (args.Length >= 1)
{
    using var asm = Mono.Cecil.AssemblyDefinition.ReadAssembly(args[0]);
    foreach (var side in new[] { "left", "right" })
        Check("Embedded " + side + " glove retains valid skin, poses and a bounded geometry budget", () =>
        {
            var resource = asm.MainModule.Resources.OfType<Mono.Cecil.EmbeddedResource>().Single(r => r.Name == "NOVR.Glove." + side + ".json");
            using var reader = new StreamReader(resource.GetResourceStream());
            var a = Newtonsoft.Json.Linq.JObject.Parse(reader.ReadToEnd());
            var vertices = a["vertices"]!.ToObject<float[]>()!;
            var weights = a["weights"]!.ToObject<float[]>()!;
            var indices = a["indices"]!.ToObject<int[]>()!;
            var triangles = a["triangles"]!.ToObject<int[]>()!;
            var bones = a["bones"]!;
            True(bones.Count() == 31 && vertices.Length / 3 == 3028 && triangles.Length / 3 == 4899);
            True(vertices.All(float.IsFinite) && weights.Length == vertices.Length / 3 * 4 && indices.Length == weights.Length);
            True(triangles.All(i => i >= 0 && i < vertices.Length / 3) && indices.All(i => i >= 0 && i < 31));
            for (var i = 0; i < weights.Length; i += 4)
                Near(weights[i] + weights[i + 1] + weights[i + 2] + weights[i + 3], 1, .0001f);
            for (var i = 0; i < 31; i++)
            {
                True((int)bones[i]!["parent"]! < i);
                foreach (var pose in new[] { "open", "fist" })
                {
                    var q = a[pose]![i]!.ToObject<float[]>()!;
                    True(q.Length == 4 && q.All(float.IsFinite));
                    Near(q.Sum(x => x * x), 1, .001f);
                }
            }
        });
}
Console.WriteLine($"{passed} checks passed.");
