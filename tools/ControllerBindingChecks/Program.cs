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
        True(bindings.GetConflicts(ControllerActionId.Zoom, b, leftHanded).Contains(ControllerActionId.UnlockTarget), "Global zoom conflicts with combat");
        True(bindings.GetConflicts(ControllerActionId.WristMenu, b, leftHanded).Contains(ControllerActionId.Map), "Global View conflicts with map");
        var release = bindings.Get(ControllerActionId.WeaponRelease);
        True(bindings.GetConflicts(ControllerActionId.Guns, release, leftHanded).Contains(ControllerActionId.WeaponRelease), "Guns and release conflict on same input");
    }

    foreach (var profile in new[] { ControllerBindingProfile.Frame, ControllerBindingProfile.Touch })
    foreach (var left in new[] { false, true })
    {
        True(!ControllerBindings.IsBindingAvailable(profile, left, ControllerActionId.Map, ControllerButton.Trigger), "Pointer trigger reserved in map");
        True(!ControllerBindings.IsBindingAvailable(profile, left, ControllerActionId.WristMenu, ControllerButton.Trigger), "Pointer trigger reserved in View");
        True(ControllerBindings.IsBindingAvailable(profile, left, ControllerActionId.Zoom, ControllerButton.Trigger), "Zoom can use trigger outside menus");
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
    Equal(saved.Get(ControllerActionId.WristMenu).Hand, ControllerHandRole.Throttle, "Custom old View follows old throttle hand");
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
        var zoom = bindings.Get(ControllerActionId.Zoom); var zoomLeft = ControllerBindings.ResolveLeft(zoom.Hand, leftHanded);
        var hand = new ControllerSample { NativeFrame = profile == ControllerBindingProfile.Frame, Shoulder = true, StickClick = true, Trigger = .9f, Primary = true };
        Equal(bindings.ReadFlight(hand, zoomLeft, zoom.Button), 0f, "Zoom reserves only its physical button");
        Equal(bindings.ReadFlight(hand, !zoomLeft, zoom.Button), 1f, "Zoom does not consume opposite hand input");
        Equal(bindings.ReadFlight(hand, zoomLeft, ControllerButton.Trigger), .9f, "Zoom leaves guns/brake trigger intact");
        Equal(ControllerBindings.Read(hand, zoom.Button), 1f, "Zoom receives raw input");
        bindings.ZoomEnabled.Value = false;
        Equal(bindings.ReadFlight(hand, zoomLeft, zoom.Button), 1f, "Disabled zoom releases its reservation");
    }
    Console.WriteLine($"Controller binding checks passed: {count} assertions (actual binding sources and BepInEx config persistence).");
}
finally { Directory.Delete(directory, true); }

ConfigFile Config(string name) => new(Path.Combine(directory, name + ".cfg"), false) { SaveOnConfigSet = false };
void Hardware(ControllerBindingProfile profile) => TrackedControllers.Left = TrackedControllers.Right =
    profile == ControllerBindingProfile.Frame ? ControllerHardwareProfile.SteamFrame : ControllerHardwareProfile.Touch;
void Equal<T>(T actual, T expected, string label) => True(EqualityComparer<T>.Default.Equals(actual, expected), label + $": expected {expected}, got {actual}");
void True(bool value, string label) { if (!value) throw new Exception(label); count++; }
