using BepInEx.Configuration;
using NOVR.MotionControls;

internal static class TouchCountermeasureChecks
{
    private const string Section = "VR Controller Bindings - Touch";
    private const string Revision = "Touch Countermeasure Cycle Upgrade Applied";
    internal static void Run(Action<bool, string> check, Func<string, ConfigFile> config)
    {
        TrackedControllers.Left = TrackedControllers.Right = ControllerHardwareProfile.Touch;
        ModConfiguration.Instance.MotionControls.LeftHanded.Value = false;
        var old = Legacy("touch-cm-old-default");
        var upgraded = Reload(old);
        check(upgraded.Get(ControllerActionId.NextCountermeasure).Button == ControllerButton.StickClick &&
            upgraded.Get(ControllerActionId.NextCountermeasure).Hand == ControllerHandRole.Throttle,
            "untouched old Touch preset gains held-throttle click for countermeasure type");
        check(upgraded.Get(ControllerActionId.WristMenu).Button == ControllerButton.Menu &&
            upgraded.Get(ControllerActionId.WristMenu).Hand == ControllerHandRole.Left,
            "countermeasure upgrade leaves existing physical-left menu behavior unchanged");
        check(upgraded.GetConflicts(ControllerActionId.NextCountermeasure, upgraded.Get(ControllerActionId.NextCountermeasure), false).Length == 0,
            "new Touch countermeasure default has no held-button conflicts");
        upgraded.Set(ControllerActionId.NextCountermeasure, new(ControllerButton.None, ControllerHandRole.Throttle));
        // The upgraded bank saves its actual edit; do not write the older
        // ConfigFile instance back over the updated revision on disk.
        var persistent = new ConfigFile(old.ConfigFilePath, false) { SaveOnConfigSet = false };
        check(Reload(persistent).Get(ControllerActionId.NextCountermeasure).Button == ControllerButton.None,
            "explicitly unassigning after migration survives another launch");

        foreach (var changed in ControllerBindingDefaults.Actions)
        {
            var file = Legacy("touch-cm-custom-" + changed);
            var bank = new ControllerBindings(file);
            bank.Profile.Value = ControllerBindingProfile.Touch;
            var current = bank.Get(changed);
            bank.Set(changed, new(current.Button == ControllerButton.None ? ControllerButton.Primary : ControllerButton.None, current.Hand));
            // Simulate a pre-revision custom bank, not an edit after upgrade.
            bank.Set(ControllerActionId.NextCountermeasure, changed == ControllerActionId.NextCountermeasure
                ? new(ControllerButton.Primary, ControllerHandRole.Throttle) : new(ControllerButton.None, ControllerHandRole.Throttle));
            file.Bind(Section, Revision, false).Value = false;
            var before = ControllerBindingDefaults.Actions.ToDictionary(action => action, bank.Get);
            var after = Reload(file);
            foreach (var action in ControllerBindingDefaults.Actions)
                check(after.Get(action).Button == before[action].Button && after.Get(action).Hand == before[action].Hand,
                    "custom old Touch action/hand map survives migration: " + changed + " / " + action);
        }
        var handFile = Legacy("touch-cm-custom-hand");
        handFile.Bind(Section, "NextCountermeasure Hand", ControllerHandRole.Throttle).Value = ControllerHandRole.Right;
        var customHand = Reload(handFile);
        check(customHand.Get(ControllerActionId.NextCountermeasure).Button == ControllerButton.None &&
            customHand.Get(ControllerActionId.NextCountermeasure).Hand == ControllerHandRole.Right,
            "explicit physical-hand override prevents automatic default upgrade");
        customHand.ResetProfile();
        check(customHand.Get(ControllerActionId.NextCountermeasure).Button == ControllerButton.StickClick &&
            customHand.Get(ControllerActionId.NextCountermeasure).Hand == ControllerHandRole.Throttle,
            "Restore defaults explicitly adopts new Touch countermeasure binding");
        customHand.Profile.Value = ControllerBindingProfile.Frame;
        check(customHand.Get(ControllerActionId.NextCountermeasure).Button == ControllerButton.Secondary,
            "Touch migration leaves the independent Frame bank unchanged");

        ConfigFile Legacy(string name)
        {
            var file = config(name);
            // Existing configs already contain their old button values; binding
            // the new defaults must not overwrite them before revision checks.
            file.Bind(Section, "Next Countermeasure - throttle hand", ControllerButton.None).Value = ControllerButton.None;
            return file;
        }
        ControllerBindings Reload(ConfigFile file)
        {
            file.Save();
            var bindings = new ControllerBindings(new ConfigFile(file.ConfigFilePath, false) { SaveOnConfigSet = true });
            bindings.Profile.Value = ControllerBindingProfile.Touch;
            return bindings;
        }
    }
}
