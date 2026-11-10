using HarmonyLib;
using NuclearOption.UI;
using NuclearOption.MissionEditorScripts;
using UnityEngine;
using NOVR.VrCamera;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;

namespace NOVR.MotionControls;

[DefaultExecutionOrder(-1050)]
internal sealed class ControllerActions : MonoBehaviour
{
    internal static ControllerActions? Instance
    {
        get; private set;
    }

    private static readonly ControllerActionId[] ActionIds = (ControllerActionId[])System.Enum.GetValues(typeof(ControllerActionId));
    internal bool TargetSelecting => !CockpitHudWidgets.Editing && _target.Held;

    private readonly ControllerButtonGate _fire = new(), _release = new(), _countermeasures = new(), _weapon = new(), _target = new(), _nextCountermeasure = new(), _map = new(), _clear = new(), _brake = new(), _unlock = new();
    internal bool GunsHeld => Held(NativeControllerAction.Guns);
    internal bool WeaponReleaseHeld => Held(NativeControllerAction.WeaponRelease);

    private readonly ControllerButtonGate _gear = new(), _radar = new(), _assist = new(), _hover = new(), _lights = new();
    private readonly ControllerButtonGate _view = new(), _cockpit = new();
    // From the outside view the stick hand's B (Frame B, Touch B) always
    // returns to the cockpit. It needs no binding, so it works on a fresh
    // install and after any custom layout.
    internal const ControllerButton CockpitReturnButton = ControllerButton.OutsideOrSecondary;
    private int _bindingVersion = -1;
    private ControllerBindingProfile _bindingProfile;
    private bool _bindingLeftHanded;
    private readonly FrameActionQueue<NativeControllerAction, Aircraft> _queuedActions = new();
    private int _viewRequestFrame = -1;
    private void Awake() => Instance = this;
    // From the wrist menu: change view on the next update, once the menu has
    // closed. The request is a frame stamp, not a button, so closing the menu
    // (which resets buttons) cannot lose it and a missed update cannot replay it.
    internal void RequestViewChange()
    {
        WristMenu.Close();
        _viewRequestFrame = CockpitHudWidgets.Editing ? -1 : Time.frameCount;
    }
    internal void QueueWristAction(NativeControllerAction action)
    {
        QueueNativeAction(action);
        WristMenu.Close();
    }

    internal void QueueNativeAction(NativeControllerAction action)
    {
        if (CockpitHudWidgets.Editing)
            return;
        if (!NativeControllerActionNames.CanQueue(action))
            return;
        if (!GameManager.GetLocalAircraft(out var aircraft))
            return;
        _queuedActions.Enqueue(action, aircraft, Time.frameCount);
    }

    // Queued native actions may coexist with our one-hand option wheel. This is
    // deliberately less restrictive than opening the full recovery map below.
    private static bool CanDispatchQueuedActions =>
        TrackedControllers.Available && Time.timeScale > 0 && GameManager.flightControlsEnabled &&
        !CursorManager.GetFlag(CursorFlags.GameMenu) &&
        (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen) &&
        !DynamicMap.mapMaximized && !WristMenu.IsOpen && !CockpitLayout.Active && !CockpitHudWidgets.Editing &&
        (SceneSingleton<RadialMenuMain>.i == null || !RadialMenuMain.IsInUse() || NativeHandOptionWheel.IsOpen) &&
        (CameraStateManager.cameraMode == CameraMode.cockpit || PilotView.External);

    internal bool Queued(NativeControllerAction action) =>
        CanDispatchQueuedActions && GameManager.GetLocalAircraft(out var aircraft) &&
        _queuedActions.Contains(action, aircraft, Time.frameCount);

