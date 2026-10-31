using BepInEx.Configuration;

namespace NOVR.MotionControls;

public sealed class ControllerBindings
{
    public readonly ConfigEntry<bool> PointerEnabled, CombatEnabled;
    public readonly ConfigEntry<ControllerButton> Fire, WeaponRelease, Countermeasures, NextWeapon, SelectTarget, NextCountermeasure, Map, WristMenu, ClearTargets, UnlockTarget, Brake;
    public readonly ConfigEntry<float> PointerResponse;
    public readonly ConfigEntry<bool> RightBumperZoom, SelectTargetOnStick;
    public readonly ConfigEntry<float> ViewZoomFactor;
    public ControllerBindings(ConfigFile config)
    {
        const string section = "VR Controller Bindings";
        PointerEnabled = config.Bind(section, "Controller Pointer", true, "Point at menus and the map with the stick hand; trigger clicks and thumbstick scrolls. Mouse remains available when controllers are untracked.");
        CombatEnabled = config.Bind(section, "Combat Controls", true, "Enable controller weapon and targeting actions while gripping virtual flight controls.");
        PointerResponse = config.Bind(section, "Pointer Response", 24f, new ConfigDescription("Laser aim smoothing. 0 disables smoothing. Higher follows faster.", new AcceptableValueRange<float>(0f, 60f)));
        RightBumperZoom = config.Bind(section, "Right Bumper View Zoom", true, "Hold the physical right bumper to show a magnified inspection window, with either flight grip held or released. Reserved for zoom even in left-handed mode. Requires a controller with a shoulder button.");
        ViewZoomFactor = config.Bind(section, "View Zoom Factor", 1.7f, new ConfigDescription("Magnification of the fixed-depth inspection window while right bumper is held. Headset projection is unchanged; release hides the window.", new AcceptableValueRange<float>(1.1f, 3f)));
        Fire = config.Bind(section, "Fire - stick hand", ControllerButton.Trigger, "GUNS ONLY. Hold while gripping the virtual stick. Guns remain available with a missile or bomb selected; native safety and readiness still apply.");
        WeaponRelease = config.Bind(section, "Weapon Release - stick hand", ControllerButton.Primary, "A on the right controller: release the selected non-gun weapon while gripping the stick. Touching the button only animates the thumb.");
        Countermeasures = config.Bind(section, "Countermeasures - throttle hand", ControllerButton.Primary, "Throttle action button: Frame D-pad down or Touch X. Hold while gripping to deploy selected countermeasures.");
        NextWeapon = config.Bind(section, "Next Weapon - stick hand", ControllerButton.TopOrSecondary, "Frame Y, or Touch B in the right-handed layout. Release while gripping to cycle weapons.");
        // Keep the old key so existing custom button assignments survive the hand change.
        SelectTarget = config.Bind(section, "Select Target - throttle hand", ControllerButton.StickClick, "Target-select button. Uses the stick hand when Select Target On Stick Hand is enabled; otherwise the throttle hand. Press and release while gripping to select under the head-directed designator.");
        NextCountermeasure = config.Bind(section, "Next Countermeasure - throttle hand", ControllerButton.Secondary, "Frame D-pad right, or Y on Touch-compatible controllers.");
        Map = config.Bind(section, "Map - stick hand", ControllerButton.OutsideOrSecondary, "Frame B, or B on Touch-compatible controllers. Opens/closes the tactical map, including after a crash or in spectator view. With a live flight stick held, its target-unselect or weapon-cycle binding takes priority.");
        WristMenu = config.Bind(section, "Wrist Menu - throttle hand", ControllerButton.Menu, "Frame View, or the Touch left menu button. Opens the wrist panel in the cockpit, or the tactical recovery map outside it. F9 is a keyboard fallback.");
        ClearTargets = config.Bind(section, "Clear Targets - stick hand", ControllerButton.None, "Use wrist Flight actions or the MFD CLEAR key to clear all targets. No default controller button; Frame Y cycles weapons.");
        UnlockTarget = config.Bind(section, "Unlock Last Target - stick hand", ControllerButton.OutsideOrNone, "Frame B while gripping the stick removes the most recently selected target. With the stick released, B opens the map. Wrist Flight actions provides the same action on controllers without a spare button.");
        var brakeDefault = ControllerButton.Trigger;
        // Do not give a new action a button already used by a custom setup.
        if (ThrottleBindingProfiles.SharesButton(brakeDefault, true, Countermeasures.Value, SelectTarget.Value, NextCountermeasure.Value, WristMenu.Value) ||
            ThrottleBindingProfiles.SharesButton(brakeDefault, false, Countermeasures.Value, SelectTarget.Value, NextCountermeasure.Value, WristMenu.Value))
            brakeDefault = ControllerButton.None;
        Brake = config.Bind(section, "Brake - throttle hand", brakeDefault,
            "Hold throttle trigger while gripping the throttle to use the game's wheel/air-brake input. Other layouts are available in the wrist Button bindings page.");
        // Only move the recognized stock VTOL layout automatically. Older throttle
        // presets, custom layouts and an explicitly saved hand choice are retained.
        var stockStickButtons = ((NextWeapon.Value == ControllerButton.Primary || NextWeapon.Value == ControllerButton.OutsideOrSecondary) &&
                ClearTargets.Value == ControllerButton.Top && UnlockTarget.Value == ControllerButton.SecondaryOrNone) ||
            (NextWeapon.Value == ControllerButton.TopOrSecondary && ClearTargets.Value == ControllerButton.None && UnlockTarget.Value == ControllerButton.OutsideOrNone);
        var stockVtol = Fire.Value == ControllerButton.Trigger && stockStickButtons &&
            Map.Value == ControllerButton.OutsideOrSecondary && WristMenu.Value == ControllerButton.Menu &&
            Brake.Value == ControllerButton.Trigger && Countermeasures.Value == ControllerButton.Primary &&
            SelectTarget.Value == ControllerButton.StickClick && NextCountermeasure.Value == ControllerButton.Secondary;
        SelectTargetOnStick = config.Bind(section, "Select Target On Stick Hand", stockVtol,
            "VTOL-style target selection uses the stick hand (right by default), while gripping or latched. Disable to keep a custom throttle-hand selection binding.");
        var upgraded = config.Bind(section, "VTOL Style Upgrade Applied", false, "One-time upgrade of the previous stock button layout; custom layouts are preserved.");
        if (!upgraded.Value)
        {
            if (Fire.Value == ControllerButton.Trigger && ClearTargets.Value == ControllerButton.Top &&
                Map.Value == ControllerButton.OutsideOrSecondary && WristMenu.Value == ControllerButton.Menu &&
                UnlockTarget.Value == ControllerButton.SecondaryOrNone && NextWeapon.Value == ControllerButton.ShoulderOrPrimary &&
                ThrottleBindingProfiles.Identify(ThrottleButtons) == ThrottleBindingLayout.CountermeasureTrigger)
                ApplyVtolStyle();
            upgraded.Value = true;
        }
        var splitUpgrade = config.Bind(section, "Split Weapon Controls Upgrade Applied", false, "One-time move of the stock A weapon-cycle binding to B, freeing A for weapon release.");
        if (!splitUpgrade.Value)
        {
            if (stockVtol && NextWeapon.Value == ControllerButton.Primary)
                NextWeapon.Value = ControllerButton.OutsideOrSecondary;
            splitUpgrade.Value = true;
        }
        var targetUpgrade = config.Bind(section, "Frame Target Buttons Upgrade Applied", false,
            "One-time change of the stock Frame bindings: B unselects and Y cycles weapons. Custom layouts and Touch bindings are retained.");
        if (!targetUpgrade.Value)
        {
            if (stockVtol && NextWeapon.Value == ControllerButton.OutsideOrSecondary &&
                ClearTargets.Value == ControllerButton.Top && UnlockTarget.Value == ControllerButton.SecondaryOrNone)
            {
                NextWeapon.Value = ControllerButton.TopOrSecondary;
                UnlockTarget.Value = ControllerButton.OutsideOrNone;
                ClearTargets.Value = ControllerButton.None;
            }
            targetUpgrade.Value = true;
        }
    }

