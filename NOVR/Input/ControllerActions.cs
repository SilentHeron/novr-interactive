using HarmonyLib;
using NuclearOption.UI;
using NuclearOption.MissionEditorScripts;
using UnityEngine;
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

    private readonly ControllerButtonGate _gear = new(), _radar = new(), _assist = new(), _hover = new();
    private int _bindingVersion = -1;
    private ControllerBindingProfile _bindingProfile;
    private bool _bindingLeftHanded;
    private readonly FrameActionQueue<NativeControllerAction, Aircraft> _queuedActions = new();
    private void Awake() => Instance = this;
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
        CameraStateManager.cameraMode == CameraMode.cockpit;

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
        var flightControls = controls != null && controls.FlightActive && !Cursor.visible && !WristMenu.IsOpen;
        bool HandleHeld(bool left) => controls != null && (left == leftHanded ? controls.StickHeld : controls.ThrottleHeld);
        void UpdateFlightButton(ControllerActionId action, ControllerButtonGate gate, bool combat = true)
        {
            var binding = bindings.Get(action);
            var left = ControllerBindings.ResolveLeft(binding.Hand, leftHanded);
            var sample = TrackedControllers.Read(left);
            var available = flightControls && (!combat || bindings.CombatEnabled.Value) && sample.Valid &&
                !NativeHandOptionWheel.OwnsHand(left) &&
                !CockpitMapInteraction.ReservesButton(left, binding.Button, sample.NativeFrame) && HandleHeld(left);
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

        var liveHandleHeld = flightControls && HandleHeld(mapLeft) &&
            GameManager.GetLocalAircraft(out var mapAircraft) && !mapAircraft.disabled;
        var mapButtonAvailable = bindings.PointerEnabled.Value && mapHand.Valid &&
            !NativeHandOptionWheel.OwnsHand(mapLeft) &&
            !CockpitMapInteraction.ReservesButton(mapLeft, mapBinding.Button, mapHand.NativeFrame) &&
            MapAccessAvailable && !WristMenu.IsOpen && !CockpitLayout.Active && !(mapSharesCombat && liveHandleHeld);
        var mapButtonValue = ControllerButtonNames.Resolve(mapBinding.Button, mapHand.NativeFrame) == ControllerButton.Trigger
            ? 0 : bindings.ReadFlight(mapHand, mapLeft, mapBinding.Button);
        _map.Update(mapButtonAvailable, mapButtonValue);
        if (_map.Pressed)
            ToggleMap();
    }

    // Recovery access does not require a held handle or a surviving aircraft.
    // Unlike queued actions, it must not open over any native radial menu.
    internal static bool MapAccessAvailable =>
        !CockpitHudWidgets.Editing &&
        (GameManager.gameState == GameState.SinglePlayer || GameManager.gameState == GameState.Multiplayer) &&
        GameManager.GetLocalPlayer<NuclearOption.Networking.Player>(out _) &&
        SceneSingleton<DynamicMap>.i != null && SceneSingleton<CameraStateManager>.i != null &&
        SceneSingleton<GameplayUI>.i != null && SceneSingleton<MapOptions>.i != null && SceneSingleton<CombatHUD>.i != null &&
        (DynamicMap.mapMaximized || DynamicMap.AllowedToOpen) && !CursorManager.GetFlag(CursorFlags.GameMenu) &&
        !LeaderboardMenu.IsOpen() && !InputFieldChecker.InsideInputField &&
        (SceneSingleton<RadialMenuMain>.i == null || !RadialMenuMain.IsInUse()) &&
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
