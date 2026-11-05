using System.Reflection;
using NOVR.MotionControls;
using NOVR.VrUi.SpecialBehavior;

var passed = 0;
void Check(bool condition, string reason)
{
    if (!condition)
        throw new Exception(reason);
    passed++;
}
var postfix = typeof(PilotMotionInputPatch).GetNestedType("AuxiliaryThumbstick", BindingFlags.NonPublic)!
    .GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic)!;
float Dispatch(Rewired.Player player, string action, float native)
{
    object[] args = { player, action, native };
    postfix.Invoke(null, args);
    return (float)args[2];
}
MotionFlightController Create(params object[] systems)
{
    CockpitMapInteraction.ReserveLeft = CockpitMapInteraction.ReserveRight = false;
    var motion = new MotionFlightController();
    motion.Configure(new Aircraft { Components = systems });
    MotionFlightController.Instance = motion;
    return motion;
}

// No aircraft-name/capability whitelist: all native/custom consumers receive
// relative input. The native game retains position/automation ownership.
foreach (var systems in new[] { Array.Empty<object>(), new object[] { new DuctedFan() }, new object[] { new CustomAircraftSystem() } })
{
    var motion = Create(systems);
    Check(motion.AuxiliaryAxis == 1, "Any aircraft may receive Custom Axis 1 while its throttle is held.");
    Check(Dispatch(GameManager.playerInput, "Custom Axis 1", .1f) == 1, "The actual postfix supplies the positive relative command.");
    motion.SetHand(-.8f);
    Check(Dispatch(GameManager.playerInput, "Custom Axis 1", .1f) == -1, "The actual postfix supplies the negative relative command.");
    motion.SetHand(0);
    Check(Dispatch(GameManager.playerInput, "Custom Axis 1", .43f) == .43f, "Returning to center preserves native/hardware input rather than forcing an absolute center.");
}

foreach (var wing in new SwingWingController[] { new SwingWingController(), new ModdedSwingWing() })
{
    var motion = Create(wing);
    Check(motion.AuxiliaryAxis == -1, "Forward thumbstick extends wings, including aircraft using a derived sweep component.");
    Check(Dispatch(GameManager.playerInput, "Custom Axis 1", .1f) == -1, "Wing sweep inversion reaches the native command postfix.");
    motion.SetHand(-.8f);
    Check(Dispatch(GameManager.playerInput, "Custom Axis 1", .1f) == 1, "Backward thumbstick sweeps wings aft.");
    motion.SetHand(0);
    Check(Dispatch(GameManager.playerInput, "Custom Axis 1", .43f) == .43f, "Centered wing-sweep thumbstick preserves hardware input.");
    foreach (var leftHanded in new[] { false, true })
    {
        motion.SetLeftHanded(leftHanded);
        motion.SetHand(.8f);
        CockpitMapInteraction.ReserveLeft = !leftHanded;
        CockpitMapInteraction.ReserveRight = leftHanded;
        Check(motion.AuxiliaryAxis == 0, "Map ownership blocks wing sweep on either role-relative throttle hand.");
        CockpitMapInteraction.ReserveLeft = leftHanded;
        CockpitMapInteraction.ReserveRight = !leftHanded;
        Check(motion.AuxiliaryAxis == -1, "Wing sweep remains inverted when only the other hand is reserved.");
    }
    CockpitMapInteraction.ReserveLeft = CockpitMapInteraction.ReserveRight = false;
    motion.Configure(new Aircraft { Components = new object[] { new DuctedFan() } });
    Check(motion.AuxiliaryAxis == 1, "Switching from swept wings to pusher fans clears inversion.");
}

