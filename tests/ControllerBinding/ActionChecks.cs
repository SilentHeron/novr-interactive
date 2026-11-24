using System.Reflection;
using BepInEx.Configuration;
using NOVR.MotionControls;
using NOVR.VrCamera;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class ActionChecks
{
    private enum Delivery
    {
        Held, Released, Queued, ClearAll
    }
    // The game's default delays: a tap is released within ClickDelay, a hold
    // acts once it has lasted PressDelay.
    private const float ClickDelay = .25f, PressDelay = .15f;
    private static readonly (ControllerActionId binding, NativeControllerAction native, Delivery delivery, bool combat)[] Actions =
    {
        (ControllerActionId.Brake, NativeControllerAction.Brake, Delivery.Held, false),
        (ControllerActionId.Gear, NativeControllerAction.Gear, Delivery.Queued, false),
        (ControllerActionId.FlightAssist, NativeControllerAction.FlightAssist, Delivery.Queued, false),
        (ControllerActionId.HoverAssist, NativeControllerAction.AutoHover, Delivery.Queued, false),
        (ControllerActionId.NavLights, NativeControllerAction.NavLights, Delivery.Queued, false),
        (ControllerActionId.Guns, NativeControllerAction.Guns, Delivery.Held, true),
        (ControllerActionId.WeaponRelease, NativeControllerAction.WeaponRelease, Delivery.Held, true),
        (ControllerActionId.FireSelected, NativeControllerAction.Fire, Delivery.Held, true),
        (ControllerActionId.Countermeasures, NativeControllerAction.Countermeasures, Delivery.Held, true),
        (ControllerActionId.SelectTarget, NativeControllerAction.Select, Delivery.Released, true),
        (ControllerActionId.NextWeapon, NativeControllerAction.NextWeapon, Delivery.Released, true),
        (ControllerActionId.NextCountermeasure, NativeControllerAction.NextCountermeasure, Delivery.Released, true),
        (ControllerActionId.Radar, NativeControllerAction.Radar, Delivery.Queued, true),
        (ControllerActionId.UnlockTarget, NativeControllerAction.Cancel, Delivery.Released, true),
        (ControllerActionId.ClearTargets, NativeControllerAction.Unknown, Delivery.ClearAll, true)
    };
    internal static void Run(Action<bool, string> check, Func<string, ConfigFile> config)
    {
        var serial = 0;
        Fixture Start(ControllerActionId action, bool combat = false, ControllerBindingProfile profile = ControllerBindingProfile.Frame, bool left = false) =>
            new(config("action-" + ++serial), action, combat, profile, left);
        foreach (var profile in new[] { ControllerBindingProfile.Frame, ControllerBindingProfile.Touch })
            foreach (var left in new[] { false, true })
                foreach (var enabled in new[] { false, true })
                    foreach (var entry in Actions)
                    {
                        var f = Start(entry.binding, enabled, profile, left);
                        f.Tick(false);
                        f.Tick(true);
                        var held = f.Actions.Held(entry.native);
                        f.Tick(false);
                        var released = f.Actions.Released(entry.native, 0, ClickDelay);
                        var all = f.Hud.AllCleared;
                        f.Tick(false);
                        var observed = entry.delivery switch
                        {
                            Delivery.Held => held,
                            Delivery.Released => released,
                            Delivery.ClearAll => all == 1,
                            _ => f.Actions.Queued(entry.native)
                        };
                        check(observed == (!entry.combat || enabled),
                            $"{profile} {(left ? "left" : "right")} {entry.binding}: combat={enabled} preserves its action contract");
                    }

        foreach (var context in new[] { "grip", "flight", "menu", "wheel", "map", "tracking", "layout" })
        {
            var f = Start(ControllerActionId.Gear);
            f.Tick(false);
            f.Tick(true);
            void Available(bool available)
            {
                switch (context)
                {
                    case "grip":
                        MotionFlightController.Instance.StickHeld = available;
                        break;
                    case "flight":
                        MotionFlightController.Instance.FlightActive = available;
                        break;
                    case "menu":
                        WristMenu.IsOpen = !available;
                        break;
                    case "wheel":
                        NativeHandOptionWheel.IsOpen = !available;
                        break;
                    case "map":
                        CockpitMapInteraction.Reserved = !available;
                        break;
                    case "tracking":
                        f.Valid = available;
                        break;
                    case "layout":
                        CockpitHudWidgets.Editing = !available;
                        break;
                }
            }
            Available(false);
            f.Tick(false);
            Available(true);
            f.Tick(true);
            check(!f.Actions.Queued(NativeControllerAction.Gear), context + " cancellation does not queue a release action");
            f.Tick(false);
            f.Tick(false);
            check(!f.Actions.Queued(NativeControllerAction.Gear), context + " recovery while held requires a fresh press");
            f.Tick(true);
            f.Tick(false);
            f.Tick(false);
            check(f.Actions.Queued(NativeControllerAction.Gear), context + " recovery accepts the next deliberate action");
        }

        var disabled = Start(ControllerActionId.Gear);
        disabled.Tick(false);
        disabled.Tick(true);
        disabled.Tick(false);
        disabled.Invoke("OnDisable");
        disabled.Tick(false);
        check(!disabled.Actions.Queued(NativeControllerAction.Gear), "Disabling the component clears a pending action");

        var toggled = Start(ControllerActionId.Guns, true);
        toggled.Tick(false);
        toggled.Tick(true);
        check(toggled.Actions.GunsHeld, "Combat action initially held");
        toggled.Bindings.CombatEnabled.Value = false;
        toggled.Tick(true);
        check(!toggled.Actions.GunsHeld, "Disabling combat immediately cancels held fire");
        toggled.Bindings.CombatEnabled.Value = true;
        toggled.Tick(true);
        check(!toggled.Actions.GunsHeld, "Re-enabling combat cannot inherit a held fire button");
        toggled.Tick(false);
        toggled.Tick(true);
        check(toggled.Actions.GunsHeld, "Fresh press after combat re-enable fires normally");
        toggled.Invoke("OnDestroy");
        check(ControllerActions.Instance == null, "Destruction clears the controller action owner");
        foreach (var leftHanded in new[] { false, true })
        {
            var f = Start(ControllerActionId.NextCountermeasure, true, ControllerBindingProfile.Touch, leftHanded);
            f.Bindings.ResetProfile();
            f.TickThrottleClick(false, true);
            f.TickThrottleClick(true, true);
            check(!f.Actions.Released(NativeControllerAction.NextCountermeasure), "held Touch click does not cycle until release");
            f.TickThrottleClick(false, true);
            check(f.Actions.Released(NativeControllerAction.NextCountermeasure), "Touch throttle click releases one CM-cycle action in either handedness");
            f.TickThrottleClick(false, true);
            check(!f.Actions.Released(NativeControllerAction.NextCountermeasure), "Touch CM-cycle release never repeats");
            f.TickThrottleClick(false, false);
            f.TickThrottleClick(true, false);
            f.TickThrottleClick(false, false);
            check(!f.Actions.Released(NativeControllerAction.NextCountermeasure), "free-hand thumbstick click stays available to wheel, not CM cycling");
            f.TickThrottleClick(true, false);
            f.TickThrottleClick(true, true);
            f.TickThrottleClick(false, true);
            check(!f.Actions.Released(NativeControllerAction.NextCountermeasure), "gripping while a free-hand click is held cannot synthesize a CM cycle");
            f.Invoke("OnDestroy");
        }
        // The standalone wheel has no native radial-menu state, so
        // recovery-map access must also honor NOVR's own wheel ownership.
        _ = Start(ControllerActionId.Map);
        GameManager.LocalPlayer = new();
        SceneSingleton<DynamicMap>.i = new();
        SceneSingleton<CameraStateManager>.i = new();
        SceneSingleton<GameplayUI>.i = new();
        SceneSingleton<MapOptions>.i = new();
        DynamicMap.mapMaximized = false;
        check(ControllerActions.MapAccessAvailable, "Recovery map remains available in an otherwise valid gameplay context.");
        NativeHandOptionWheel.IsOpen = true;
        ControllerActions.ToggleMap();
        check(!ControllerActions.MapAccessAvailable && !DynamicMap.mapMaximized,
            "A standalone wheel blocks other-hand recovery-map input without relying on a native radial menu.");
        NativeHandOptionWheel.IsOpen = false;
        ControllerActions.ToggleMap();
        check(DynamicMap.mapMaximized, "Recovery-map access returns when the standalone wheel releases ownership.");
        GameManager.LocalPlayer = null;

        foreach (var profile in new[] { ControllerBindingProfile.Frame, ControllerBindingProfile.Touch })
            foreach (var leftHanded in new[] { false, true })
                foreach (var hasPlayer in new[] { false, true })
                {
                    var f = Start(ControllerActionId.Map, true, profile, leftHanded);
                    f.Bindings.ResetProfile();
                    f.Bindings.PointerEnabled.Value = false;
                    GameManager.LocalPlayer = hasPlayer ? new() : null;
                    GameManager.Aircraft = null;
                    GameManager.flightControlsEnabled = false;
                    CameraStateManager.cameraMode = CameraMode.external;
                    // Native level1 starts with an active menu child under an
                    // inactive HUD parent. Its IsInUse result stays true until
                    // the player first spawns, so reproduce that first join.
                    SceneSingleton<RadialMenuMain>.i = new() { isActiveAndEnabled = false };
                    RadialMenuMain.InUse = true;
                    var camera = SceneSingleton<CameraStateManager>.i = new();
                    var target = camera.followingUnit = new object();
                    var map = SceneSingleton<DynamicMap>.i = new();
                    DynamicMap.AllowedToOpen = true;
                    f.TickMap(false);
                    f.TickMap(true);
                    check(DynamicMap.mapMaximized && map.Opens == 1,
                        $"{profile} left={leftHanded} player={hasPlayer}: default map button opens while spectating without a pointer or aircraft");
                    f.TickMap(true);
                    check(DynamicMap.mapMaximized && map.Opens == 1 && map.Closes == 0,
                        "Holding the map button never toggles it repeatedly");
                    f.TickMap(false);
                    f.TickMap(true);
                    check(!DynamicMap.mapMaximized && map.Closes == 1 && camera.followingUnit == target,
                        "Next map press returns to the same spectator target");
                    check(f.UnselectTaps == 0 && f.ClearHolds == 0 && f.Hud.LastCleared == 0 && f.Hud.AllCleared == 0,
                        "Spectator map button cannot dispatch its shared target-unselect binding");
                    f.TickMap(false);
                    DynamicMap.AllowedToOpen = false;
                    f.TickMap(true);
                    check(!DynamicMap.mapMaximized && map.Opens == 1, "Native aircraft-selection lock is respected");
                    DynamicMap.AllowedToOpen = true;
                    f.TickMap(true);
                    check(!DynamicMap.mapMaximized, "A held button is not replayed after a map lock clears");
                    f.TickMap(false);
                    f.TickMap(true);
                    check(DynamicMap.mapMaximized && map.Opens == 2, "Fresh press reopens the spectator map after selection");
                    // Closing must still be possible if an owner locks opening
                    // after the map is already visible.
                    f.TickMap(false);
                    DynamicMap.AllowedToOpen = false;
                    f.TickMap(true);
                    check(!DynamicMap.mapMaximized && map.Closes == 2, "Open map can close while new opens are disallowed");
                    DynamicMap.AllowedToOpen = true;
                    f.Invoke("OnDestroy");
                }

        DynamicMap.mapMaximized = false;
        SceneSingleton<RadialMenuMain>.i!.isActiveAndEnabled = true;
        ControllerActions.ToggleMap();
        check(!DynamicMap.mapMaximized, "A live desktop wheel still owns input and blocks map toggling");
        SceneSingleton<RadialMenuMain>.i.isActiveAndEnabled = false;
        ControllerActions.ToggleMap();
        check(DynamicMap.mapMaximized, "An inactive cockpit wheel cannot block a spectator map toggle");
        SceneSingleton<RadialMenuMain>.i = null;
        RadialMenuMain.InUse = false;

        var staleGrip = Start(ControllerActionId.Map, true);
        staleGrip.Bindings.ResetProfile();
        CameraStateManager.cameraMode = CameraMode.external;
        staleGrip.TickMap(false);
        staleGrip.TickMap(true);
        check(DynamicMap.mapMaximized, "Stale cockpit grip state cannot suppress map access after switching to an external camera");
        staleGrip.TickMap(false);
        check(staleGrip.UnselectTaps == 0 && staleGrip.ClearHolds == 0, "External camera never unselects a combat target on map-button release");
        GameManager.LocalPlayer = null;
        DynamicMap.mapMaximized = false;

        // ---- Outside view: flying the local aircraft from the orbit camera ----
        // No handles exist there, so buttons work as if their handle were held.
        void Outside(bool motion = true)
        {
            var handles = MotionFlightController.Instance;
            handles.FlightActive = handles.StickHeld = handles.ThrottleHeld = false;
            CameraStateManager.cameraMode = CameraMode.external;
            ExternalFlightInput.Active = motion;
            PilotView.External = PilotView.CanGoCockpit = true;
            PilotView.CanGoExternal = false;
        }
        foreach (var profile in new[] { ControllerBindingProfile.Frame, ControllerBindingProfile.Touch })
            foreach (var leftHanded in new[] { false, true })
            {
                var label = $"{profile} left={leftHanded}: ";
                var guns = Start(ControllerActionId.Guns, true, profile, leftHanded);
                Outside();
                guns.Tick(false); guns.Tick(true);
                check(guns.Actions.Held(NativeControllerAction.Guns), label + "outside view fires without a handle to hold");
                guns.Tick(false);
                check(!guns.Actions.Held(NativeControllerAction.Guns), label + "releasing the button stops firing");
                ExternalFlightInput.Active = false;
                guns.Tick(false); guns.Tick(true);
                check(!guns.Actions.Held(NativeControllerAction.Guns), label + "outside view without thumbstick flight keeps flight buttons off");

                var gear = Start(ControllerActionId.Gear, false, profile, leftHanded);
                Outside();
                gear.Tick(false); gear.Tick(true); gear.Tick(false); gear.Tick(false);
                check(gear.Actions.Queued(NativeControllerAction.Gear), label + "queued aircraft actions reach the game from the outside view");
                gear = Start(ControllerActionId.Gear, false, profile, leftHanded);
                Outside();
                gear.Tick(false); gear.Tick(true); gear.Tick(false);
                PilotView.External = false; // spectating someone else, same camera mode
                gear.Tick(false);
                check(!gear.Actions.Queued(NativeControllerAction.Gear), label + "a spectator camera never dispatches aircraft actions");

                // The default fire buttons work out there exactly as in the cockpit:
                // guns on the trigger, missiles and bombs on the release button.
                var trigger = Start(ControllerActionId.Guns, true, profile, leftHanded);
                trigger.Bindings.Set(ControllerActionId.Guns, new(ControllerButton.Trigger, ControllerHandRole.Stick));
                trigger.Bindings.Set(ControllerActionId.WeaponRelease, new(ControllerButton.Primary, ControllerHandRole.Stick));
                Outside();
                trigger.TickTrigger(false); trigger.TickTrigger(true);
                check(trigger.Actions.Held(NativeControllerAction.Guns) && trigger.Actions.GunsHeld && !trigger.Actions.WeaponReleaseHeld,
                    label + "in the outside view the trigger fires guns as bound, and only guns");
                trigger.TickTrigger(false);
                check(!trigger.Actions.GunsHeld, label + "releasing the trigger stops the guns");
                trigger.Tick(false); trigger.Tick(true);
                check(trigger.Actions.Held(NativeControllerAction.WeaponRelease) && trigger.Actions.WeaponReleaseHeld && !trigger.Actions.GunsHeld,
                    label + "in the outside view the release button launches the selected weapon as bound, and never guns");

                // Thumbstick click stays free for the quick wheel.
                var click = Start(ControllerActionId.SelectTarget, true, profile, leftHanded);
                click.Bindings.Set(ControllerActionId.SelectTarget, new(ControllerButton.StickClick, ControllerHandRole.Stick));
                Outside();
                click.TickStickClick(false); click.TickStickClick(true); click.TickStickClick(false);
                check(!click.Actions.Released(NativeControllerAction.Select), label + "thumbstick click is never a flight button in the outside view");

                // The stick hand's B returns to the cockpit and outranks its bindings.
                var unlock = Start(ControllerActionId.UnlockTarget, true, profile, leftHanded);
                unlock.Bindings.Set(ControllerActionId.UnlockTarget, new(ControllerActions.CockpitReturnButton, ControllerHandRole.Stick));
                unlock.Bindings.Set(ControllerActionId.Map, new(ControllerActions.CockpitReturnButton, ControllerHandRole.Stick));
                GameManager.LocalPlayer = new();
                var map = SceneSingleton<DynamicMap>.i = new();
                SceneSingleton<CameraStateManager>.i = new();
                SceneSingleton<GameplayUI>.i = new();
                SceneSingleton<MapOptions>.i = new();
                Outside();
                unlock.TickMap(false); unlock.TickMap(true);
                check(PilotView.CockpitRequests == 1 && CameraStateManager.cameraMode == CameraMode.cockpit && !PilotView.External,
                    label + "B on the stick hand returns from the outside view to the cockpit");
                check(map.Opens == 0 && unlock.UnselectTaps == 0 && unlock.ClearHolds == 0, label + "that press neither opens the map nor unselects a target");
                unlock.TickMap(true); unlock.TickMap(true);
                check(PilotView.CockpitRequests == 1 && map.Opens == 0, label + "holding B through the change does nothing more");
                unlock.TickMap(false);
                check(unlock.UnselectTaps == 0 && unlock.ClearHolds == 0 && map.Opens == 0, label + "releasing B after the change is not a click in the cockpit");
                unlock.TickMap(true);
                check(map.Opens == 1, label + "back in the cockpit with a free hand, a fresh B press is the map button again");
                DynamicMap.mapMaximized = false;

                // The same button on the other hand keeps its binding.
                var other = Start(ControllerActionId.Gear, false, profile, leftHanded);
                other.Bindings.Set(ControllerActionId.Gear, new(ControllerActions.CockpitReturnButton, ControllerHandRole.Throttle));
                Outside();
                other.TickMap(false, throttleHand: true); other.TickMap(true, throttleHand: true);
                other.TickMap(false, throttleHand: true); other.TickMap(false, throttleHand: true);
                check(other.Actions.Queued(NativeControllerAction.Gear) && PilotView.CockpitRequests == 0,
                    label + "only the stick hand's B is reserved; the other hand keeps its binding");

                // A physical HOTAS (motion controls off) still gets the way back.
                var hotas = Start(ControllerActionId.Guns, true, profile, leftHanded);
                Outside(motion: false);
                hotas.TickMap(false); hotas.TickMap(true);
                check(PilotView.CockpitRequests == 1 && CameraStateManager.cameraMode == CameraMode.cockpit,
                    label + "B returns to the cockpit even without thumbstick flight");
                foreach (var blocked in new[] { "wrist menu", "layout", "wheel", "tracking" })
                {
                    var f = Start(ControllerActionId.Guns, true, profile, leftHanded);
                    Outside();
                    f.TickMap(false);
                    if (blocked == "wrist menu") WristMenu.IsOpen = true;
                    if (blocked == "layout") CockpitLayout.Active = true;
                    if (blocked == "wheel") NativeHandOptionWheel.IsOpen = true;
                    if (blocked == "tracking") TrackedControllers.Available = false;
                    f.TickMap(true);
                    check(PilotView.CockpitRequests == 0, label + "B does not change view under: " + blocked);
                    WristMenu.IsOpen = CockpitLayout.Active = NativeHandOptionWheel.IsOpen = false;
                    TrackedControllers.Available = true;
                    f.TickMap(true);
                    check(PilotView.CockpitRequests == 0, label + "a press held through a blocked moment is not replayed: " + blocked);
                }

                // Change view: an optional button that works both ways, no handle needed.
                var view = Start(ControllerActionId.ChangeView, false, profile, leftHanded);
                var handlesFree = MotionFlightController.Instance;
                handlesFree.StickHeld = handlesFree.ThrottleHeld = false;
                PilotView.CanGoExternal = true;
                view.Tick(false); view.Tick(true);
                check(PilotView.ToggleRequests == 1 && CameraStateManager.cameraMode == CameraMode.external,
                    label + "the change-view button leaves the cockpit with free hands");
                view.Tick(true); view.Tick(true);
                check(PilotView.ToggleRequests == 1, label + "holding it does not flip the view back");
                view.Tick(false); view.Tick(true);
                check(PilotView.ToggleRequests == 2 && CameraStateManager.cameraMode == CameraMode.cockpit,
                    label + "a second press returns to the cockpit");
                view = Start(ControllerActionId.ChangeView, false, profile, leftHanded);
                view.Tick(false); view.Tick(true);
                check(PilotView.ToggleRequests == 0, label + "with no view to change to, the button does nothing");
                view = Start(ControllerActionId.ChangeView, false, profile, leftHanded);
                PilotView.CanGoExternal = true;
                WristMenu.IsOpen = true;
                view.Actions.RequestViewChange();
                check(!WristMenu.IsOpen && PilotView.ToggleRequests == 0, label + "the wrist menu's view button closes the menu first");
                view.Tick(false);
                check(PilotView.ToggleRequests == 1 && CameraStateManager.cameraMode == CameraMode.external,
                    label + "and the view changes on the next update");
                view.Tick(false);
                check(PilotView.ToggleRequests == 1, label + "a wrist-menu request is served once");
                view.Actions.RequestViewChange();
                Time.frameCount += 5; // updates missed, for example while editing the HUD layout
                view.Tick(false);
                check(PilotView.ToggleRequests == 1, label + "a stale wrist-menu request never changes the view later");
            }
        GameManager.LocalPlayer = null;
        PilotView.Reset();
        ExternalFlightInput.Active = false;
        CameraStateManager.cameraMode = CameraMode.cockpit;

        // Binding rules for the two new actions.
        foreach (var profile in new[] { ControllerBindingProfile.Frame, ControllerBindingProfile.Touch })
        {
            var frame = profile == ControllerBindingProfile.Frame;
            check(ControllerBindingDefaults.Get(profile, ControllerActionId.ChangeView).Button == ControllerButton.None &&
                  ControllerBindingDefaults.Get(profile, ControllerActionId.NavLights).Button == ControllerButton.None,
                $"{profile}: change view and nav lights start unassigned, so no existing layout changes");
            check(!ControllerBindingDefaults.RequiresGrip(ControllerActionId.ChangeView) && ControllerBindingDefaults.RequiresGrip(ControllerActionId.NavLights),
                "Change view works with free hands; nav lights is a flight button");
            check(!ControllerBindingDefaults.IsBindingAvailable(profile, false, ControllerActionId.ChangeView, ControllerButton.Trigger) &&
                  !ControllerBindingDefaults.IsBindingAvailable(profile, false, ControllerActionId.ChangeView, ControllerButton.StickClick) &&
                  ControllerBindingDefaults.IsBindingAvailable(profile, false, ControllerActionId.ChangeView, ControllerButton.Primary) &&
                  ControllerBindingDefaults.IsBindingAvailable(profile, false, ControllerActionId.NavLights, ControllerButton.StickClick),
                $"{profile}: change view cannot take the pointer trigger or the quick-wheel click");
            var a = new ControllerActionBinding(ControllerButton.Primary, ControllerHandRole.Stick);
            check(ControllerBindingDefaults.Conflict(ControllerActionId.ChangeView, a, ControllerActionId.WeaponRelease, a, false, frame) &&
                  ControllerBindingDefaults.Conflict(ControllerActionId.ChangeView, a, ControllerActionId.Map, a, false, frame) &&
                  ControllerBindingDefaults.Conflict(ControllerActionId.ChangeView, a, ControllerActionId.WristMenu, a, false, frame),
                $"{profile}: change view needs a button of its own");
            check(!ControllerBindingDefaults.Conflict(ControllerActionId.NavLights, a, ControllerActionId.Map, a, false, frame) &&
                  ControllerBindingDefaults.Conflict(ControllerActionId.NavLights, a, ControllerActionId.Gear, a, false, frame),
                $"{profile}: nav lights follows the rules of the other flight buttons");
        }
        check(ControllerBindingDefaults.ActionLabel(ControllerActionId.ChangeView) == "Change view" &&
              ControllerBindingDefaults.ActionLabel(ControllerActionId.NavLights) == "Nav lights", "The new actions have plain labels");
        check((int)ControllerActionId.ChangeView == 15 && (int)ControllerActionId.NavLights == 16 && (int)ControllerActionId.HoverAssist == 14 &&
              (int)ControllerActionId.FireSelected == 17,
            "New actions are appended; existing action ids, and with them saved settings, do not move");
        TapHoldChecks(check, (action, combat, profile, left) => Start(action, combat, profile, left));
    }

    // ---- 0.4.8.35: the game's own tap/hold on Select and Cancel, and a single fire button ----
    private static void TapHoldChecks(Action<bool, string> check,
        Func<ControllerActionId, bool, ControllerBindingProfile, bool, Fixture> start)
    {
        foreach (var profile in new[] { ControllerBindingProfile.Frame, ControllerBindingProfile.Touch })
        {
            check(ControllerBindingDefaults.Get(profile, ControllerActionId.FireSelected).Button == ControllerButton.None &&
                  ControllerBindingDefaults.Get(profile, ControllerActionId.FireSelected).Hand == ControllerHandRole.Stick &&
                  ControllerBindingDefaults.Get(profile, ControllerActionId.Guns).Button == ControllerButton.Trigger &&
                  ControllerBindingDefaults.Get(profile, ControllerActionId.WeaponRelease).Button == ControllerButton.Primary,
                $"{profile}: fire selected weapon starts unassigned; the split guns and release layout stays the default");
            var a = new ControllerActionBinding(ControllerButton.Trigger, ControllerHandRole.Stick);
            check(ControllerBindingDefaults.RequiresGrip(ControllerActionId.FireSelected) &&
                  ControllerBindingDefaults.IsBindingAvailable(profile, false, ControllerActionId.FireSelected, ControllerButton.Trigger) &&
                  ControllerBindingDefaults.Conflict(ControllerActionId.FireSelected, a, ControllerActionId.Guns, a, false, profile == ControllerBindingProfile.Frame),
                $"{profile}: fire selected weapon is a flight button, may use the trigger, and needs a button of its own");
        }
        check(ControllerBindingDefaults.ActionLabel(ControllerActionId.FireSelected) == "Fire selected weapon",
            "The new action has a plain label");
        check(ControllerBindingDefaults.ContextLabel(ControllerActionId.SelectTarget).Contains("hold") &&
              ControllerBindingDefaults.ContextLabel(ControllerActionId.UnlockTarget).Contains("hold") &&
              ControllerBindingDefaults.ContextLabel(ControllerActionId.FireSelected) == ControllerBindingDefaults.ContextLabel(ControllerActionId.Guns),
            "Select and unselect explain their hold; the single fire button is held like the split ones");

        foreach (var profile in new[] { ControllerBindingProfile.Frame, ControllerBindingProfile.Touch })
            foreach (var leftHanded in new[] { false, true })
            {
                var label = $"{profile} left={leftHanded}: ";

                // One fire button for whatever is selected.
                var fire = start(ControllerActionId.FireSelected, true, profile, leftHanded);
                fire.Tick(false); fire.Tick(true);
                check(fire.Actions.Held(NativeControllerAction.Fire) && fire.Actions.FireSelectedHeld &&
                      !fire.Actions.GunsHeld && !fire.Actions.WeaponReleaseHeld,
                    label + "the single fire button holds the game's Fire and is neither the guns nor the release button");
                fire.Tick(false);
                check(!fire.Actions.Held(NativeControllerAction.Fire) && !fire.Actions.FireSelectedHeld, label + "releasing it stops firing");
                fire.Bindings.CombatEnabled.Value = false;
                fire.Tick(false); fire.Tick(true);
                check(!fire.Actions.Held(NativeControllerAction.Fire), label + "it is a combat control like the others");

                // Select: tap, hold, and the overlap the game itself has.
                foreach (var (action, native) in new[] { (ControllerActionId.SelectTarget, NativeControllerAction.Select),
                             (ControllerActionId.UnlockTarget, NativeControllerAction.Cancel) })
                {
                    var name = label + action + ": ";
                    var f = start(action, true, profile, leftHanded);
                    f.Tick(false);
                    f.Tick(true, 0f);
                    check(!f.Actions.HoldReached(native, PressDelay) && !f.Actions.Released(native, 0, ClickDelay), name + "a fresh press is neither a tap nor a hold yet");
                    f.Tick(true, .1f);
                    check(!f.Actions.HoldReached(native, PressDelay), name + "a press shorter than the press delay is not a hold");
                    f.Tick(false, .12f);
                    check(f.Actions.Released(native, 0, ClickDelay) && !f.Actions.HoldReached(native, PressDelay), name + "a quick release is a tap");
                    f.Tick(false, .13f);
                    check(!f.Actions.Released(native, 0, ClickDelay), name + "a tap is reported for one frame only");

                    f.Tick(true, 1f);
                    f.Tick(true, 1.14f);
                    check(!f.Actions.HoldReached(native, PressDelay), name + "the hold waits for the game's press delay");
                    f.Tick(true, 1.16f);
                    check(f.Actions.HoldReached(native, PressDelay), name + "the hold acts on the frame the delay is reached");
                    f.Tick(true, 1.2f);
                    f.Tick(true, 1.5f);
                    check(!f.Actions.HoldReached(native, PressDelay), name + "a continuing hold acts once");
                    f.Tick(false, 1.6f);
                    check(!f.Actions.Released(native, 0, ClickDelay), name + "releasing a long hold is not also a tap");
                    check(f.Actions.Released(native, 0, 0) && f.Actions.Released(native), name + "a zero expiry never expires, as in the game's input library");
                    check(!f.Actions.Released(native, 1f, 0), name + "a minimum hold time is honoured");

                    // The game's defaults overlap: 0.15 s to hold, 0.25 s to tap.
                    f.Tick(true, 2f);
                    f.Tick(true, 2.2f);
                    var hold = f.Actions.HoldReached(native, PressDelay);
                    f.Tick(false, 2.22f);
                    check(hold && f.Actions.Released(native, 0, ClickDelay), name + "a press between the two delays is both, exactly as on a keyboard");

                    // A press the gate cancels is neither.
                    foreach (var cancel in new[] { "grip", "tracking", "menu", "editing" })
                    {
                        f = start(action, true, profile, leftHanded);
                        f.Tick(false); f.Tick(true, 3f);
                        if (cancel == "grip") MotionFlightController.Instance.StickHeld = false;
                        if (cancel == "tracking") f.Valid = false;
                        if (cancel == "menu") WristMenu.IsOpen = true;
                        if (cancel == "editing") CockpitHudWidgets.Editing = true;
                        f.Tick(true, 3.05f);
                        var tapped = f.Actions.Released(native, 0, ClickDelay);
                        MotionFlightController.Instance.StickHeld = true;
                        f.Valid = true;
                        WristMenu.IsOpen = CockpitHudWidgets.Editing = false;
                        f.Tick(true, 3.4f);
                        check(!tapped && !f.Actions.HoldReached(native, PressDelay) && !f.Actions.HoldReached(native, 0f),
                            name + "a press cancelled by " + cancel + " is not a tap, and holding through it is not a hold");
                        f.Tick(false, 3.45f);
                        check(!f.Actions.Released(native, 0, ClickDelay), name + "nor is its release a tap (" + cancel + ")");
                        f.Tick(true, 3.5f); f.Tick(false, 3.55f);
                        check(f.Actions.Released(native, 0, ClickDelay), name + "the next deliberate tap works (" + cancel + ")");
                    }
                    check(f.Hud.LastCleared == 0 && f.Hud.AllCleared == 0, name + "neither button calls the HUD directly; the game's own handling acts on the answer");
                }

                // Clear all targets, and the wrist-menu buttons, take the game's full path.
                var clear = start(ControllerActionId.ClearTargets, true, profile, leftHanded);
                var cam = new TargetCam();
                clear.Hud.aircraft = new Aircraft { targetCam = cam };
                var icons = SceneSingleton<DynamicMap>.i = new();
                clear.Tick(false); clear.Tick(true); clear.Tick(false);
                check(clear.Hud.AllCleared == 1 && clear.Hud.AllClearedWithAudio == 1 && icons.IconsCleared == 1 && cam.Cancelled == 1,
                    label + "clear all targets also clears the map icons and the target camera, with the game's sound");
                ControllerActions.UnselectLatestTarget();
                check(clear.Hud.LastCleared == 1 && cam.Cancelled == 2 && clear.Hud.AllCleared == 1, label + "the wrist menu's unselect resets the target camera too");
                clear.Hud.aircraft = null;
                ControllerActions.ClearAllTargets();
                ControllerActions.UnselectLatestTarget();
                check(clear.Hud.AllCleared == 2 && clear.Hud.LastCleared == 2 && cam.Cancelled == 2, label + "no aircraft or target camera is not an error");
                SceneSingleton<CombatHUD>.i = null;
                SceneSingleton<DynamicMap>.i = null;
                ControllerActions.ClearAllTargets();
                ControllerActions.UnselectLatestTarget();
                check(clear.Hud.AllCleared == 2 && clear.Hud.LastCleared == 2, label + "no HUD means nothing to clear");
            }

        // The timer on its own.
        var gate = new ControllerButtonGate();
        var timer = new ControllerPressTimer();
        void Step(bool available, float value, float now)
        {
            gate.Update(available, value);
            timer.Update(gate, now);
        }
        Step(true, 0, 10f);
        check(!timer.Tapped(0, 0) && !timer.HoldReached(0), "an idle button is neither a tap nor a hold, even with zero thresholds");
        Step(true, 1, 10f);
        check(timer.HoldReached(0) && !timer.HoldReached(.01f), "a zero press delay acts on the press itself");
        Step(true, 1, 10.5f);
        check(!timer.HoldReached(0) && timer.HoldReached(.5f) && !timer.HoldReached(float.NaN) && !timer.HoldReached(-1f),
            "each delay is reached once; a NaN or negative delay never repeats");
        Step(true, 0, 10.75f); // exactly representable, so the limits can be tested at equality
        check(timer.Tapped(.75f, 0) && !timer.Tapped(.76f, 0) && timer.Tapped(0, .75f) && !timer.Tapped(0, .74f) && !timer.Tapped(float.NaN, 0),
            "tap limits are inclusive and a NaN minimum never taps");
        Step(true, 1, 5f);
        Step(true, 1, 4f);
        Step(true, 0, 3f);
        check(timer.Tapped(0, .25f), "a clock that runs backwards reads as a zero-length press, not as a negative one");
        Step(true, 1, 20f);
        timer.Reset();
        gate.Reset();
        Step(true, 1, 20.4f);
        check(!timer.HoldReached(PressDelay), "after a reset a still-held button is not a hold");
        Step(true, 0, 20.5f);
        check(!timer.Tapped(0, 0), "and its release is not a tap");
    }

    private sealed class Fixture
    {
        internal readonly ControllerActions Actions = new();
        internal readonly ControllerBindings Bindings;
        internal readonly CombatHUD Hud = new();
        internal bool Valid = true;
        // What the game's own Cancel handling would have acted on, summed over every update.
        internal int UnselectTaps, ClearHolds;
        private readonly bool _left, _frame;
        internal Fixture(ConfigFile config, ControllerActionId action, bool combat, ControllerBindingProfile profile, bool left)
        {
            _left = left;
            _frame = profile == ControllerBindingProfile.Frame;
            TrackedControllers.Available = true;
            ModConfiguration.Instance.MotionControls.LeftHanded.Value = left;
            Bindings = new ControllerBindings(config);
            Bindings.Profile.Value = profile;
            foreach (var id in Enum.GetValues<ControllerActionId>())
                Bindings.Set(id, new(ControllerButton.None, ControllerHandRole.Stick));
            Bindings.Set(action, new(ControllerButton.Primary, ControllerHandRole.Stick));
            Bindings.CombatEnabled.Value = combat;
            ModConfiguration.Instance.ControllerBindings = Bindings;
            MotionFlightController.Instance = new();
            GameManager.Aircraft = new();
            GameManager.flightControlsEnabled = true;
            Time.timeScale = 1;
            Cursor.visible = WristMenu.IsOpen = NativeHandOptionWheel.IsOpen = CockpitHudWidgets.Editing = false;
            CockpitMapInteraction.Reserved = CockpitLayout.Active = DynamicMap.mapMaximized = false;
            CursorManager.Flags = CursorFlags.None;
            CameraStateManager.cameraMode = CameraMode.cockpit;
            ExternalFlightInput.Active = false;
            PilotView.Reset();
            Time.unscaledTime = 0;
            SceneSingleton<CombatHUD>.i = Hud;
            Invoke("Awake");
        }
        internal void TickTrigger(bool pressed)
        {
            Time.frameCount++;
            TrackedControllers.Samples[0] = TrackedControllers.Samples[1] = new ControllerSample { Valid = Valid, NativeFrame = _frame };
            TrackedControllers.Samples[_left ? 0 : 1].Trigger = pressed ? 1 : 0;
            Invoke("Update");
        }
        internal void TickStickClick(bool pressed)
        {
            Time.frameCount++;
            TrackedControllers.Samples[0] = TrackedControllers.Samples[1] = new ControllerSample { Valid = Valid, NativeFrame = _frame };
            TrackedControllers.Samples[_left ? 0 : 1].StickClick = pressed;
            Invoke("Update");
        }
        internal void Tick(bool pressed, float now)
        {
            Time.unscaledTime = now;
            Tick(pressed);
        }
        internal void Tick(bool pressed)
        {
            Time.frameCount++;
            TrackedControllers.Samples[0] = TrackedControllers.Samples[1] = new ControllerSample { Valid = Valid, NativeFrame = _frame };
            TrackedControllers.Samples[_left ? 0 : 1].Primary = pressed;
            Invoke("Update");
        }
        internal void TickThrottleClick(bool pressed, bool held)
        {
            Time.frameCount++;
            TrackedControllers.Samples[0] = TrackedControllers.Samples[1] = new ControllerSample { Valid = true, NativeFrame = false };
            TrackedControllers.Samples[_left ? 1 : 0].StickClick = pressed;
            MotionFlightController.Instance!.ThrottleHeld = held;
            Invoke("Update");
        }
        internal void TickMap(bool pressed, bool throttleHand = false)
        {
            Time.frameCount++;
            TrackedControllers.Samples[0] = TrackedControllers.Samples[1] = new ControllerSample { Valid = Valid, NativeFrame = _frame };
            var hand = _left != throttleHand ? 0 : 1;
            if (_frame)
                TrackedControllers.Samples[hand].Outside = pressed;
            else
                TrackedControllers.Samples[hand].Secondary = pressed;
            Invoke("Update");
        }
        internal void Invoke(string name)
        {
            typeof(ControllerActions).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Actions, null);
            if (name != "Update")
                return;
            if (Actions.Released(NativeControllerAction.Cancel, 0, ClickDelay))
                UnselectTaps++;
            if (Actions.HoldReached(NativeControllerAction.Cancel, PressDelay))
                ClearHolds++;
        }
    }
}
