using System.Reflection;
using BepInEx.Configuration;
using NOVR.MotionControls;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class ActionChecks
{
    private enum Delivery
    {
        Held, Released, Queued, ClearLast, ClearAll
    }
    private static readonly (ControllerActionId binding, NativeControllerAction native, Delivery delivery, bool combat)[] Actions =
    {
        (ControllerActionId.Brake, NativeControllerAction.Brake, Delivery.Held, false),
        (ControllerActionId.Gear, NativeControllerAction.Gear, Delivery.Queued, false),
        (ControllerActionId.FlightAssist, NativeControllerAction.FlightAssist, Delivery.Queued, false),
        (ControllerActionId.HoverAssist, NativeControllerAction.AutoHover, Delivery.Queued, false),
        (ControllerActionId.Guns, NativeControllerAction.Guns, Delivery.Held, true),
        (ControllerActionId.WeaponRelease, NativeControllerAction.WeaponRelease, Delivery.Held, true),
        (ControllerActionId.Countermeasures, NativeControllerAction.Countermeasures, Delivery.Held, true),
        (ControllerActionId.SelectTarget, NativeControllerAction.Select, Delivery.Released, true),
        (ControllerActionId.NextWeapon, NativeControllerAction.NextWeapon, Delivery.Released, true),
        (ControllerActionId.NextCountermeasure, NativeControllerAction.NextCountermeasure, Delivery.Released, true),
        (ControllerActionId.Radar, NativeControllerAction.Radar, Delivery.Queued, true),
        (ControllerActionId.UnlockTarget, NativeControllerAction.Unknown, Delivery.ClearLast, true),
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
                        var released = f.Actions.Released(entry.native);
                        var last = f.Hud.LastCleared;
                        var all = f.Hud.AllCleared;
                        f.Tick(false);
                        var observed = entry.delivery switch
                        {
                            Delivery.Held => held,
                            Delivery.Released => released,
                            Delivery.ClearLast => last == 1,
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
    }

    private sealed class Fixture
    {
        internal readonly ControllerActions Actions = new();
        internal readonly ControllerBindings Bindings;
        internal readonly CombatHUD Hud = new();
        internal bool Valid = true;
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
            SceneSingleton<CombatHUD>.i = Hud;
            Invoke("Awake");
        }
        internal void Tick(bool pressed)
        {
            Time.frameCount++;
            TrackedControllers.Samples[0] = TrackedControllers.Samples[1] = new ControllerSample { Valid = Valid, NativeFrame = _frame };
            TrackedControllers.Samples[_left ? 0 : 1].Primary = pressed;
            Invoke("Update");
        }
        internal void Invoke(string name) => typeof(ControllerActions).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Actions, null);
    }
}
