using BepInEx.Configuration;
using NOVR.MotionControls;

var count = 0;
var directory = Path.Combine(Path.GetTempPath(), "novr-controller-bindings-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
const string frameSection = "VR Controller Bindings";
try
{
    foreach (var profile in new[] { ControllerBindingProfile.Frame, ControllerBindingProfile.Touch })
    foreach (var leftHanded in new[] { false, true })
    {
        Hardware(profile); ModConfiguration.Instance.MotionControls.LeftHanded.Value = leftHanded;
        var bindings = new ControllerBindings(Config("defaults-" + profile + leftHanded));
        Equal(bindings.EffectiveProfile, profile, "Detected preset");
        foreach (var action in ControllerBindingDefaults.Actions)
        {
            var binding = bindings.Get(action); var expected = ControllerBindingDefaults.Get(profile, action);
            Equal(binding.Button, expected.Button, "Default button " + action);
            Equal(binding.Hand, expected.Hand, "Default hand " + action);
            var left = ControllerBindings.ResolveLeft(binding.Hand, leftHanded);
            True(ControllerBindings.IsBindingAvailable(profile, left, action, binding.Button), "Default supported " + action);
            True(bindings.GetConflicts(action, binding, leftHanded).Length == 0, "Default conflict-free " + action);
        }
        True(ControllerBindings.ResolveLeft(bindings.Get(ControllerActionId.WristMenu).Hand, leftHanded), "View remains physical left");
        Equal(ControllerBindings.ResolveLeft(bindings.Get(ControllerActionId.Guns).Hand, leftHanded), leftHanded, "Guns follow stick hand");
        Equal(ControllerBindings.ResolveLeft(bindings.Get(ControllerActionId.Countermeasures).Hand, leftHanded), !leftHanded, "CM follows throttle hand");
        var b = bindings.Get(ControllerActionId.UnlockTarget);
        True(!bindings.GetConflicts(ControllerActionId.Map, b, leftHanded).Contains(ControllerActionId.UnlockTarget), "Map shares only free-hand context");
        True(bindings.GetConflicts(ControllerActionId.Radar, b, leftHanded).Contains(ControllerActionId.UnlockTarget), "Assigned radar conflicts with combat");
        True(bindings.GetConflicts(ControllerActionId.WristMenu, b, leftHanded).Contains(ControllerActionId.Map), "Global View conflicts with map");
        var release = bindings.Get(ControllerActionId.WeaponRelease);
        True(bindings.GetConflicts(ControllerActionId.Guns, release, leftHanded).Contains(ControllerActionId.WeaponRelease), "Guns and release conflict on same input");
    }

    foreach (var profile in new[] { ControllerBindingProfile.Frame, ControllerBindingProfile.Touch })
    foreach (var left in new[] { false, true })
    {
        True(!ControllerBindings.IsBindingAvailable(profile, left, ControllerActionId.Map, ControllerButton.Trigger), "Pointer trigger reserved in map");
        True(!ControllerBindings.IsBindingAvailable(profile, left, ControllerActionId.WristMenu, ControllerButton.Trigger), "Pointer trigger reserved in View");
        True(ControllerBindings.IsBindingAvailable(profile, left, ControllerActionId.Radar, ControllerButton.Trigger), "Radar can use trigger while holding a handle");
        True(!ControllerBindings.IsBindingAvailable(profile, left, ControllerActionId.Map, ControllerButton.StickClick), "Free stick click reserved for option wheel, not map");
        True(!ControllerBindings.IsBindingAvailable(profile, left, ControllerActionId.WristMenu, ControllerButton.StickClick), "Free stick click reserved for option wheel, not View");
        True(ControllerBindings.IsBindingAvailable(profile, left, ControllerActionId.SelectTarget, ControllerButton.StickClick), "Held-handle target click remains assignable");
        var buttons = ControllerBindings.AvailableButtons(profile, left);
        buttons[0] = ControllerButton.Top;
        Equal(ControllerBindings.AvailableButtons(profile, left)[0], ControllerButton.None, "Picker cannot mutate shared availability");
    }
    foreach (var button in new[] { ControllerButton.Top, ControllerButton.Outside, ControllerButton.Shoulder, ControllerButton.Menu })
        True(!ControllerBindings.IsButtonAvailable(ControllerBindingProfile.Touch, false, button), "Touch right has no " + button);
    True(ControllerBindings.IsButtonAvailable(ControllerBindingProfile.Touch, true, ControllerButton.Menu), "Touch left menu is available");
    Equal(ControllerButtonNames.Describe(ControllerButton.Secondary, false, false), "B", "Touch B name");
    Equal(ControllerButtonNames.Describe(ControllerButton.TopOrSecondary, true, false), "Y", "Touch Y name");
    Equal(ControllerButtonNames.Describe(ControllerButton.TopOrSecondary, false, true), "Y", "Frame Y name");
    Equal(ControllerButtonNames.Describe(ControllerButton.OutsideOrNone, false, true), "B", "Frame B name");

    // Exercise real BepInEx config entries, persistence and migration, not mocked binds.
    Hardware(ControllerBindingProfile.Frame); ModConfiguration.Instance.MotionControls.LeftHanded.Value = false;
    var config = Config("saved-banks");
    config.Bind(frameSection, "Next Weapon - stick hand", ControllerButton.Secondary).Value = ControllerButton.Secondary;
    config.Bind(frameSection, "Select Target - throttle hand", ControllerButton.PrimaryOrSecondary).Value = ControllerButton.PrimaryOrSecondary;
    config.Bind(frameSection, "Select Target On Stick Hand", false).Value = false;
    config.Bind(frameSection, "Wrist Menu - throttle hand", ControllerButton.StickClick).Value = ControllerButton.StickClick;
    var saved = new ControllerBindings(config);
    Equal(saved.Get(ControllerActionId.NextWeapon).Button, ControllerButton.Secondary, "Custom Frame button preserved");
    Equal(saved.Get(ControllerActionId.SelectTarget).Hand, ControllerHandRole.Throttle, "Legacy custom target hand preserved");
    Equal(saved.Get(ControllerActionId.WristMenu).Hand, ControllerHandRole.Left, "Legacy free-stick View moves to reachable physical left menu");
    Equal(saved.Get(ControllerActionId.WristMenu).Button, ControllerButton.Menu, "Legacy free-stick View yields its click to the option wheel");
    var revision = saved.ChangeVersion;
    saved.Set(ControllerActionId.Gear, new(ControllerButton.Outside, ControllerHandRole.Throttle));
    True(saved.ChangeVersion > revision, "Editing binding changes revision");
    revision = saved.ChangeVersion;
    Hardware(ControllerBindingProfile.Touch);
    Equal(saved.EffectiveProfile, ControllerBindingProfile.Touch, "Auto selects Touch");
    True(saved.ChangeVersion > revision, "Detected family change changes revision");
    Equal(saved.Get(ControllerActionId.NextWeapon).Hand, ControllerHandRole.Throttle, "Touch has its own default");
    saved.Set(ControllerActionId.Gear, new(ControllerButton.Primary, ControllerHandRole.Right));
    Equal(saved.Get(ControllerActionId.Gear).Button, ControllerButton.Primary, "Touch customization stored");
    saved.Set(ControllerActionId.WristMenu, new(ControllerButton.Trigger, ControllerHandRole.Left));
    Equal(saved.Get(ControllerActionId.WristMenu).Button, ControllerButton.Menu, "Set rejects View trigger");
    saved.Set(ControllerActionId.Map, new(ControllerButton.Trigger, ControllerHandRole.Stick));
    Equal(saved.Get(ControllerActionId.Map).Button, ControllerButton.OutsideOrSecondary, "Set rejects map trigger");
    saved.Set(ControllerActionId.Gear, new(ControllerButton.Shoulder, ControllerHandRole.Right));
    Equal(saved.Get(ControllerActionId.Gear).Button, ControllerButton.Primary, "Set rejects unsupported physical button");
    saved.Set(ControllerActionId.WristMenu, new(ControllerButton.Menu, ControllerHandRole.Right));
    Equal(saved.Get(ControllerActionId.WristMenu).Hand, ControllerHandRole.Left, "Set rejects reserved Touch system menu");
    config.Save();
    var restored = new ControllerBindings(new ConfigFile(config.ConfigFilePath, false) { SaveOnConfigSet = false });
    Equal(restored.Get(ControllerActionId.Gear).Button, ControllerButton.Primary, "Touch customization survives reload");
    Hardware(ControllerBindingProfile.Frame);
    Equal(restored.Get(ControllerActionId.Gear).Button, ControllerButton.Outside, "Frame customization survives profile roundtrip and reload");
    Equal(restored.Get(ControllerActionId.NextWeapon).Button, ControllerButton.Secondary, "Legacy custom button survives reload");
    Hardware(ControllerBindingProfile.Touch); restored.ResetProfile();
    Equal(restored.Get(ControllerActionId.Gear).Button, ControllerButton.None, "Reset restores selected bank");
    Hardware(ControllerBindingProfile.Frame);
    Equal(restored.Get(ControllerActionId.Gear).Button, ControllerButton.Outside, "Reset does not erase other bank");
    revision = restored.ChangeVersion;
    restored.Profile.Value = ControllerBindingProfile.Touch;
    True(restored.ChangeVersion > revision && restored.EffectiveProfile == ControllerBindingProfile.Touch, "Manual override changes revision");
    Equal(restored.ButtonProfile(false), ControllerBindingProfile.Frame, "Availability uses physical hardware despite override");
    restored.Profile.Value = ControllerBindingProfile.Auto;
    Hardware(ControllerBindingProfile.Touch); _ = restored.EffectiveProfile;
    TrackedControllers.Left = TrackedControllers.Right = ControllerHardwareProfile.Unknown;
    revision = restored.ChangeVersion;
    Equal(restored.EffectiveProfile, ControllerBindingProfile.Touch, "Disconnect retains last profile");
    Equal(restored.ChangeVersion, revision, "Disconnect does not repeatedly reset input gates");
    var unknown = new ControllerBindings(Config("unknown"));
    Equal(unknown.EffectiveProfile, ControllerBindingProfile.Frame, "Fresh unknown starts with Frame compatibility");
    TrackedControllers.Right = ControllerHardwareProfile.Other;
    Equal(unknown.EffectiveProfile, ControllerBindingProfile.Touch, "Known generic controller uses Touch fallback");

    Hardware(ControllerBindingProfile.Frame);
    var stock = Config("old-stock");
    stock.Bind(frameSection, "Next Weapon - stick hand", ControllerButton.OutsideOrSecondary);
    stock.Bind(frameSection, "Clear Targets - stick hand", ControllerButton.Top);
    stock.Bind(frameSection, "Unlock Last Target - stick hand", ControllerButton.SecondaryOrNone);
    var migrated = new ControllerBindings(stock);
    Equal(migrated.Get(ControllerActionId.NextWeapon).Button, ControllerButton.TopOrSecondary, "Stock Y migration retained");
    Equal(migrated.Get(ControllerActionId.UnlockTarget).Button, ControllerButton.OutsideOrNone, "Stock B migration retained");
    Equal(migrated.Get(ControllerActionId.ClearTargets).Button, ControllerButton.None, "Stock clear no longer claims Y");
    Equal(migrated.Get(ControllerActionId.SelectTarget).Hand, ControllerHandRole.Stick, "Stock targeting moved to stick");

    foreach (var profile in new[] { ControllerBindingProfile.Frame, ControllerBindingProfile.Touch })
    foreach (var leftHanded in new[] { false, true })
    {
        Hardware(profile); ModConfiguration.Instance.MotionControls.LeftHanded.Value = leftHanded;
        var bindings = new ControllerBindings(Config("read-" + profile + leftHanded));
        var hand = new ControllerSample { NativeFrame = profile == ControllerBindingProfile.Frame, Shoulder = true, StickClick = true, Trigger = .9f, Primary = true };
        foreach (var left in new[] {false, true})
        {
            Equal(bindings.ReadFlight(hand, left, ControllerButton.Shoulder), 1f, "Shoulder is available without retired zoom reservation");
            Equal(bindings.ReadFlight(hand, left, ControllerButton.StickClick), 1f, "Held-handle stick click remains available");
            Equal(bindings.ReadFlight(hand, left, ControllerButton.Trigger), .9f, "Trigger pressure is preserved");
        }

    }

    // A menu confirmation is one undoable edit, even when it displaces several
    // actions. Use actual BepInEx values so restoration and persistence agree.
    foreach (var profile in new[] { ControllerBindingProfile.Frame, ControllerBindingProfile.Touch })
    foreach (var leftHanded in new[] { false, true })
    {
        Hardware(profile); ModConfiguration.Instance.MotionControls.LeftHanded.Value = leftHanded;
        var editConfig = Config("undo-" + profile + leftHanded);
        var bindings = new ControllerBindings(editConfig);
        True(!bindings.CanUndoEdit && !bindings.UndoEdit(), "New profile has no invented undo history");
        var release = bindings.Get(ControllerActionId.WeaponRelease);
        // An imported configuration may already contain duplicate actions.
        bindings.Set(ControllerActionId.Gear, release);
        var before = ControllerBindingDefaults.Actions.ToDictionary(action => action, bindings.Get);
        var beforeVersion = bindings.ChangeVersion;
        True(!bindings.TryApplyEdit(ControllerActionId.Radar, release, false), "Conflicts need explicit replacement");
        foreach (var action in ControllerBindingDefaults.Actions)
            Equal(bindings.Get(action), before[action], "Rejected edit preserves " + action);
        Equal(bindings.ChangeVersion, beforeVersion, "Rejected edit does not reset input state");
        True(bindings.TryApplyEdit(ControllerActionId.Radar, release, true), "Replacement can be confirmed");
        Equal(bindings.Get(ControllerActionId.WeaponRelease).Button, ControllerButton.None, "Replacement unassigns weapon release");
        Equal(bindings.Get(ControllerActionId.Gear).Button, ControllerButton.None, "Replacement unassigns every conflicting action");
        Equal(bindings.Get(ControllerActionId.Guns), before[ControllerActionId.Guns], "Replacement preserves unrelated guns input");
        True(bindings.CanUndoEdit && bindings.ChangeVersion > beforeVersion, "Saved edit is undoable and resets input gates");
        Equal(bindings.UndoEditLabel, ControllerBindings.ActionLabel(ControllerActionId.Radar), "Undo names the last saved edit");
        True(bindings.TryApplyEdit(ControllerActionId.Radar, bindings.Get(ControllerActionId.Radar), false), "Saving an unchanged row is accepted");
        True(!bindings.TryApplyEdit(ControllerActionId.WristMenu, new(ControllerButton.Trigger, ControllerHandRole.Left), true), "Invalid pointer trigger cannot clear other assignments");
        True(bindings.CanUndoEdit && bindings.UndoEdit(), "No-op and invalid edits preserve the previous undo");
        foreach (var action in ControllerBindingDefaults.Actions)
            Equal(bindings.Get(action), before[action], "Undo restores button and hand for " + action);
        True(!bindings.CanUndoEdit && !bindings.UndoEdit(), "Undo preserves other settings and is one-shot");

        bindings.Set(ControllerActionId.SelectTarget, new(ControllerButton.None, ControllerHandRole.Throttle));
        bindings.ResetProfile();
        True(bindings.SelectTargetOnStick.Value, "Default reset restores its ancillary settings");
        True(bindings.CanUndoEdit && bindings.UndoEdit(), "Default reset is undoable");
        Equal(bindings.Get(ControllerActionId.Gear), release, "Reset undo restores custom bindings");
        Equal(bindings.Get(ControllerActionId.SelectTarget).Hand, ControllerHandRole.Throttle, "Reset undo restores target hand");
        True(!bindings.SelectTargetOnStick.Value, "Reset undo restores ancillary settings");
        bindings.ResetProfile(); bindings.PointerResponse.Value = 2.2f;
        True(bindings.UndoEdit(), "Unrelated pointer response edit does not discard binding undo");
        Equal(bindings.PointerResponse.Value, 2.2f, "Undo does not overwrite unrelated settings");

        True(bindings.TryApplyEdit(ControllerActionId.Radar, release, true), "Second replacement saves");
        bindings.Set(ControllerActionId.NextWeapon, new(ControllerButton.None, ControllerHandRole.Throttle));
        True(!bindings.CanUndoEdit && !bindings.UndoEdit(), "Newer external binding change blocks stale undo");
        Equal(bindings.Get(ControllerActionId.NextWeapon).Button, ControllerButton.None, "Blocked undo leaves newer change intact");
        editConfig.Save();
        var afterReload = new ControllerBindings(new ConfigFile(editConfig.ConfigFilePath, false) { SaveOnConfigSet = false });
        Equal(afterReload.Get(ControllerActionId.Radar), release, "Confirmed edit persists");
        True(!afterReload.CanUndoEdit, "Undo history is confined to the current game session");
    }
    Hardware(ControllerBindingProfile.Frame); ModConfiguration.Instance.MotionControls.LeftHanded.Value = false;
    var perBank = new ControllerBindings(Config("undo-independent-banks"));
    var frameZoom = perBank.Get(ControllerActionId.Radar);
    True(perBank.TryApplyEdit(ControllerActionId.Radar, perBank.Get(ControllerActionId.WeaponRelease), true), "Frame edit saved");
    var editedFrameZoom = perBank.Get(ControllerActionId.Radar);
    Hardware(ControllerBindingProfile.Touch);
    var touchZoom = perBank.Get(ControllerActionId.Radar);
    True(!perBank.CanUndoEdit, "Touch does not expose Frame undo");
    True(perBank.TryApplyEdit(ControllerActionId.Radar, perBank.Get(ControllerActionId.WeaponRelease), true), "Touch edit saved independently");
    True(perBank.UndoEdit(), "Touch edit undoes independently");
    Equal(perBank.Get(ControllerActionId.Radar), touchZoom, "Touch undo restores Touch button and hand");
    Hardware(ControllerBindingProfile.Frame);
    Equal(perBank.Get(ControllerActionId.Radar), editedFrameZoom, "Touch undo does not overwrite Frame changes");
    True(perBank.CanUndoEdit && perBank.UndoEdit(), "Frame retains its own undo through profile switches");
    Equal(perBank.Get(ControllerActionId.Radar), frameZoom, "Frame undo restores Frame button and hand");
    Console.WriteLine($"Controller binding checks passed: {count} assertions (actual binding sources and BepInEx config persistence).");
}
finally { Directory.Delete(directory, true); }

ConfigFile Config(string name) => new(Path.Combine(directory, name + ".cfg"), false) { SaveOnConfigSet = false };
void Hardware(ControllerBindingProfile profile) => TrackedControllers.Left = TrackedControllers.Right =
    profile == ControllerBindingProfile.Frame ? ControllerHardwareProfile.SteamFrame : ControllerHardwareProfile.Touch;
void Equal<T>(T actual, T expected, string label) => True(EqualityComparer<T>.Default.Equals(actual, expected), label + $": expected {expected}, got {actual}");
void True(bool value, string label) { if (!value) throw new Exception(label); count++; }
