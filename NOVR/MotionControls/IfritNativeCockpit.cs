using NOVR.VrCamera;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NOVR.MotionControls;

// Invisible contacts on the installed KR-67 gear wheel and shared seat handles.
// Native displays, materials, meshes and cockpit animations remain game-owned.
[DefaultExecutionOrder(-1145)]
internal sealed class IfritNativeCockpit : MonoBehaviour
{
    internal static IfritNativeCockpit? Instance { get; private set; }
    internal static bool IsAircraft(Aircraft? aircraft) => aircraft?.definition?.jsonKey == "Multirole1";
    internal static bool Selected => GameManager.GetLocalAircraft(out var aircraft) && IsAircraft(aircraft);
    private enum Control { None, Gear, EjectLeft, EjectRight }
    // Measured center of the native wheel (60 triangles) in cockpit_int.
    internal static readonly Vector3 GearPoint = new(-.22789334f, .19608104f, .50077453f);
    private static readonly Quaternion GearFace = Quaternion.Euler(18, 0, 0);
    private readonly SwitchGripGate[] _gearGrips = { new(), new() };
    private readonly ControllerButtonGate[] _ejectGrips = { new(), new() };
    private readonly Control[] _near = new Control[2];
    private readonly SeatHandlePull _pull = new();
    private readonly EjectionRequest _request = new();
    private Aircraft? _aircraft;
    private Transform? _cockpit, _seat;
    private Control _active;
    private int _owner = -1;
    private bool _ready, _sent;
    private Vector3 _start, _seatGrab, _seatOffset, _calibration, _anchor;
    private Quaternion _handRotation, _recenter;
    private float _gearReadyAt, _seatLift;