    internal ThrottleButtons ThrottleButtons => new(Brake.Value, Countermeasures.Value,
        SelectTargetOnStick.Value ? ControllerButton.None : SelectTarget.Value, NextCountermeasure.Value);
    internal void ApplyVtolStyle()
    {
        Fire.Value = ControllerButton.Trigger; WeaponRelease.Value = ControllerButton.Primary; NextWeapon.Value = ControllerButton.TopOrSecondary;
        UnlockTarget.Value = ControllerButton.OutsideOrNone; ClearTargets.Value = ControllerButton.None;
        RightBumperZoom.Value = true;
        ApplyThrottleLayout(ThrottleBindingLayout.VtolStyle);
    }

    internal float ReadFlight(ControllerSample hand, bool left, ControllerButton button) =>
        RightBumperZoom.Value && !left && ControllerButtonNames.Resolve(button, hand.NativeFrame) == ControllerButton.Shoulder
            ? 0 : Read(hand, button);
    internal void ApplyThrottleLayout(ThrottleBindingLayout layout)
    {
        var preset = ThrottleBindingProfiles.Get(layout);
        Brake.Value = preset.Brake; Countermeasures.Value = preset.Countermeasures;
        SelectTargetOnStick.Value = layout == ThrottleBindingLayout.VtolStyle;
        SelectTarget.Value = SelectTargetOnStick.Value ? ControllerButton.StickClick : preset.Target;
        NextCountermeasure.Value = preset.NextCountermeasure;
        ControllerActions.Instance?.ResetButtons();
    }

    internal static float Read(ControllerSample hand, ControllerButton button) => ControllerButtonNames.Resolve(button, hand.NativeFrame) switch
    {
        ControllerButton.Trigger => hand.Trigger,
        ControllerButton.Primary => hand.Primary ? 1 : 0,
        ControllerButton.Secondary => hand.Secondary ? 1 : 0,
        ControllerButton.Menu => hand.Menu ? 1 : 0,
        ControllerButton.Shoulder => hand.Shoulder ? 1 : 0,
        ControllerButton.Top => hand.Top ? 1 : 0,
        ControllerButton.Outside => hand.Outside ? 1 : 0,
        ControllerButton.StickClick => hand.StickClick ? 1 : 0,
        _ => 0
    };
}
