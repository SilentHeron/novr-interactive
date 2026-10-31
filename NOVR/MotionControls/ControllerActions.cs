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
    internal static ControllerActions? Instance { get; private set; }
    private static readonly ControllerActionId[] ActionIds = (ControllerActionId[])System.Enum.GetValues(typeof(ControllerActionId));
    internal bool TargetSelecting => !CockpitHudWidgets.Editing && _target.Held;
    private readonly ControllerButtonGate _fire = new(), _release = new(), _countermeasures = new(), _weapon = new(), _target = new(), _nextCountermeasure = new(), _map = new(), _clear = new(), _brake = new(), _unlock = new();
    internal bool GunsHeld => Held("Guns");
    internal bool WeaponReleaseHeld => Held("Weapon Release");
    private readonly ControllerButtonGate _gear = new(), _radar = new(), _assist = new(), _hover = new(), _eots = new();
    private int _bindingVersion = -1;
    private ControllerBindingProfile _bindingProfile;
    private bool _bindingLeftHanded;
    private readonly FrameActionQueue<Aircraft> _queuedActions = new();
    private void Awake() => Instance = this;

    internal void QueueWristAction(string action)
    {
        QueuePanelAction(action);
        WristMenu.Close();
    }

    internal void QueuePanelAction(string action)
    {
        if (CockpitHudWidgets.Editing) return;
        if (action != "Gear" && action != "Radar" && action != "Toggle Engine" && action != "Flight Assist" &&
            action != "Next Weapon" && action != "Previous Weapon" && action != "Next Countermeasure" && action != "Eject" && action != "Auto Hover" &&
            action != "Nav Lights" && action != "Link Guns") return;
        if (!GameManager.GetLocalAircraft(out var aircraft)) return;
        _queuedActions.Enqueue(action, aircraft, Time.frameCount);
    }

    internal bool Queued(string action) => TrackedControllers.Available && Time.timeScale > 0 && GameManager.flightControlsEnabled &&
        !CursorManager.GetFlag(CursorFlags.GameMenu) && (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen) &&
        !DynamicMap.mapMaximized && !WristMenu.IsOpen && !CockpitLayout.Active && !CockpitHudWidgets.Editing &&
        (SceneSingleton<RadialMenuMain>.i == null || !RadialMenuMain.IsInUse()) &&
        CameraStateManager.cameraMode == CameraMode.cockpit && GameManager.GetLocalAircraft(out var aircraft) &&
        _queuedActions.Contains(action, aircraft, Time.frameCount);

    private void Update()
    {
        // Cancel rather than release: ending a HUD drag must not fire a weapon,
        // replay an eject request, or inherit the grip used to move a widget.
        if (CockpitHudWidgets.Editing) { ResetButtons(); return; }
        var config = ModConfiguration.Instance;
        var bindings = config.ControllerBindings;
        var leftHanded = config.MotionControls.LeftHanded.Value;
        var profile = bindings.EffectiveProfile;
        if (_bindingVersion != bindings.ChangeVersion || _bindingProfile != profile || _bindingLeftHanded != leftHanded)
        {
            ResetButtons(); _bindingVersion = bindings.ChangeVersion;
            _bindingProfile = profile; _bindingLeftHanded = leftHanded;
        }
        var controls = MotionFlightController.Instance;
        var flightControls = controls != null && controls.FlightActive && !Cursor.visible && !WristMenu.IsOpen;
        bool HandleHeld(bool left) => controls != null && (left == leftHanded ? controls.StickHeld : controls.ThrottleHeld);
        void FlightButton(ControllerActionId action, ControllerButtonGate gate, bool combat = true)
        {
            var binding = bindings.Get(action);
            var left = ControllerBindings.ResolveLeft(binding.Hand, leftHanded);
            var sample = TrackedControllers.Read(left);
            gate.Update(flightControls && (!combat || bindings.CombatEnabled.Value) && sample.Valid && HandleHeld(left),
                bindings.ReadFlight(sample, left, binding.Button));
        }
        FlightButton(ControllerActionId.Brake, _brake, false);
        FlightButton(ControllerActionId.Guns, _fire);
        FlightButton(ControllerActionId.WeaponRelease, _release);
        FlightButton(ControllerActionId.Countermeasures, _countermeasures);
        FlightButton(ControllerActionId.NextWeapon, _weapon);
        FlightButton(ControllerActionId.SelectTarget, _target);
        FlightButton(ControllerActionId.NextCountermeasure, _nextCountermeasure);
        FlightButton(ControllerActionId.ClearTargets, _clear);
        FlightButton(ControllerActionId.UnlockTarget, _unlock);
        FlightButton(ControllerActionId.Gear, _gear);
        FlightButton(ControllerActionId.Radar, _radar);
        FlightButton(ControllerActionId.FlightAssist, _assist);
        FlightButton(ControllerActionId.HoverAssist, _hover);
        FlightButton(ControllerActionId.Eots, _eots);
        if (_unlock.Released) SceneSingleton<CombatHUD>.i?.DeselectLast();
        if (_clear.Released) SceneSingleton<CombatHUD>.i?.DeselectAll();
        if (_gear.Released) QueuePanelAction("Gear");
        if (_radar.Released) QueuePanelAction("Radar");
        if (_assist.Released) QueuePanelAction("Flight Assist");
        if (_hover.Released) QueuePanelAction("Auto Hover");
        if (_eots.Released) OpticalTargetingHud.Toggle();

        // Map may intentionally share a combat button. The assigned hand's held
        // handle takes priority; the recovery map remains available after a crash.
        var mapBinding = bindings.Get(ControllerActionId.Map);
        var mapLeft = ControllerBindings.ResolveLeft(mapBinding.Hand, leftHanded);
        var mapHand = TrackedControllers.Read(mapLeft);
        var mapSharesCombat = false;
        foreach (var action in ActionIds)
        {
            if (action == ControllerActionId.Map || action == ControllerActionId.WristMenu || action == ControllerActionId.Zoom) continue;
            var candidate = bindings.Get(action);
            if (ControllerBindings.ResolveLeft(candidate.Hand, leftHanded) == mapLeft &&
                ControllerButtonNames.SamePhysicalButton(mapBinding.Button, candidate.Button, mapHand.NativeFrame))
            { mapSharesCombat = true; break; }
        }
        var liveHandleHeld = flightControls && HandleHeld(mapLeft) &&
            GameManager.GetLocalAircraft(out var mapAircraft) && !mapAircraft.disabled;
        _map.Update(bindings.PointerEnabled.Value && mapHand.Valid && MapAccessAvailable && !WristMenu.IsOpen && !CockpitLayout.Active &&
            !(mapSharesCombat && liveHandleHeld), ControllerButtonNames.Resolve(mapBinding.Button, mapHand.NativeFrame) == ControllerButton.Trigger
                ? 0 : bindings.ReadFlight(mapHand, mapLeft, mapBinding.Button));
        if (_map.Pressed) ToggleMap();
    }

    internal static bool MapAccessAvailable =>
        !CockpitHudWidgets.Editing && (GameManager.gameState == GameState.SinglePlayer || GameManager.gameState == GameState.Multiplayer) &&
        GameManager.GetLocalPlayer<NuclearOption.Networking.Player>(out _) &&
        SceneSingleton<DynamicMap>.i != null && SceneSingleton<CameraStateManager>.i != null &&
        SceneSingleton<GameplayUI>.i != null && SceneSingleton<MapOptions>.i != null && SceneSingleton<CombatHUD>.i != null &&
        (DynamicMap.mapMaximized || DynamicMap.AllowedToOpen) && !CursorManager.GetFlag(CursorFlags.GameMenu) &&
        !LeaderboardMenu.IsOpen() && !InputFieldChecker.InsideInputField &&
        (SceneSingleton<RadialMenuMain>.i == null || !RadialMenuMain.IsInUse()) &&
        (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen);

    internal static void ToggleMap()
    {
        if (!MapAccessAvailable) return;
        var map = SceneSingleton<DynamicMap>.i;
        if (map == null) return;
        WristMenu.Close();
        MotionFlightController.Instance?.ResetGrips();
        if (DynamicMap.mapMaximized) map.Minimize(); else map.Maximize();
    }

    internal bool Held(string action) => TrackedControllers.Available && !WristMenu.IsOpen && !CockpitHudWidgets.Editing && !Cursor.visible && action switch
    {
        "Brake" => _brake.Held || Fs20NativeCockpit.BrakeHeld,
        "Fire" => _fire.Held || _release.Held,
        "Guns" => _fire.Held,
        "Weapon Release" => _release.Held,
        "Countermeasures" => _countermeasures.Held,
        _ => false
    };

    internal bool Released(string action) => TrackedControllers.Available && !WristMenu.IsOpen && !CockpitHudWidgets.Editing && !Cursor.visible && action switch
    {
        "Next Weapon" => _weapon.Released,
        "Select" => _target.Released,
        "Next Countermeasure" => _nextCountermeasure.Released,
        _ => false
    };

    internal void ResetButtons()
    {
        _queuedActions.Clear();
        foreach (var gate in new[] { _fire, _release, _countermeasures, _weapon, _target, _nextCountermeasure, _map, _clear, _brake, _unlock, _gear, _radar, _assist, _hover, _eots }) gate.Reset();
    }
    private void OnDisable() => ResetButtons();
    private void OnDestroy() { OnDisable(); if (Instance == this) Instance = null; }
}