    private void Update()
    {
        CockpitMapInteraction.UpdateNavigation();
        // Cancel rather than release: ending a HUD drag must not fire a weapon,
        // replay an eject request, or inherit the grip used to move a widget.
        if (CockpitHudWidgets.Editing)
        {
            _viewRequestFrame = -1;
            ResetButtons();
            return;
        }

        var config = ModConfiguration.Instance;
        var bindings = config.ControllerBindings;
        var leftHanded = config.MotionControls.LeftHanded.Value;
        var profile = bindings.EffectiveProfile;
        if (_bindingVersion != bindings.ChangeVersion || _bindingProfile != profile || _bindingLeftHanded != leftHanded)
        {
            ResetButtons();
            _bindingVersion = bindings.ChangeVersion;
            _bindingProfile = profile;
            _bindingLeftHanded = leftHanded;
        }

        var controls = MotionFlightController.Instance;
        var flightControls = controls != null && controls.FlightActive && CameraStateManager.cameraMode == CameraMode.cockpit &&
            !Cursor.visible && !WristMenu.IsOpen;
        // The outside view has no handles to hold, so there every flight button
        // works as if its handle were held. Three inputs are spoken for: the
        // triggers (rudder, as on the game's gamepad layout), thumbstick click
        // (the quick wheel) and the stick hand's B (cockpit).
        var outside = ExternalFlightInput.Sample() && !Cursor.visible;
        bool OutsideReserved(bool left, ControllerButton button, bool frame)
        {
            var resolved = ControllerButtonNames.Resolve(button, frame);
            return resolved == ControllerButton.Trigger || resolved == ControllerButton.StickClick ||
                left == leftHanded && ControllerButtonNames.SamePhysicalButton(button, CockpitReturnButton, frame);
        }
        bool HandleHeld(bool left) => controls != null && (left == leftHanded ? controls.StickHeld : controls.ThrottleHeld);
        void UpdateFlightButton(ControllerActionId action, ControllerButtonGate gate, bool combat = true)
        {
            var binding = bindings.Get(action);
            var left = ControllerBindings.ResolveLeft(binding.Hand, leftHanded);
            var sample = TrackedControllers.Read(left);
            var held = outside ? !OutsideReserved(left, binding.Button, sample.NativeFrame)
                : flightControls && !CockpitMapInteraction.ReservesButton(left, binding.Button, sample.NativeFrame) && HandleHeld(left);
            var available = held && (!combat || bindings.CombatEnabled.Value) && sample.Valid &&
                !NativeHandOptionWheel.OwnsHand(left);
            gate.Update(available, bindings.ReadFlight(sample, left, binding.Button));
        }

        UpdateFlightButton(ControllerActionId.Brake, _brake, false);
        UpdateFlightButton(ControllerActionId.Guns, _fire);
        UpdateFlightButton(ControllerActionId.WeaponRelease, _release);
        UpdateFlightButton(ControllerActionId.Countermeasures, _countermeasures);
        UpdateFlightButton(ControllerActionId.NextWeapon, _weapon);
        UpdateFlightButton(ControllerActionId.SelectTarget, _target);
        UpdateFlightButton(ControllerActionId.NextCountermeasure, _nextCountermeasure);
        UpdateFlightButton(ControllerActionId.ClearTargets, _clear);
        UpdateFlightButton(ControllerActionId.UnlockTarget, _unlock);
        UpdateFlightButton(ControllerActionId.Gear, _gear, false);
        UpdateFlightButton(ControllerActionId.Radar, _radar);
        UpdateFlightButton(ControllerActionId.FlightAssist, _assist, false);
        UpdateFlightButton(ControllerActionId.HoverAssist, _hover, false);
        UpdateFlightButton(ControllerActionId.NavLights, _lights, false);
        if (_lights.Released)
            QueueNativeAction(NativeControllerAction.NavLights);
        if (_unlock.Released)
            SceneSingleton<CombatHUD>.i?.DeselectLast();
        if (_clear.Released)
            SceneSingleton<CombatHUD>.i?.DeselectAll();
        if (_gear.Released)
            QueueNativeAction(NativeControllerAction.Gear);
        if (_radar.Released)
            QueueNativeAction(NativeControllerAction.Radar);
        if (_assist.Released)
            QueueNativeAction(NativeControllerAction.FlightAssist);
        if (_hover.Released)
            QueueNativeAction(NativeControllerAction.AutoHover);
        // Camera buttons need no handle: a free hand in the cockpit, either hand
        // outside. Both recheck the native camera state when they act.
        var idle = TrackedControllers.Available && !WristMenu.IsOpen && !CockpitLayout.Active;
        var stickHand = TrackedControllers.Read(leftHanded);
        var cockpitReturn = idle && PilotView.CanGoCockpit && stickHand.Valid && !NativeHandOptionWheel.OwnsHand(leftHanded);
        _cockpit.Update(cockpitReturn, ControllerBindings.Read(stickHand, CockpitReturnButton));
        var viewBinding = bindings.Get(ControllerActionId.ChangeView);
        var viewLeft = ControllerBindings.ResolveLeft(viewBinding.Hand, leftHanded);
        var viewHand = TrackedControllers.Read(viewLeft);
        _view.Update(idle && (PilotView.CanGoExternal || PilotView.CanGoCockpit) && viewHand.Valid &&
            !NativeHandOptionWheel.OwnsHand(viewLeft) &&
            !CockpitMapInteraction.ReservesButton(viewLeft, viewBinding.Button, viewHand.NativeFrame),
            bindings.ReadFlight(viewHand, viewLeft, viewBinding.Button));
        var viewRequested = _viewRequestFrame >= 0 && Time.frameCount - _viewRequestFrame <= 1;
        _viewRequestFrame = -1;
        if (_cockpit.Pressed && PilotView.ToCockpit() || (_view.Pressed || viewRequested) && PilotView.Toggle())
        {
            // A button held across the change must not carry into the new view.
            MotionFlightController.Instance?.ResetGrips();
            ResetButtons();
            return;
        }
        // Map may intentionally share a combat button. The assigned hand's held
        // handle takes priority; the recovery map remains available after a crash.
        var mapBinding = bindings.Get(ControllerActionId.Map);
        var mapLeft = ControllerBindings.ResolveLeft(mapBinding.Hand, leftHanded);
        var mapHand = TrackedControllers.Read(mapLeft);
        var mapSharesCombat = false;
        foreach (var action in ActionIds)
        {
            if (action == ControllerActionId.Map || action == ControllerActionId.WristMenu)
                continue;
            var candidate = bindings.Get(action);
            if (ControllerBindings.ResolveLeft(candidate.Hand, leftHanded) == mapLeft &&
                ControllerButtonNames.SamePhysicalButton(mapBinding.Button, candidate.Button, mapHand.NativeFrame))
            {
                mapSharesCombat = true;
                break;
            }
        }

        var liveHandleHeld = (outside || flightControls && HandleHeld(mapLeft)) &&
            GameManager.GetLocalAircraft(out var mapAircraft) && !mapAircraft.disabled;
        // The map button is navigation, not laser-pointer input. It remains
        // usable with the pointer disabled and while following a spectator target.
        // The cockpit-return button outranks a map binding on the same button;
        // the quick wheel still opens the map from the outside view.
        var returnOwnsMapButton = cockpitReturn && mapLeft == leftHanded &&
            ControllerButtonNames.SamePhysicalButton(mapBinding.Button, CockpitReturnButton, mapHand.NativeFrame);
        var mapButtonAvailable = TrackedControllers.Available && mapHand.Valid && !returnOwnsMapButton &&
            !NativeHandOptionWheel.OwnsHand(mapLeft) &&
            !CockpitMapInteraction.ReservesButton(mapLeft, mapBinding.Button, mapHand.NativeFrame) &&
            MapAccessAvailable && !WristMenu.IsOpen && !CockpitLayout.Active && !(mapSharesCombat && liveHandleHeld);
        var mapButtonValue = ControllerButtonNames.Resolve(mapBinding.Button, mapHand.NativeFrame) == ControllerButton.Trigger
            ? 0 : bindings.ReadFlight(mapHand, mapLeft, mapBinding.Button);
        _map.Update(mapButtonAvailable, mapButtonValue);
        if (_map.Pressed)
            ToggleMap();
    }

