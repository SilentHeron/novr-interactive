using System.Collections.Generic;
using BepInEx.Configuration;

namespace NOVR.MotionControls;

public sealed class ControllerBindings
{
    internal readonly ConfigEntry<ControllerBindingProfile> Profile;
    private readonly Bank _frame, _touch;
    private ControllerBindingProfile _lastEffective;
    private int _changeVersion;
    private Bank Active => EffectiveProfile == ControllerBindingProfile.Touch ? _touch : _frame;
    private ControllerBindingBank Values => Active.Values;
    public ConfigEntry<bool> PointerEnabled => Values.PointerEnabled;
    public ConfigEntry<bool> CombatEnabled => Values.CombatEnabled;
    public ConfigEntry<float> PointerResponse => Values.PointerResponse;
    public ConfigEntry<bool> SelectTargetOnStick => Values.SelectTargetOnStick;
    public ConfigEntry<ControllerButton> Fire => Values.Fire;
    public ConfigEntry<ControllerButton> WeaponRelease => Values.WeaponRelease;
    public ConfigEntry<ControllerButton> Countermeasures => Values.Countermeasures;
    public ConfigEntry<ControllerButton> NextWeapon => Values.NextWeapon;
    public ConfigEntry<ControllerButton> SelectTarget => Values.SelectTarget;
    public ConfigEntry<ControllerButton> NextCountermeasure => Values.NextCountermeasure;
    public ConfigEntry<ControllerButton> Map => Values.Map;
    public ConfigEntry<ControllerButton> WristMenu => Values.WristMenu;
    public ConfigEntry<ControllerButton> ClearTargets => Values.ClearTargets;
    public ConfigEntry<ControllerButton> UnlockTarget => Values.UnlockTarget;
    public ConfigEntry<ControllerButton> Brake => Values.Brake;

    public ControllerBindings(ConfigFile config)
    {
        Profile = config.Bind("VR Controller Bindings", "Controller Profile", ControllerBindingProfile.Auto,
            "Auto uses the detected controller family. Frame and Touch keep independent saved bindings; switching profiles never overwrites either layout.");
        Profile.SettingChanged += (_, _) => Changed();
        _frame = new Bank(config, ControllerBindingProfile.Frame, Changed);
        _touch = new Bank(config, ControllerBindingProfile.Touch, Changed);
    }
    private void Changed() { unchecked { _changeVersion++; } }
    internal ControllerBindingProfile EffectiveProfile
    {
        get
        {
            var effective = Profile.Value;
            if (effective == ControllerBindingProfile.Auto)
            {
                var left = TrackedControllers.Profile(true); var right = TrackedControllers.Profile(false);
                effective = left == ControllerHardwareProfile.SteamFrame || right == ControllerHardwareProfile.SteamFrame ? ControllerBindingProfile.Frame :
                    left == ControllerHardwareProfile.Touch || right == ControllerHardwareProfile.Touch || left == ControllerHardwareProfile.Other || right == ControllerHardwareProfile.Other
                        ? ControllerBindingProfile.Touch : _lastEffective == ControllerBindingProfile.Touch ? ControllerBindingProfile.Touch : ControllerBindingProfile.Frame;
            }
            if (_lastEffective != effective) { _lastEffective = effective; Changed(); }
            return effective;
        }
    }
    internal int ChangeVersion { get { _ = EffectiveProfile; return _changeVersion; } }
    internal ControllerActionBinding Get(ControllerActionId action) => Active.Get(action);
    internal void Set(ControllerActionId action, ControllerActionBinding binding)
    {
        var left = ResolveLeft(binding.Hand, ModConfiguration.Instance.MotionControls.LeftHanded.Value);
        if (!IsBindingAvailable(ButtonProfile(left), left, action, binding.Button)) return;
        Active.Set(action, binding);
        if (action == ControllerActionId.SelectTarget && (binding.Hand == ControllerHandRole.Stick || binding.Hand == ControllerHandRole.Throttle))
            Values.SelectTargetOnStick.Value = binding.Hand == ControllerHandRole.Stick;
    }
    internal bool CanUndoEdit => Active.CanUndo;
    internal string UndoEditLabel => Active.CanUndo ? Active.EditLabel : "";

    // A replacement is one user edit, including every assignment it displaces.
    // Validate first so a rejected input cannot leave unrelated actions cleared.
    internal bool TryApplyEdit(ControllerActionId action, ControllerActionBinding binding, bool replaceConflicts)
    {
        var leftHanded = ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        var left = ResolveLeft(binding.Hand, leftHanded);
        if (!IsBindingAvailable(ButtonProfile(left), left, action, binding.Button)) return false;
        var conflicts = GetConflicts(action, binding, leftHanded);
        if (conflicts.Length > 0 && !replaceConflicts) return false;
        var bank = Active;
        var before = bank.Capture();
        foreach (var other in conflicts) bank.Set(other, new(ControllerButton.None, bank.Get(other).Hand));
        bank.Set(action, binding);
        if (action == ControllerActionId.SelectTarget && (binding.Hand == ControllerHandRole.Stick || binding.Hand == ControllerHandRole.Throttle))
            bank.Values.SelectTargetOnStick.Value = binding.Hand == ControllerHandRole.Stick;
        bank.RememberEdit(before, ActionLabel(action));
        return true;
    }