    internal static bool OwnsHand(bool left) => Instance?._ready == true && Instance._owner == (left ? 0 : 1);
    internal static bool Pinching(bool left) => OwnsHand(left) && Instance!._active == Control.Gear;
    internal static bool ControlWithinReach(bool left, ControllerSample hand, Transform tracking)
    {
        var self = Instance;
        return self?._ready == true && self._owner < 0 && !self._request.BlocksInput && hand.Valid &&
            !CockpitLayout.Active && !WristMenu.IsOpen && self.Near(left, hand, tracking, out _) != Control.None;
    }
    internal static Pose? HandPose(bool left)
    {
        if (!OwnsHand(left)) return null;
        var self = Instance!;
        if (self._active == Control.Gear && self._cockpit != null)
            return new Pose(self._cockpit.TransformPoint(GearPoint), self._cockpit.rotation * self._handRotation);
        if (self._seat != null)
            return new Pose(self._seat.TransformPoint(self._seatGrab + Vector3.up * self._seatLift), self._seat.rotation * self._handRotation);
        return null;
    }
    private void Awake() => Instance = this;
    private bool Ready(out Aircraft aircraft, out Transform tracking)
    {
        aircraft = null!; tracking = null!;
        if (!ModConfiguration.Instance.IfritNativeCockpitEnabled.Value || !UnityEngine.XR.XRSettings.enabled ||
            !GameManager.GetLocalAircraft(out aircraft) || !IsAircraft(aircraft) || aircraft.cockpit == null ||
            aircraft.cockpit.IsDetached() || aircraft.HasEjected() || CameraStateManager.cameraMode != CameraMode.cockpit)
            return false;
        var camera = APIBus.MainCamera;
        if (camera == null || camera.GetComponent<NOVRPoseDriver>() == null || camera.transform.parent == null ||
            SceneSingleton<CameraStateManager>.i?.followingUnit != aircraft) return false;
        tracking = camera.transform.parent;
        foreach (var pilot in aircraft.pilots)
            if (pilot != null && !pilot.dead && pilot.currentState == pilot.playerState) return true;
        return false;
    }
    private void Discover(Aircraft aircraft)
    {
        Cancel(); _aircraft = aircraft; _cockpit = _seat = null; _gearReadyAt = 0;
        foreach (var cockpit in aircraft.cockpit.GetComponentsInChildren<Cockpit>(true))
        {
            var mesh = cockpit.GetComponent<MeshFilter>()?.sharedMesh;
            // Guard the measured contact against future changed/modded models.
            if (mesh != null && mesh.name == "multirole1_cockpit_int" && mesh.vertexCount == 10105 &&
                mesh.subMeshCount == 2 && mesh.GetIndexCount(0) == 31899 && mesh.GetIndexCount(1) == 54)
            { _cockpit = cockpit.transform; break; }
        }
        foreach (var pilot in aircraft.pilots)
        {
            if (pilot == null) continue;
            foreach (var filter in pilot.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (filter.name == "EjectionSeat" && mesh != null && mesh.name == "ejectionSeat" &&
                    mesh.vertexCount == 3806 && mesh.subMeshCount == 1 && mesh.GetIndexCount(0) == 11652)
                { _seat = filter.transform; break; }
            }
            if (_seat != null) break;
        }
        Debug.Log($"[NOVR] KR-67 native contacts: gear={_cockpit != null}, eject={_seat != null}; cockpit geometry and displays unchanged.");
    }
    private void Update()
    {
        if (Selected && Keyboard.current?.f6Key.wasPressedThisFrame == true)
        {
            CockpitLayout.Instance?.Finish(false);
            var setting = ModConfiguration.Instance.IfritNativeCockpitEnabled;
            setting.Value = !setting.Value;
        }
        if (!Ready(out var aircraft, out var tracking))
        {
            _ready = false; Cancel();
            if (!GameManager.GetLocalAircraft(out var current) || current != _aircraft)
            { _aircraft = null; _cockpit = _seat = null; }
            return;
        }
        if (_aircraft != aircraft) Discover(aircraft);
        if (_cockpit == null && _seat == null) { _ready = false; Cancel(); return; }
        _ready = true;
        if (_seatOffset != CockpitProfiles.SeatOffset || _calibration != NOVRHeadsetData.TranslationCalibrationOffset ||
            _anchor != NOVRHeadsetData.TranslationAnchor || _recenter != NOVRHeadsetData.RotationCalibrationOffset)
        {
            Cancel(); _seatOffset = CockpitProfiles.SeatOffset; _calibration = NOVRHeadsetData.TranslationCalibrationOffset;
            _anchor = NOVRHeadsetData.TranslationAnchor; _recenter = NOVRHeadsetData.RotationCalibrationOffset;
        }
        if (!TrackedControllers.Available || !GameManager.flightControlsEnabled || Time.timeScale <= 0 ||
            WristMenu.IsOpen || CockpitLayout.Active || DynamicMap.mapMaximized || CursorManager.GetFlag(CursorFlags.GameMenu) ||
            (GameManager.controlMapper != null && GameManager.controlMapper.isOpen) ||
            (SceneSingleton<RadialMenuMain>.i != null && RadialMenuMain.IsInUse())) { Cancel(); return; }
        _request.Update(aircraft.HasEjected(), Time.unscaledTime, Time.frameCount);
        var motion = MotionFlightController.Instance;
        var stickLeft = ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        for (var i = 0; i < 2; i++)
        {
            var left = i == 0; var hand = TrackedControllers.Read(left);
            var free = hand.Valid && !HelmetNightVision.OwnsHand(left) &&
                !(left == stickLeft ? motion?.StickHeld == true : motion?.ThrottleHeld == true);
            Direct(left, i, hand, tracking, free);
        }
    }
    private Control Near(bool left, ControllerSample hand, Transform tracking, out Vector3 seatGrab)
    {
        seatGrab = Vector3.zero;
        var best = float.MaxValue; var control = Control.None;
        if (_cockpit != null)
        {
            var tip = PilotHandVisuals.TouchPoint(left, hand, tracking, true);
            var delta = Quaternion.Inverse(GearFace) * (_cockpit.InverseTransformPoint(tip) - GearPoint);
            if (SwitchGrabGeometry.Contains(delta.x, delta.y, -delta.z, -delta.z + .025f))
            { best = delta.magnitude; control = Control.Gear; }
        }
        if (_seat != null)
        {
            var palm = _seat.InverseTransformPoint(PilotHandVisuals.TouchPoint(left, hand, tracking));
            for (var side = 0; side < 2; side++)
            {
                // The native striped rail rises toward its front bend. Follow
                // that slope so the grip does not float above its rear end.
                var z = Mathf.Clamp(palm.z, .27f, .42f);
                var rail = new Vector3(side == 0 ? -.25828f : .25828f,
                    Mathf.Lerp(.0625f, .0865f, (z - .27f) / .15f), z);
                var distance = Vector3.Distance(palm, rail);
                if (distance < .035f && distance < best)
                { best = distance; seatGrab = rail; control = side == 0 ? Control.EjectLeft : Control.EjectRight; }
            }
        }
        return control;
    }
    private void Direct(bool left, int side, ControllerSample hand, Transform tracking, bool free)
    {
        var available = free && !_request.BlocksInput && (_owner < 0 || _owner == side);
        _gearGrips[side].Update(available, hand.Grip, Time.unscaledTime);
        _ejectGrips[side].Update(available, hand.Grip);
        var seatGrab = Vector3.zero;
        var near = available ? Near(left, hand, tracking, out seatGrab) : Control.None;
        if (near != Control.None && near != _near[side]) InteractionHaptics.Pulse(hand.Device, InteractionCue.Touch);
        _near[side] = near;
        if (_owner < 0 && near != Control.None)
        {
            var gear = near == Control.Gear;
            var pressed = gear ? _gearGrips[side].CanAcquire(Time.unscaledTime) : _ejectGrips[side].Pressed;
            if (pressed && gear && Time.unscaledTime < _gearReadyAt)
            { _gearGrips[side].Consume(); pressed = false; }
            if (pressed)
            {
                _owner = side; _active = near; _sent = false;
                InteractionHaptics.Pulse(hand.Device, InteractionCue.Grab);
                if (gear)
                {
                    _gearGrips[side].Consume();
                    _start = _cockpit!.InverseTransformPoint(PilotHandVisuals.TouchPoint(left, hand, tracking, true));
                    _handRotation = Quaternion.Inverse(_cockpit.rotation) * PilotHandVisuals.FreeHandRotation(left, hand, tracking);
                }
                else
                {
                    _start = _seat!.InverseTransformPoint(PilotHandVisuals.TouchPoint(left, hand, tracking));
                    _seatGrab = seatGrab; _seatLift = 0; _pull.Begin();
                    _handRotation = Quaternion.Euler(0, left ? 90 : -90, 0);
                }
            }
        }
        if (_owner != side) return;
        if (!available) { End(); return; }
        if (_active == Control.Gear)
        {
            var point = _cockpit!.InverseTransformPoint(PilotHandVisuals.TouchPoint(left, hand, tracking, true));
            if (!_gearGrips[side].Held || Vector3.Distance(point, GearPoint) > .18f)
            { InteractionHaptics.Pulse(hand.Device, InteractionCue.Release); End(); return; }
            var delta = Quaternion.Inverse(GearFace) * (point - _start);
            if (!_sent && Mathf.Abs(delta.y) >= .003f && (delta.y > 0) == _aircraft!.gearDeployed)
            {
                _sent = true; _gearReadyAt = Time.unscaledTime + .65f;
                if (_aircraft.radarAlt <= .2f && delta.y > 0) return; // Keep the native ground safety.
                ControllerActions.Instance?.QueuePanelAction("Gear");
                InteractionHaptics.Pulse(hand.Device, InteractionCue.Switch);
            }
            return;
        }
        var travel = _seat!.InverseTransformPoint(PilotHandVisuals.TouchPoint(left, hand, tracking)) - _start;
        _seatLift = Mathf.Clamp(travel.y, 0, .045f);
        if (!_ejectGrips[side].Held || new Vector2(travel.x, travel.z).magnitude > .12f)
        { InteractionHaptics.Pulse(hand.Device, InteractionCue.Release); End(); return; }
        if (_pull.Update(true, travel.y, new Vector2(travel.x, travel.z).magnitude))
        {
            ControllerActions.Instance?.QueuePanelAction("Eject");
            _request.Begin(Time.unscaledTime, Time.frameCount);
            InteractionHaptics.Pulse(hand.Device, InteractionCue.Eject); End();
        }
    }
    private void End() { _owner = -1; _active = Control.None; _seatLift = 0; _pull.Reset(); }
    private void Cancel()
    {
        End(); _request.Reset();
        for (var i = 0; i < 2; i++) { _gearGrips[i].Reset(); _ejectGrips[i].Reset(); _near[i] = Control.None; }
    }
    private void OnDisable() { _ready = false; Cancel(); }
    private void OnDestroy() { Cancel(); if (Instance == this) Instance = null; }
}