    // Match native map access: a spectator may have no cached local Player at
    // all. The map itself decides which spawn/spectator options to show.
    // Unlike queued actions, it must not open over any native radial menu.
    internal static bool MapAccessAvailable =>
        !CockpitHudWidgets.Editing && !NativeHandOptionWheel.IsOpen &&
        (GameManager.gameState == GameState.SinglePlayer || GameManager.gameState == GameState.Multiplayer) &&
        SceneSingleton<DynamicMap>.i != null && SceneSingleton<CameraStateManager>.i != null &&
        SceneSingleton<GameplayUI>.i != null && SceneSingleton<MapOptions>.i != null && SceneSingleton<CombatHUD>.i != null &&
        (DynamicMap.mapMaximized || DynamicMap.AllowedToOpen) && !CursorManager.GetFlag(CursorFlags.GameMenu) &&
        !LeaderboardMenu.IsOpen() && !InputFieldChecker.InsideInputField &&
        !NativeRadialMenuState.IsBlockingInput &&
        (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen);

    internal static void ToggleMap()
    {
        if (!MapAccessAvailable)
            return;
        var map = SceneSingleton<DynamicMap>.i;
        if (map == null)
            return;
        WristMenu.Close();
        MotionFlightController.Instance?.ResetGrips();
        if (DynamicMap.mapMaximized)
            map.Minimize();
        else
            map.Maximize();
    }

