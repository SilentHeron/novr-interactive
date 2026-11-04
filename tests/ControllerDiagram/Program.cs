using NOVR.MotionControls;

var checks = 0;
foreach (var profile in new[] { ControllerBindingProfile.Frame, ControllerBindingProfile.Touch })
foreach (var stickLeft in new[] { false, true })
{
    var bindings = ControllerBindingDefaults.Actions.ToDictionary(a => a, a => ControllerBindingDefaults.Get(profile, a));
    foreach (var left in new[] { false, true })
    foreach (var button in ControllerBindingDefaults.AvailableButtons(profile, left).Where(b => b != ControllerButton.None))
    {
        var actions = ControllerDiagramModel.Assigned(left, button, profile, stickLeft, a => bindings[a]);
        Check(actions.Distinct().Count() == actions.Length, "A physical diagram location never duplicates an action");
        foreach (var action in actions)
        {
            Check(ControllerBindingDefaults.ResolveLeft(bindings[action].Hand, stickLeft) == left,
                "Diagram displays each assignment on its actual physical hand");
            Check(ControllerButtonNames.SamePhysicalButton(bindings[action].Button, button, profile == ControllerBindingProfile.Frame),
                "Diagram resolves legacy aliases through the active physical profile");
            var proposed = ControllerDiagramModel.Proposed(bindings[action], left, button, stickLeft);
            Check(proposed.Hand == bindings[action].Hand && proposed.Button == button, "Editing an existing physical assignment retains its handle-relative role");
        }
    }
    var fire = bindings[ControllerActionId.WeaponRelease];
    var fireLeft = ControllerBindingDefaults.ResolveLeft(fire.Hand, stickLeft);
    Check(ControllerDiagramModel.Assigned(fireLeft, ControllerButton.Primary, profile, stickLeft, a => bindings[a]).Contains(ControllerActionId.WeaponRelease),
        "Missile release follows the stick hand in both handedness modes");
    var crossHand = ControllerDiagramModel.Proposed(fire, !fireLeft, ControllerButton.Secondary, stickLeft);
    Check(crossHand.Hand == (!fireLeft ? ControllerHandRole.Left : ControllerHandRole.Right), "Cross-hand diagram change explicitly chooses the clicked physical hand");
    Check(ControllerBindingDefaults.ResolveLeft(crossHand.Hand, stickLeft) == !fireLeft, "Cross-hand proposal resolves to the selected controller");
    var before = bindings[ControllerActionId.WeaponRelease];
    Check(before.Hand == fire.Hand && before.Button == fire.Button, "Reading/proposing from the diagram cannot mutate saved bindings");
    var view = ControllerDiagramModel.Assigned(true, ControllerButton.Menu, profile, stickLeft, a => bindings[a]);
    Check(view.Contains(ControllerActionId.WristMenu), "Physical-left menu remains View regardless of stick handedness");
    if (profile == ControllerBindingProfile.Touch)
        foreach (var unavailable in new[] { ControllerButton.Menu, ControllerButton.Shoulder, ControllerButton.Top, ControllerButton.Outside })
            Check(ControllerDiagramModel.Assigned(false, unavailable, profile, stickLeft, a => bindings[a]).Length == 0,
                "Touch diagram never exposes system/unsupported inputs as assignments");
}
var custom = ControllerBindingDefaults.Actions.ToDictionary(a => a, _ => new ControllerActionBinding(ControllerButton.None, ControllerHandRole.Left));
custom[ControllerActionId.Map] = new(ControllerButton.Secondary, ControllerHandRole.Right);
custom[ControllerActionId.UnlockTarget] = new(ControllerButton.Secondary, ControllerHandRole.Stick);
var shared = ControllerDiagramModel.Assigned(false, ControllerButton.Secondary, ControllerBindingProfile.Touch, false, a => custom[a]);
Check(shared.Contains(ControllerActionId.Map) && shared.Contains(ControllerActionId.UnlockTarget), "One physical button lists both free-hand and gripped contexts");
Check(!ControllerBindingDefaults.IsBindingAvailable(ControllerBindingProfile.Frame, false, ControllerActionId.WristMenu, ControllerButton.StickClick) &&
    ControllerBindingDefaults.IsBindingAvailable(ControllerBindingProfile.Frame, false, ControllerActionId.SelectTarget, ControllerButton.StickClick),
    "Free click remains reserved for wheel while held click is editable for flight actions");
Check(ControllerDiagramModel.ShortButton(ControllerButton.Primary, true, ControllerBindingProfile.Frame) == "D-pad ↓" &&
    ControllerDiagramModel.ShortButton(ControllerButton.Primary, true, ControllerBindingProfile.Touch) == "X" &&
    ControllerDiagramModel.ShortButton(ControllerButton.Secondary, false, ControllerBindingProfile.Frame) == "X" &&
    ControllerDiagramModel.ShortButton(ControllerButton.Secondary, false, ControllerBindingProfile.Touch) == "B", "Face labels distinguish Frame and Touch layouts");
Console.WriteLine($"Controller diagram mapping checks passed: {checks} assertions (actual mapping/default source; no UI rendering).");
void Check(bool pass, string message) { if (!pass) throw new Exception(message); checks++; }