    internal bool UndoEdit() => Active.Undo();
    internal void ResetProfile()
    {
        var bank = Active; var before = bank.Capture();
        bank.Reset(); bank.RememberEdit(before, "Restore defaults");
    }
    internal static bool ResolveLeft(ControllerHandRole role, bool leftHanded) => ControllerBindingDefaults.ResolveLeft(role, leftHanded);
    internal static string ActionLabel(ControllerActionId action) => ControllerBindingDefaults.ActionLabel(action);
    internal static string ContextLabel(ControllerActionId action) => ControllerBindingDefaults.ContextLabel(action);
    internal static bool RequiresGrip(ControllerActionId action) => ControllerBindingDefaults.RequiresGrip(action);
    internal static ControllerButton[] AvailableButtons(ControllerBindingProfile profile, bool left) => ControllerBindingDefaults.AvailableButtons(profile, left);
    internal static bool IsButtonAvailable(ControllerBindingProfile profile, bool left, ControllerButton button) => ControllerBindingDefaults.IsButtonAvailable(profile, left, button);
    internal static bool IsBindingAvailable(ControllerBindingProfile profile, bool left, ControllerActionId action, ControllerButton button) =>
        ControllerBindingDefaults.IsBindingAvailable(profile, left, action, button);
    internal ControllerBindingProfile ButtonProfile(bool left) => TrackedControllers.Profile(left) switch
    {
        ControllerHardwareProfile.SteamFrame => ControllerBindingProfile.Frame,
        ControllerHardwareProfile.Touch => ControllerBindingProfile.Touch,
        ControllerHardwareProfile.Other => ControllerBindingProfile.Touch,
        _ => EffectiveProfile
    };
    internal bool IsButtonAvailable(ControllerActionBinding binding, bool leftHanded)
    {
        var left = ResolveLeft(binding.Hand, leftHanded);
        return IsButtonAvailable(ButtonProfile(left), left, binding.Button);
    }
    internal ControllerActionId[] GetConflicts(ControllerActionId action, ControllerActionBinding proposed, bool leftHanded)
    {
        var result = new List<ControllerActionId>();
        var frame = ButtonProfile(ResolveLeft(proposed.Hand, leftHanded)) == ControllerBindingProfile.Frame;
        foreach (var other in ControllerBindingDefaults.Actions)
            if (ControllerBindingDefaults.Conflict(action, proposed, other, Get(other), leftHanded, frame)) result.Add(other);
        return result.ToArray();
    }
    internal ThrottleButtons ThrottleButtons => new(Brake.Value, Countermeasures.Value,
        Get(ControllerActionId.SelectTarget).Hand == ControllerHandRole.Stick ? ControllerButton.None : SelectTarget.Value, NextCountermeasure.Value);
    internal void ApplyVtolStyle() => ResetProfile();
    internal void ApplyThrottleLayout(ThrottleBindingLayout layout)
    {
        var preset = ThrottleBindingProfiles.Get(layout);
        Set(ControllerActionId.Brake, new(preset.Brake, ControllerHandRole.Throttle));
        Set(ControllerActionId.Countermeasures, new(preset.Countermeasures, ControllerHandRole.Throttle));
        Set(ControllerActionId.SelectTarget, new(layout == ThrottleBindingLayout.VtolStyle ? ControllerButton.StickClick : preset.Target,
            layout == ThrottleBindingLayout.VtolStyle ? ControllerHandRole.Stick : ControllerHandRole.Throttle));
        Set(ControllerActionId.NextCountermeasure, new(preset.NextCountermeasure, ControllerHandRole.Throttle));
        ControllerActions.Instance?.ResetButtons();
    }
    internal float ReadFlight(ControllerSample hand, bool left, ControllerButton button) => Read(hand, button);
    internal static float Read(ControllerSample hand, ControllerButton button) => ControllerBindingBank.Read(hand, button);