// Feed the game's existing action processing so weapon safety, G-LOC, radar
// altitude checks and multiplayer commands continue to be applied by the game.
[HarmonyPatch(typeof(Rewired.Player), "GetButton", new[] { typeof(string) })]
internal static class ControllerHeldActionPatch
{
    [HarmonyPostfix]
    private static void Postfix(Rewired.Player __instance, string __0, ref bool __result)
    {
        if (__instance == GameManager.playerInput) __result |= ControllerActions.Instance?.Held(__0) == true;
    }
}

[HarmonyPatch(typeof(Rewired.Player), "GetButtonTimedPressUp", new[] { typeof(string), typeof(float), typeof(float) })]
internal static class ControllerReleasedActionPatch
{
    [HarmonyPostfix]
    private static void Postfix(Rewired.Player __instance, string __0, ref bool __result)
    {
        if (__instance == GameManager.playerInput)
            __result |= ControllerActions.Instance?.Released(__0) == true || ControllerActions.Instance?.Queued(__0) == true;
    }
}

[HarmonyPatch(typeof(Rewired.Player), "GetButtonDown", new[] { typeof(string) })]
internal static class ControllerPressedActionPatch
{
    [HarmonyPostfix]
    private static void Postfix(Rewired.Player __instance, string __0, ref bool __result)
    {
        if (__instance == GameManager.playerInput && (__0 == "Gear" || __0 == "Eject"))
            __result |= ControllerActions.Instance?.Queued(__0) == true;
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
        if (__instance == GameManager.playerInput && __0 == "Flight Assist")
            __result |= ControllerActions.Instance?.Queued("Auto Hover") == true;
    }
}
