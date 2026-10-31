using System;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NOVR.MotionControls;

// Gear-only adaptation of the installed FS-3 cockpit. No added controls or
// replacement displays; gear state remains owned by the aircraft's native action.
[DefaultExecutionOrder(-1146)]
internal sealed class TernionNativeCockpit : MonoBehaviour
{
    internal static TernionNativeCockpit? Instance { get; private set; }
    internal static bool IsAircraft(Aircraft? aircraft) => aircraft?.definition?.jsonKey == "P_Trisurface1";
    private static readonly Quaternion Face = Quaternion.Euler(20, 0, 0);
    private readonly SwitchGripGate[] _grips = { new(), new() };
    private readonly bool[] _near = new bool[2];
    private Aircraft? _aircraft;
    private Transform? _cockpit;
    private TernionNativeGear? _gear;
    private bool _ready, _discovered, _sent;
    private int _owner = -1;
    private float _readyAt;
    private Vector3 _start, _seat, _anchor, _calibration;
    private Quaternion _heldRotation, _recenter;

    internal static bool OwnsHand(bool left) => !CockpitHudWidgets.Editing && Instance?._ready == true && Instance._owner == (left ? 0 : 1);
    internal static bool ControlWithinReach(bool left, ControllerSample hand, Transform tracking)
    {
        var self = Instance;
        return self?._ready == true && self._owner < 0 && hand.Valid && !CockpitLayout.Active &&
            !CockpitHudWidgets.Editing && !WristMenu.IsOpen && self.Near(left, hand, tracking);
    }
    internal static Pose? HandPose(bool left)
    {
        if (!OwnsHand(left) || Instance!._gear == null) return null;
        return new Pose(Instance._gear.Center, Instance._gear.Lever.rotation * Instance._heldRotation);
    }
    private void Awake() => Instance = this;
    private bool Ready(out Aircraft aircraft, out Transform tracking)
    {
        aircraft = null!; tracking = null!;
        if (!ModConfiguration.Instance.TernionNativeCockpitEnabled.Value || !UnityEngine.XR.XRSettings.enabled ||
            !GameManager.GetLocalAircraft(out aircraft) || !IsAircraft(aircraft) || aircraft.cockpit == null ||
            aircraft.cockpit.IsDetached() || aircraft.HasEjected() || CameraStateManager.cameraMode != CameraMode.cockpit) return false;
        var camera = APIBus.MainCamera; var state = SceneSingleton<CameraStateManager>.i;
        if (camera == null || state == null || state.followingUnit != aircraft || camera.transform.parent != state.transform ||
            camera.GetComponent<NOVRPoseDriver>() == null) return false;
        tracking = camera.transform.parent;
        foreach (var pilot in aircraft.pilots)
            if (pilot != null && !pilot.dead && pilot.currentState == pilot.playerState) return true;
        return false;
    }
    private void Discover(Aircraft aircraft)
    {
        _discovered = true;
        foreach (var cockpit in aircraft.cockpit.GetComponentsInChildren<Cockpit>(true))
        {
            var filter = cockpit.GetComponent<MeshFilter>();
            if (cockpit.name != "cockpit_interior" || filter == null || !TernionNativeGear.Matches(filter.sharedMesh)) continue;
            try { _gear = new TernionNativeGear(filter); _cockpit = cockpit.transform; }
            catch (Exception error) { Debug.LogWarning("[NOVR] Ternion gear left original: " + error.Message); }
            break;
        }
        Debug.Log("[NOVR] FS-3 Ternion original gear lever interaction: " + (_gear != null ? "ready" : "unmatched model; unchanged"));
    }
    private void Update()
    {
        if (GameManager.GetLocalAircraft(out var selected) && IsAircraft(selected) && Keyboard.current?.f6Key.wasPressedThisFrame == true)
        {
            CockpitLayout.Instance?.Finish(false);
            var setting = ModConfiguration.Instance.TernionNativeCockpitEnabled; setting.Value = !setting.Value;
        }
        if (!Ready(out var aircraft, out var tracking)) { Release(); return; }
        if (_aircraft != aircraft) { Release(); _aircraft = aircraft; }
        if (!_discovered) Discover(aircraft);
        if (_gear == null || _cockpit == null) return;
        _gear.Update(aircraft.gearDeployed, Time.unscaledDeltaTime); _ready = true;
        if (_seat != CockpitProfiles.SeatOffset || _anchor != NOVRHeadsetData.TranslationAnchor ||
            _calibration != NOVRHeadsetData.TranslationCalibrationOffset || _recenter != NOVRHeadsetData.RotationCalibrationOffset)
        {
            Cancel(); _seat = CockpitProfiles.SeatOffset; _anchor = NOVRHeadsetData.TranslationAnchor;
            _calibration = NOVRHeadsetData.TranslationCalibrationOffset; _recenter = NOVRHeadsetData.RotationCalibrationOffset;
        }
        if (!TrackedControllers.Available || !GameManager.flightControlsEnabled || Time.timeScale <= 0 || WristMenu.IsOpen ||
            CockpitLayout.Active || CockpitHudWidgets.Editing || DynamicMap.mapMaximized || CursorManager.GetFlag(CursorFlags.GameMenu) ||
            (GameManager.controlMapper != null && GameManager.controlMapper.isOpen) ||
            (SceneSingleton<RadialMenuMain>.i != null && RadialMenuMain.IsInUse())) { Cancel(); return; }
        var motion = MotionFlightController.Instance; var stickLeft = ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        for (var i = 0; i < 2; i++)
        {
            var left = i == 0; var hand = TrackedControllers.Read(left);
            var free = hand.Valid && !HelmetNightVision.OwnsHand(left) &&
                !(left == stickLeft ? motion?.StickHeld == true : motion?.ThrottleHeld == true);
            Direct(left, i, hand, tracking, free);
        }
    }
    private bool Near(bool left, ControllerSample hand, Transform tracking)
    {
        if (_gear == null) return false;
        var tip = PilotHandVisuals.TouchPoint(left, hand, tracking, true);
        var delta = Quaternion.Inverse(_gear.Lever.rotation * Face) * (tip - _gear.Center);
        return SwitchGrabGeometry.Contains(delta.x, delta.y, -delta.z, -delta.z + .025f);
    }
    private void Direct(bool left, int side, ControllerSample hand, Transform tracking, bool free)
    {
        var available = free && (_owner < 0 || _owner == side);
        _grips[side].Update(available, hand.Grip, Time.unscaledTime);
        var near = available && Near(left, hand, tracking);
        if (near && !_near[side]) InteractionHaptics.Pulse(hand.Device, InteractionCue.Touch);
        _near[side] = near;
        if (_owner < 0 && near && _grips[side].CanAcquire(Time.unscaledTime))
        {
            _grips[side].Consume();
            if (Time.unscaledTime < _readyAt) return;
            _owner = side; _sent = false;
            _start = _cockpit!.InverseTransformPoint(PilotHandVisuals.TouchPoint(left, hand, tracking, true));
            _heldRotation = Quaternion.Inverse(_gear!.Lever.rotation) * PilotHandVisuals.FreeHandRotation(left, hand, tracking);
            InteractionHaptics.Pulse(hand.Device, InteractionCue.Grab);
        }
        if (_owner != side) return;
        var point = PilotHandVisuals.TouchPoint(left, hand, tracking, true);
        if (!available || !_grips[side].Held || Vector3.Distance(point, _gear!.Center) > .18f)
        { if (hand.Valid) InteractionHaptics.Pulse(hand.Device, InteractionCue.Release); _owner = -1; return; }
        var delta = Quaternion.Inverse(Face) * (_cockpit!.InverseTransformPoint(point) - _start);
        if (_sent || Mathf.Abs(delta.y) < .003f || (delta.y > 0) != _aircraft!.gearDeployed) return;
        _sent = true; _readyAt = Time.unscaledTime + .65f;
        if (_aircraft.radarAlt <= .2f && delta.y > 0) return;
        ControllerActions.Instance?.QueuePanelAction("Gear");
        InteractionHaptics.Pulse(hand.Device, InteractionCue.Switch);
    }
    private void Cancel()
    {
        _owner = -1; _sent = false;
        for (var i = 0; i < 2; i++) { _grips[i].Reset(); _near[i] = false; }
    }
    private void Release()
    {
        _ready = false; Cancel(); _gear?.Dispose(); _gear = null; _cockpit = null;
        _aircraft = null; _discovered = false; _readyAt = 0;
    }
    private void OnDisable() => Release();
    private void OnDestroy() { Release(); if (Instance == this) Instance = null; }
}