    private sealed class Slot
    {
        internal readonly ConfigEntry<ControllerButton> Button;
        internal readonly ConfigEntry<ControllerHandRole> Hand;
        internal Slot(ConfigEntry<ControllerButton> button, ConfigEntry<ControllerHandRole> hand) { Button = button; Hand = hand; }
    }
    private sealed class Bank
    {
        internal readonly ControllerBindingBank Values;
        private readonly ControllerBindingProfile _profile;
        private readonly Dictionary<ControllerActionId, Slot> _slots = new();
        private Snapshot? _beforeEdit, _afterEdit;
        internal string EditLabel { get; private set; } = "";
        internal sealed class Snapshot
        {
            internal readonly Dictionary<ControllerActionId, ControllerActionBinding> Bindings = new();
            internal bool SelectOnStick;
        }
        internal Bank(ConfigFile config, ControllerBindingProfile profile, System.Action changed)
        {
            _profile = profile;
            var section = profile == ControllerBindingProfile.Touch ? "VR Controller Bindings - Touch" : "VR Controller Bindings";
            Values = new ControllerBindingBank(config, section, profile == ControllerBindingProfile.Touch);
            void Watch<T>(ConfigEntry<T> entry) => entry.SettingChanged += (_, _) => changed();
            Watch(Values.PointerEnabled); Watch(Values.CombatEnabled); Watch(Values.PointerResponse);
            Watch(Values.SelectTargetOnStick);
            foreach (var action in ControllerBindingDefaults.Actions)
            {
                var preset = ControllerBindingDefaults.Get(profile, action);
                var button = action switch
                {
                    ControllerActionId.Guns => Values.Fire, ControllerActionId.WeaponRelease => Values.WeaponRelease,
                    ControllerActionId.SelectTarget => Values.SelectTarget, ControllerActionId.UnlockTarget => Values.UnlockTarget,
                    ControllerActionId.ClearTargets => Values.ClearTargets, ControllerActionId.NextWeapon => Values.NextWeapon,
                    ControllerActionId.Countermeasures => Values.Countermeasures, ControllerActionId.NextCountermeasure => Values.NextCountermeasure,
                    ControllerActionId.Brake => Values.Brake, ControllerActionId.Map => Values.Map, ControllerActionId.WristMenu => Values.WristMenu,
                    _ => config.Bind(section, action + " Button", preset.Button, ActionLabel(action) + ". " + ContextLabel(action) + ".")
                };
                var role = preset.Hand;
                if (action == ControllerActionId.SelectTarget) role = Values.SelectTargetOnStick.Value ? ControllerHandRole.Stick : ControllerHandRole.Throttle;
                // Custom old wrist bindings followed the throttle. Stock View
                // stays on physical left when the flight handles are mirrored.
                if (profile == ControllerBindingProfile.Frame && action == ControllerActionId.WristMenu && button.Value != ControllerButton.Menu)
                    role = ControllerHandRole.Throttle;
                var hand = config.Bind(section, action + " Hand", role,
                    "Hand for " + ActionLabel(action) + ". Stick/throttle follow cockpit handedness; Left/Right remain physical.");
                // Free thumbstick click now always belongs to the native hand wheel.
                // Repair only imported global bindings that would become unreachable.
                if ((action == ControllerActionId.Map || action == ControllerActionId.WristMenu) && button.Value == ControllerButton.StickClick)
                { button.Value = preset.Button; hand.Value = preset.Hand; }
                _slots.Add(action, new Slot(button, hand)); Watch(button); Watch(hand);
            }
        }
        internal ControllerActionBinding Get(ControllerActionId action) => new(_slots[action].Button.Value, _slots[action].Hand.Value);
        internal void Set(ControllerActionId action, ControllerActionBinding binding)
        { _slots[action].Button.Value = binding.Button; _slots[action].Hand.Value = binding.Hand; }
        internal Snapshot Capture()
        {
            var snapshot = new Snapshot { SelectOnStick = Values.SelectTargetOnStick.Value };
            foreach (var action in ControllerBindingDefaults.Actions) snapshot.Bindings.Add(action, Get(action));
            return snapshot;
        }
        private bool Matches(Snapshot snapshot)
        {
            if (snapshot.SelectOnStick != Values.SelectTargetOnStick.Value) return false;
            foreach (var action in ControllerBindingDefaults.Actions)
            {
                var current = Get(action); var expected = snapshot.Bindings[action];
                if (current.Button != expected.Button || current.Hand != expected.Hand) return false;
            }
            return true;
        }
        internal bool CanUndo => _beforeEdit != null && _afterEdit != null && Matches(_afterEdit);
        internal void RememberEdit(Snapshot before, string label)
        {
            if (Matches(before)) return; // Saving an unchanged row does not consume Undo.
            _beforeEdit = before; _afterEdit = Capture(); EditLabel = label;
        }
        internal bool Undo()
        {
            // A config reload or another writer may have changed bindings since
            // the edit. Never replace that newer state with a stale snapshot.
            if (!CanUndo) return false;
            var restore = _beforeEdit!;
            _beforeEdit = _afterEdit = null; EditLabel = "";
            foreach (var action in ControllerBindingDefaults.Actions) Set(action, restore.Bindings[action]);
            Values.SelectTargetOnStick.Value = restore.SelectOnStick;
            return true;
        }
        internal void Reset()
        {
            foreach (var action in ControllerBindingDefaults.Actions) Set(action, ControllerBindingDefaults.Get(_profile, action));
            Values.SelectTargetOnStick.Value = true;
        }
    }
}
