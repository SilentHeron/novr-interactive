using HarmonyLib;
using NuclearOption.UI;
using NuclearOption.MissionEditorScripts;
using UnityEngine;
using NOVR.VrUi;

namespace NOVR.MotionControls;

[DefaultExecutionOrder(-1050)]
internal sealed class ControllerActions : MonoBehaviour
{
    internal static ControllerActions? Instance { get; private set; }
    internal bool TargetSelecting => _target.Held;
    private readonly ControllerButtonGate _fire = new(), _release = new(), _countermeasures = new(), _weapon = new(), _target = new(), _nextCountermeasure = new(), _map = new(), _clear = new(), _brake = new(), _unlock = new();
    internal bool GunsHeld => Held("Guns");
    internal bool WeaponReleaseHeld => Held("Weapon Release");
    private string? _queuedAction;
    private int _queuedFrame;
    private Aircraft? _queuedAircraft;
    private void Awake() => Instance = this;

    internal void QueueWristAction(string action)
    {
        QueuePanelAction(action);
        WristMenu.Close();
    }

    internal void QueuePanelAction(string action)
    {
        if (action != "Gear" && action != "Radar" && action != "Toggle Engine" && action != "Flight Assist" &&
            action != "Next Weapon" && action != "Previous Weapon" && action != "Next Countermeasure" && action != "Eject" && action != "Auto Hover" &&
            action != "Nav Lights" && action != "Link Guns") return;
        if (!GameManager.GetLocalAircraft(out var aircraft)) return;
        _queuedAction = action;
        _queuedFrame = Time.frameCount + 1;
        _queuedAircraft = aircraft;
    }

    internal bool Queued(string action) => _queuedAction == action && _queuedFrame == Time.frameCount &&
        TrackedControllers.Available && Time.timeScale > 0 && GameManager.flightControlsEnabled &&
        !CursorManager.GetFlag(CursorFlags.GameMenu) && (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen) &&
        !DynamicMap.mapMaximized && !WristMenu.IsOpen && !CockpitLayout.Active &&
        (SceneSingleton<RadialMenuMain>.i == null || !RadialMenuMain.IsInUse()) &&
        CameraStateManager.cameraMode == CameraMode.cockpit && GameManager.GetLocalAircraft(out var aircraft) && aircraft == _queuedAircraft;

    private void Update()
    {
        var config = ModConfiguration.Instance;
        var bindings = config.ControllerBindings;
        var stick = TrackedControllers.Read(config.MotionControls.LeftHanded.Value);
        var throttle = TrackedControllers.Read(!config.MotionControls.LeftHanded.Value);
        var controls = MotionFlightController.Instance;
        var flightControls = controls != null && controls.FlightActive && !Cursor.visible && !WristMenu.IsOpen;
        var flight = bindings.CombatEnabled.Value && flightControls;
        var stickActive = flight && stick.Valid && controls!.StickHeld;
        var throttleActive = flight && throttle.Valid && controls!.ThrottleHeld;
        _brake.Update(flightControls && throttle.Valid && controls!.ThrottleHeld, bindings.ReadFlight(throttle, !config.MotionControls.LeftHanded.Value, bindings.Brake.Value));
        _fire.Update(stickActive, bindings.ReadFlight(stick, config.MotionControls.LeftHanded.Value, bindings.Fire.Value));
        _release.Update(stickActive, bindings.ReadFlight(stick, config.MotionControls.LeftHanded.Value, bindings.WeaponRelease.Value));
        _countermeasures.Update(throttleActive, bindings.ReadFlight(throttle, !config.MotionControls.LeftHanded.Value, bindings.Countermeasures.Value));
        _weapon.Update(stickActive, bindings.ReadFlight(stick, config.MotionControls.LeftHanded.Value, bindings.NextWeapon.Value));
        var targetOnStick = bindings.SelectTargetOnStick.Value;
        _target.Update(!((bindings.SelectTargetOnStick.Value ? config.MotionControls.LeftHanded.Value : !config.MotionControls.LeftHanded.Value) && UniversalCockpitPanel.LeftStickSlaved) && (targetOnStick ? stickActive : throttleActive),
            bindings.ReadFlight(targetOnStick ? stick : throttle,
                targetOnStick ? config.MotionControls.LeftHanded.Value : !config.MotionControls.LeftHanded.Value, bindings.SelectTarget.Value));
        _nextCountermeasure.Update(throttleActive, bindings.ReadFlight(throttle, !config.MotionControls.LeftHanded.Value, bindings.NextCountermeasure.Value));
        _clear.Update(stickActive, bindings.ReadFlight(stick, config.MotionControls.LeftHanded.Value, bindings.ClearTargets.Value));
        _unlock.Update(stickActive, bindings.ReadFlight(stick, config.MotionControls.LeftHanded.Value, bindings.UnlockTarget.Value));
        if (_unlock.Released) SceneSingleton<CombatHUD>.i?.DeselectLast();
        if (_clear.Released) SceneSingleton<CombatHUD>.i?.DeselectAll();

        // The tactical map is also the aircraft selection/respawn UI. A crash,
        // dismounted pilot or spectator camera must not remove its shortcut.
        var mapSharesWeapon = ControllerButtonNames.Resolve(bindings.Map.Value, stick.NativeFrame) == ControllerButtonNames.Resolve(bindings.NextWeapon.Value, stick.NativeFrame);
        var liveStickHeld = flightControls && controls!.StickHeld &&
            GameManager.GetLocalAircraft(out var mapAircraft) && !mapAircraft.disabled;
        _map.Update(bindings.PointerEnabled.Value && stick.Valid && MapAccessAvailable && !WristMenu.IsOpen && !CockpitLayout.Active &&
            !(mapSharesWeapon && liveStickHeld), bindings.ReadFlight(stick, config.MotionControls.LeftHanded.Value, bindings.Map.Value));
        if (_map.Pressed) ToggleMap();
    }

    internal static bool MapAccessAvailable =>
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
        if (!MapAccessAvailable) return;
        var map = SceneSingleton<DynamicMap>.i;
        if (map == null) return;
        WristMenu.Close();
        MotionFlightController.Instance?.ResetGrips();
        if (DynamicMap.mapMaximized) map.Minimize(); else map.Maximize();
    }

    internal bool Held(string action) => TrackedControllers.Available && !WristMenu.IsOpen && !Cursor.visible && action switch
    {
        "Brake" => _brake.Held || Fs20NativeCockpit.BrakeHeld,
        "Fire" => _fire.Held || _release.Held,
        "Guns" => _fire.Held,
        "Weapon Release" => _release.Held,
        "Countermeasures" => _countermeasures.Held,
        _ => false
    };

    internal bool Released(string action) => TrackedControllers.Available && !WristMenu.IsOpen && !Cursor.visible && action switch
    {
        "Next Weapon" => _weapon.Released,
        "Select" => _target.Released,
        "Next Countermeasure" => _nextCountermeasure.Released,
        _ => false
    };

    internal void ResetButtons()
    {
        _queuedAction = null;
        foreach (var gate in new[] { _fire, _release, _countermeasures, _weapon, _target, _nextCountermeasure, _map, _clear, _brake, _unlock }) gate.Reset();
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