    private static bool CanReadFlightButtons => TrackedControllers.Available && !WristMenu.IsOpen && !CockpitHudWidgets.Editing && !Cursor.visible;

    internal bool Held(NativeControllerAction action) => CanReadFlightButtons && action switch
    {
        NativeControllerAction.Brake => _brake.Held,
        NativeControllerAction.Fire => _fire.Held || _release.Held,
        NativeControllerAction.Guns => _fire.Held,
        NativeControllerAction.WeaponRelease => _release.Held,
        NativeControllerAction.Countermeasures => _countermeasures.Held,
        _ => false
    };
    internal bool Released(NativeControllerAction action) => CanReadFlightButtons && action switch
    {
        NativeControllerAction.NextWeapon => _weapon.Released,
        NativeControllerAction.Select => _target.Released,
        NativeControllerAction.NextCountermeasure => _nextCountermeasure.Released,
        _ => false
    };
    internal void ResetButtons()
    {
        _queuedActions.Clear();
        _fire.Reset();
        _release.Reset();
        _countermeasures.Reset();
        _weapon.Reset();
        _target.Reset();
        _nextCountermeasure.Reset();
        _map.Reset();
        _clear.Reset();
        _brake.Reset();
        _unlock.Reset();
        _gear.Reset();
        _radar.Reset();
        _assist.Reset();
        _hover.Reset();
        _lights.Reset();
        _view.Reset();
        _cockpit.Reset();
    }

    private void OnDisable() => ResetButtons();
    private void OnDestroy()
    {
        OnDisable();
        if (Instance == this)
            Instance = null;
    }
}

// Feed the game's existing action processing so weapon safety, G-LOC, radar
// altitude checks and multiplayer commands continue to be applied by the game.
[HarmonyPatch(typeof(Rewired.Player), "GetButton", new[] { typeof(string) })]
internal static class ControllerHeldActionPatch
{
    [HarmonyPostfix]
    private static void Postfix(Rewired.Player __instance, string __0, ref bool __result)
    {
        if (__instance != GameManager.playerInput)
            return;
        var action = NativeControllerActionNames.Parse(__0);
        __result |= ControllerActions.Instance?.Held(action) == true;
    }
}

[HarmonyPatch(typeof(Rewired.Player), "GetButtonTimedPressUp", new[] { typeof(string), typeof(float), typeof(float) })]
internal static class ControllerReleasedActionPatch
{
    [HarmonyPostfix]
    private static void Postfix(Rewired.Player __instance, string __0, ref bool __result)
    {
        if (__instance != GameManager.playerInput)
            return;
        var action = NativeControllerActionNames.Parse(__0);
        __result |= ControllerActions.Instance?.Released(action) == true || ControllerActions.Instance?.Queued(action) == true;
    }
}

[HarmonyPatch(typeof(Rewired.Player), "GetButtonDown", new[] { typeof(string) })]
internal static class ControllerPressedActionPatch
{
    [HarmonyPostfix]
    private static void Postfix(Rewired.Player __instance, string __0, ref bool __result)
    {
        if (__instance != GameManager.playerInput)
            return;
        var action = NativeControllerActionNames.Parse(__0);
        if (action is NativeControllerAction.Gear or NativeControllerAction.Eject)
            __result |= ControllerActions.Instance?.Queued(action) == true;
    }
}

// Hover assist is the native long-press branch of Flight Assist. Expose it as
// its own deliberate VR action without also toggling ordinary flight assist.
[HarmonyPatch(typeof(Rewired.Player), "GetButtonTimedPressDown", new[] { typeof(string), typeof(float) })]
internal static class ControllerHoverActionPatch
{
    [HarmonyPostfix]
    private static void Postfix(Rewired.Player __instance, string __0, ref bool __result)
    {
        if (__instance == GameManager.playerInput && NativeControllerActionNames.Parse(__0) == NativeControllerAction.FlightAssist)
            __result |= ControllerActions.Instance?.Queued(NativeControllerAction.AutoHover) == true;
    }
}