foreach (var nozzle in new object[] { new TiltWingController(), new DuctedThrustSystem(), new SwivelDuctSystem(), new ModdedTiltWing() })
{
    var motion = Create(nozzle);
    var aircraft = new Aircraft { Components = new[] { nozzle }, Hover = true };
    motion.Configure(aircraft);
    Check(motion.AuxiliaryAxis == 0, "Existing nozzle/tiltwing auto-hover protection remains.");
    if (nozzle is TiltWingController tilt)
        Check(tilt.Automatic, "Blocked input cannot turn off automatic tilt.");
    aircraft.Hover = false;
    Check(motion.AuxiliaryAxis == 1, "Nozzle command resumes outside auto-hover.");
    if (nozzle is TiltWingController manual)
        Check(!manual.Automatic, "Deliberate tiltwing movement retains the existing manual takeover.");
    motion.SetHand(-.8f);
    Check(motion.AuxiliaryAxis == -1, "Nozzle and tiltwing negative commands retain their direction.");
    Check(aircraft.FlightAssist, "Auxiliary input never changes whole-aircraft flight assist.");
}

foreach (var system in new object[] { new DuctedFan(), new SwingWingController(), new CustomAircraftSystem() })
{
    var motion = Create(system);
    var aircraft = new Aircraft { Components = new[] { system }, Hover = true };
    motion.Configure(aircraft);
    var expected = system is SwingWingController ? -1f : 1f;
    Check(motion.AuxiliaryAxis == expected && aircraft.FlightAssist, "Non-nozzle auxiliary controls use native semantics rather than an unrelated nozzle hover restriction.");
}

var current = Create(new TiltWingController());
current.Configure(new Aircraft { Components = new object[] { new DuctedFan() }, Hover = true });
Check(current.AuxiliaryAxis == 1, "Aircraft replacement clears old nozzle restrictions.");
foreach (var leftHanded in new[] { false, true })
{
    current.SetLeftHanded(leftHanded);
    CockpitMapInteraction.ReserveLeft = !leftHanded;
    CockpitMapInteraction.ReserveRight = leftHanded;
    Check(current.AuxiliaryAxis == 0, "Map ownership blocks the role-relative throttle thumbstick.");
    CockpitMapInteraction.ReserveLeft = leftHanded;
    CockpitMapInteraction.ReserveRight = !leftHanded;
    Check(current.AuxiliaryAxis == 1, "Map ownership of the other hand cannot block the throttle thumbstick.");
}
CockpitMapInteraction.ReserveLeft = CockpitMapInteraction.ReserveRight = false;

foreach (var value in new[] { 0f, .3499f, -.3499f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    current.SetHand(value);
    Check(current.AuxiliaryAxis == 0, "Neutral/invalid axes cannot issue a command.");
}
foreach (var value in new[] { .35f, -.35f, .7f, -.7f, 1f, -1f })
{
    current.SetHand(value);
    Check(current.AuxiliaryAxis == MathF.Sign(value), "A deliberate deflection supplies a relative step, preserving the native deadzone boundary.");
}
current.SetHand(.8f, valid: false);
Check(current.AuxiliaryAxis == 0, "Tracking loss blocks commands.");
current.SetHand(.8f, hasAxis: false);
Check(current.AuxiliaryAxis == 0, "Missing thumbstick capability blocks commands.");
current.SetHand(.8f);
current.ThrottleHeld = false;
Check(current.AuxiliaryAxis == 0, "Free-hand wheel use cannot move the aircraft auxiliary control.");
current.ThrottleHeld = true;
current.FlightActive = false;
Check(current.AuxiliaryAxis == 0, "Menus/layout/inactive cockpit context blocks commands.");
current.FlightActive = true;

Check(Dispatch(new Rewired.Player(), "Custom Axis 1", -.4f) == -.4f, "Other Rewired players are untouched.");
foreach (var name in new[] { "Throttle", "Pitch", "Custom Axis 2", "custom axis 1", "" })
    Check(Dispatch(GameManager.playerInput, name, -.4f) == -.4f, "Unrelated native actions are untouched.");
MotionFlightController.Instance = null;
Check(Dispatch(GameManager.playerInput, "Custom Axis 1", -.4f) == -.4f, "Disabled/missing VR controller preserves native input.");

Console.WriteLine($"PASS {passed} source-linked auxiliary-axis checks (controller/game boundaries doubled; no native physics/headset).");
