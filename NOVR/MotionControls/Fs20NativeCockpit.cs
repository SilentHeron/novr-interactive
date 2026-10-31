using System.Collections.Generic;
using NOVR.VrCamera;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;

namespace NOVR.MotionControls;

// Integrated FS-20 controls are physical-only. No controller ray can operate
// a switch or an eject handle. Native aircraft logic owns every action.
[DefaultExecutionOrder(-1140)]
internal sealed class Fs20NativeCockpit : MonoBehaviour
{
    internal static Fs20NativeCockpit? Instance { get; private set; }
    internal static bool IsAircraft(Aircraft? aircraft) => aircraft != null && Fs20ControlMap.Matches(aircraft.definition?.jsonKey);
    internal static bool Selected => GameManager.GetLocalAircraft(out var aircraft) && IsAircraft(aircraft);
    internal static bool Active => Instance?._ready == true;
    internal static Vector2 MapPan => Vector2.zero;
    internal static bool OwnsHand(bool left) => Active && Instance!._owner == (left ? 0 : 1) && Instance._active != null;
    internal static bool SwitchWithinReach(bool left, ControllerSample hand, Transform tracking)
    {
        var self = Instance;
        if (self?._ready != true || !hand.Valid || CockpitLayout.Active || WristMenu.IsOpen || self._request.BlocksInput || self._owner >= 0) return false;
        var tip = PilotHandVisuals.TouchPoint(left, hand, tracking, true);
        foreach (var control in Fs20PanelLayout.Controls)
            if (control.Type == Fs20PanelLayout.Kind.Switch && self.WithinSwitch(control, tip, out _)) return true;
        return false;
    }
    internal static bool BrakeHeld => Active && Instance!._active == "brake";
    internal static bool IsDocked(Transform widget) => false;
    internal static Transform? Cockpit => Active ? Instance?._root : null;
    internal static void ReleaseMap() { }
    internal static bool LeftStickSlaved => false;
    internal static bool Pinching(bool left) => Active && Instance!._owner == (left ? 0 : 1) && Instance._active is "gear" or "radar" or "aots";
    private static CockpitLayout.Element SideHandle(bool left) => left == ModConfiguration.Instance.MotionControls.LeftHanded.Value ? CockpitLayout.Element.Stick : CockpitLayout.Element.Throttle;
    private Aircraft? _aircraft;
    private Transform? _root, _seat;
    private Fs20ControlCover? _nativeCover;
    private Fs20NativeGear? _nativeGear;
    private readonly Dictionary<string, Transform> _actuators = new();
    private readonly Dictionary<string, TextMesh> _labels = new();
    private readonly Dictionary<string, Renderer> _faces = new();
    private readonly List<Mesh> _hardwareMeshes = new();
    private readonly ControllerButtonGate[] _grips = { new(), new() }, _presses = { new(), new() };
    private readonly SwitchGripGate[] _switchGrips = { new(), new() };
    private readonly Dictionary<string, float> _switchReadyAt = new();
    private const float SwitchTravel = .003f, SwitchCooldown = .65f;
    private readonly SeatTouchGate[] _seatTouches = { new(), new() };
    private readonly Dictionary<string, CockpitTouchGate>[] _touch = { new(), new() };
    private readonly ButtonContactGuide[] _buttonGuides = { new(), new() };
    private readonly Fs20PanelLayout.Control?[] _buttonControls = new Fs20PanelLayout.Control?[2];
    private readonly Quaternion[] _buttonRotations = new Quaternion[2];
    private readonly string?[] _near = new string?[2];
    private readonly SeatHandlePull _pull = new();
    private readonly EjectionRequest _request = new();
    private Material? _base, _cap, _hover, _on, _metal, _textMaterial;
    private Font? _font;
    private TextMesh? _hint;
    private string? _active;
    private int _owner = -1;
    private Vector3 _start, _calibration, _anchor;
    private Quaternion _recenter;
    private bool _ready, _sent, _physicalPress;
    private Quaternion _heldHandRotation;
    private Vector3 _seatGrab;
    private float _seatLift;
    private float _feedbackUntil;
    private string _feedback = "";
    internal static Pose? HandPose(bool left, out bool pointing)
    {
        pointing = false; var self = Instance; var side = left ? 0 : 1;
        if (self?._ready != true) return null;
        // A held switch/handle owns the hand pose even when the tracked finger
        // moves near another button. Hover and a stale guide must not recurl it.
        if (self._owner == side && self._active != null)
        {
            if (self._active.StartsWith("eject_"))
                return self._seat != null ? new Pose(self._seat.TransformPoint(self._seatGrab + Vector3.up * self._seatLift), self._seat.rotation * self._heldHandRotation) : null;
            if (self._active is "gear" or "radar" or "aots")
            {
                var actuator = self._actuators[self._active];
                return new Pose(actuator.TransformPoint(self._active == "gear" ? Vector3.zero : new Vector3(0,0,-.022f)), actuator.rotation * self._heldHandRotation);
            }
        }
        var near = self._near[side];
        pointing = near != null && (near.EndsWith("_lock") || near.StartsWith("seat_") || near == "brake");
        if (self._buttonGuides[side].Captured && self._buttonControls[side] is { } button)
        {
            pointing = true;
            var guide = self._buttonGuides[side];
            var local = button.Point + button.Rotation * new Vector3(guide.X, guide.Y, -.016f - guide.VisualDepth);
            return new Pose(self._root!.TransformPoint(local), self._root.rotation * button.Rotation * self._buttonRotations[side]);
        }
        return null;
    }
    private void Awake() => Instance = this;
    private bool Ready(out Aircraft aircraft)
    {
        aircraft = null!;
        if (!ModConfiguration.Instance.Fs20NativeCockpitEnabled.Value || !UnityEngine.XR.XRSettings.enabled ||
            !GameManager.GetLocalAircraft(out aircraft) || !IsAircraft(aircraft) || aircraft.cockpit == null ||
            aircraft.cockpit.IsDetached() || aircraft.HasEjected() || CameraStateManager.cameraMode != CameraMode.cockpit) return false;
        var camera = APIBus.MainCamera;
        if (camera == null || camera.GetComponent<NOVRPoseDriver>() == null || camera.transform.parent == null ||
            NOUIManager.I == null || APIBus.CockpitHudCamera?.transform.parent == null ||
            SceneSingleton<CameraStateManager>.i?.followingUnit != aircraft) return false;
        foreach (var pilot in aircraft.pilots)
            if (pilot != null && !pilot.dead && pilot.currentState == pilot.playerState) return true;
        return false;
    }
    private void Update()
    {
        if (Selected && Keyboard.current?.f6Key.wasPressedThisFrame == true)
        {
            CockpitLayout.Instance?.Finish(false);
            var setting = ModConfiguration.Instance.Fs20NativeCockpitEnabled;
            setting.Value = !setting.Value;
        }
        if (!Ready(out var aircraft))
        {
            Pause();
            if (_aircraft != null && (!GameManager.GetLocalAircraft(out var current) || current != _aircraft)) Release();
            return;
        }
        if (_aircraft != aircraft || _root == null)
        {
            Release();
            var cockpit = aircraft.cockpit.GetComponentInChildren<Cockpit>(true);
            if (cockpit == null || cockpit.name != "cockpit_int") return;
            _aircraft = aircraft;
            _root = new GameObject("NOVR FS-20 integrated controls").transform; _root.SetParent(cockpit.transform, false);
            foreach (var node in aircraft.cockpit.GetComponentsInChildren<Transform>(true))
                if (node.name == "EjectionSeat") { _seat = node; break; }
            try
            {
                CreateVisuals();
                try { _nativeCover = new Fs20ControlCover(cockpit.GetComponent<MeshFilter>()); }
                catch (System.Exception e) { Debug.LogWarning("[NOVR] Native control cover skipped: " + e.Message); }
                if (_nativeCover != null)
                {
                    try
                    {
                        _nativeGear = new Fs20NativeGear(_nativeCover, cockpit.GetComponent<MeshFilter>(), _root);
                        Destroy(_actuators["gear"].gameObject);
                        _actuators["gear"] = _nativeGear.Contact;
                        _nativeGear.Update(!aircraft.gearDeployed, 0);
                    }
                    catch (System.Exception e) { Debug.LogWarning("[NOVR] Native gear movement unavailable; original knob remains interactive: " + e.Message); }
                }
            }
            catch (System.Exception e) { Release(); Debug.LogError("[NOVR] FS-20 cockpit setup failed: " + e); enabled = false; return; }
            Debug.Log("[NOVR] FS-20 physical controls ready: native displays retained; gear, brake, radar, EOTS, seat adjustment and native eject handles.");
        }
        if (_calibration != NOVRHeadsetData.TranslationCalibrationOffset || _anchor != NOVRHeadsetData.TranslationAnchor || _recenter != NOVRHeadsetData.RotationCalibrationOffset)
        { Cancel(); _calibration = NOVRHeadsetData.TranslationCalibrationOffset; _anchor = NOVRHeadsetData.TranslationAnchor; _recenter = NOVRHeadsetData.RotationCalibrationOffset; }
        _nativeCover?.SetVisible(true);
        _ready = true; _root!.gameObject.SetActive(true);
        // Presentation remains fixed and visible while positioning or using
        // setup menus. Only interaction pauses; native floating HUD stays hidden.
        if (!TrackedControllers.Available || !GameManager.flightControlsEnabled || Time.timeScale <= 0 || WristMenu.IsOpen ||
            DynamicMap.mapMaximized || CursorManager.GetFlag(CursorFlags.GameMenu) ||
            (GameManager.controlMapper != null && GameManager.controlMapper.isOpen) ||
            (SceneSingleton<RadialMenuMain>.i != null && RadialMenuMain.IsInUse()))
        { Cancel(); _hint!.text = ""; return; }
        _request.Update(aircraft.HasEjected(), Time.unscaledTime, Time.frameCount);
        var motion = MotionFlightController.Instance;
        var leftStick = ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        for (var i = 0; i < 2; i++)
        {
            var left = i == 0; var hand = TrackedControllers.Read(left);
            var free = hand.Valid && !HelmetNightVision.OwnsHand(left) && CockpitLayout.HeldByHand(left) == CockpitLayout.Element.None &&
                !(left == leftStick ? motion?.StickHeld == true : motion?.ThrottleHeld == true);
            Direct(hand, free, left, i);
        }
        ShowHint();
    }
    private void Direct(ControllerSample hand, bool free, bool left, int side)
    {
        if (CockpitLayout.Active && _active != null)
        {
            var setup = false;
            foreach (var control in Fs20PanelLayout.Controls)
                if (control.Id == _active) { setup = control.Type == Fs20PanelLayout.Kind.Setup; break; }
            if (!setup)
            {
                // Editing only permits seat and handle-position controls. A
                // previously held switch/eject must not survive that transition.
                End();
                foreach (var gate in _grips) gate.Reset();
                foreach (var gate in _switchGrips) gate.Reset();
                foreach (var gate in _presses) gate.Reset();
            }
        }
        var available = free && !_request.BlocksInput && (_owner < 0 || _owner == side);
        var tracking = APIBus.MainCamera!.transform.parent;
        var world = PilotHandVisuals.TouchPoint(left, hand, tracking);
        var fingertip = PilotHandVisuals.TouchPoint(left, hand, tracking, true);
        // The seat step itself shifts mapped hands by 2 cm. Exclude that
        // virtual shift when deciding whether the pilot actually withdrew.
        var physicalTip = tracking.InverseTransformPoint(fingertip) - CockpitProfiles.SeatOffset;
        _seatTouches[side].Observe(hand.Valid, physicalTip.x, physicalTip.y, physicalTip.z);
        _grips[side].Update(available, hand.Grip); _presses[side].Update(available, Mathf.Max(hand.Trigger, hand.Grip));
        _switchGrips[side].Update(available && !CockpitLayout.Active, hand.Grip, Time.unscaledTime);
        // A deliberate grip at a switch tip wins over an adjacent button's
        // pointing guide, including one still waiting for finger withdrawal.
        Fs20PanelLayout.Control? grabbedSwitch = null;
        if (available && _owner < 0 && _switchGrips[side].CanAcquire(Time.unscaledTime) && !CockpitLayout.Active)
        {
            var best = float.MaxValue;
            foreach (var c in Fs20PanelLayout.Controls)
                if (c.Type == Fs20PanelLayout.Kind.Switch && WithinSwitch(c, fingertip, out var distance) && distance < best)
                { grabbedSwitch = c; best = distance; }
            if (grabbedSwitch != null)
            { _buttonGuides[side].Reset(); _buttonControls[side] = null; }
        }
        UpdateButtonGuide(side, left, hand, tracking, fingertip, available && grabbedSwitch == null);
        string? near = null;
        Fs20PanelLayout.Control? target = null;
        float closest = float.MaxValue;
        var touched = false;
        foreach (var c in Fs20PanelLayout.Controls)
        {
            if (CockpitLayout.Active && c.Type != Fs20PanelLayout.Kind.Setup) continue;
            var p = Quaternion.Inverse(c.Rotation) * (_root!.InverseTransformPoint(fingertip) - c.Point);
            var inside = Mathf.Abs(p.x) < c.Size.x * .5f && Mathf.Abs(p.y) < c.Size.y * .5f;
            var guided = _buttonGuides[side].Captured && _buttonControls[side] == c;
            var eligible = available && (grabbedSwitch == null || grabbedSwitch == c) && (_buttonControls[side] == null || guided);
            if (guided) inside = true; // Stabilize small lateral drift along the captured axis.
            var depth = -p.z - .016f;
            var distance = Mathf.Abs(depth);
            var close = eligible && inside && depth > -.025f && depth < .085f;
            if (c.Type == Fs20PanelLayout.Kind.Switch)
            {
                // The cap sits to the right of its label and tilts with state.
                // Use the same real tracked fingertip as the push buttons.
                // The palm is roughly 13 cm behind the visible fingertip.
                inside = WithinSwitch(c, fingertip, out distance);
                close = eligible && inside;
            }
            if (!_touch[side].TryGetValue(c.Id, out var gate)) _touch[side][c.Id] = gate = new CockpitTouchGate();
            var contact = c.Type != Fs20PanelLayout.Kind.Switch && gate.Update(eligible, inside, depth);
            if (close && distance < closest)
            { closest = distance; near = c.Id; target = c; touched = contact; }
        }
        // The long striped handles are part of the native seat mesh. Match
        // their upper rails, not a guessed point on a replacement dashboard.
        if (available && grabbedSwitch == null && _buttonControls[side] == null && _seat != null && !CockpitLayout.Active)
        {
            var p = _seat.InverseTransformPoint(world);
            for (var i = 0; i < 2; i++)
            {
                var rail = new Vector3(i == 0 ? -.258f : .258f, .086f, Mathf.Clamp(p.z, .25f, .42f));
                var distance = Vector3.Distance(p, rail);
                if (distance < .035f && distance < closest) { near = i == 0 ? "eject_left" : "eject_right"; target = null; closest = distance; }
            }
        }
        if (near != _near[side] && near != null) InteractionHaptics.Pulse(hand.Device, InteractionCue.Touch);
        _near[side] = near;
        if (_owner < 0 && near != null)
        {
            var seatHandle = near.StartsWith("eject_");
            var flick = target?.Type == Fs20PanelLayout.Kind.Switch;
            var press = seatHandle ? _grips[side].Pressed : flick ? _switchGrips[side].CanAcquire(Time.unscaledTime) : touched;
            if (press && flick && _switchReadyAt.TryGetValue(near, out var readyAt) && Time.unscaledTime < readyAt)
            {
                // Reject this entire squeeze, not just this frame. Holding it
                // through the cooldown must never produce a delayed flip.
                _switchGrips[side].Consume();
                press = false;
            }
            if (press && near.StartsWith("seat_") && target != null)
            {
                var outward = tracking.InverseTransformDirection(_root!.rotation * target.Rotation * Vector3.back).normalized;
                press = _seatTouches[side].TryPress(physicalTip.x, physicalTip.y, physicalTip.z, outward.x, outward.y, outward.z, Time.unscaledTime);
            }
            if (press)
            {
                _active = near; _owner = side; _sent = false; _physicalPress = touched;
                _start = seatHandle ? _seat!.InverseTransformPoint(world) : _root!.InverseTransformPoint(fingertip);
                if (seatHandle || flick) InteractionHaptics.Pulse(hand.Device, InteractionCue.Grab);
                else InteractionHaptics.Pulse(hand.Device, .35f, .045f, InteractionCue.Click);
                if (seatHandle)
                {
                    _pull.Begin(); _seatLift = 0;
                    _seatGrab = new Vector3(near == "eject_left" ? -.258f : .258f, .086f, Mathf.Clamp(_start.z,.25f,.42f));
                    _heldHandRotation = Quaternion.Euler(0, left ? 90 : -90, 0);
                }
                else if (flick)
                {
                    _switchGrips[side].Consume();
                    _heldHandRotation = Quaternion.Inverse(_actuators[near].rotation) * PilotHandVisuals.FreeHandRotation(left,hand,tracking);
                }
                else if (target?.Type != Fs20PanelLayout.Kind.Hold) { Activate(near, fingertip); _sent = true; }
            }
        }
        if (_owner != side) return;
        if (!available) { End(); return; }
        if (_active!.StartsWith("eject_"))
        {
            var d = _seat!.InverseTransformPoint(world) - _start;
            _seatLift = Mathf.Clamp(d.y,0,.045f);
            if (!_grips[side].Held || new Vector2(d.x, d.z).magnitude > .12f) { End(); return; }
            if (_pull.Update(true, d.y, new Vector2(d.x, d.z).magnitude))
            {
                ControllerActions.Instance?.QueuePanelAction("Eject"); _request.Begin(Time.unscaledTime, Time.frameCount);
                InteractionHaptics.Pulse(hand.Device, .65f, .09f, InteractionCue.Eject); End();
            }
            return;
        }
        Fs20PanelLayout.Control? active = null;
        foreach (var c in Fs20PanelLayout.Controls) if (c.Id == _active) { active = c; break; }
        if (active == null) { End(); return; }
        // Once pressed, a guided finger may move slightly behind the cap while
        // the visual tip stops at its travel limit. That must not release a
        // held brake until the real hand retreats or leaves the guide bounds.
        var guidedHold = _buttonGuides[side].Captured && _buttonControls[side] == active;
        var held = active.Type == Fs20PanelLayout.Kind.Switch ? _switchGrips[side].Held : _physicalPress
            ? guidedHold ? _buttonGuides[side].HoldsPress : near == _active && closest < .012f
            : _presses[side].Held;
        if (!held || Vector3.Distance(fingertip, _root!.TransformPoint(active.Point)) > .18f)
        { InteractionHaptics.Pulse(hand.Device,.14f,.025f,InteractionCue.Release); End(); return; }
        if (active.Type == Fs20PanelLayout.Kind.Switch && !_sent)
        {
            var delta = Quaternion.Inverse(active.Rotation) * (_root.InverseTransformPoint(fingertip) - _start);
            if (Mathf.Abs(delta.y) >= SwitchTravel)
            {
                var desired = delta.y > 0;
                // Gear UP means retract; radar/EOTS UP means on.
                var state = State(active.Id);
                // A small initial movement toward the existing state must not
                // consume the flick; the pilot can reverse direction naturally.
                if (state != desired)
                {
                    Activate(active.Id, fingertip);
                    _switchReadyAt[active.Id] = Time.unscaledTime + SwitchCooldown;
                    _sent = true; InteractionHaptics.Pulse(hand.Device, InteractionCue.Switch);
                }
            }
        }
    }
    private bool WithinSwitch(Fs20PanelLayout.Control control, Vector3 fingertip, out float distance)
    {
        var plate = Quaternion.Inverse(control.Rotation) * (_root!.InverseTransformPoint(fingertip) - control.Point);
        var cap = _actuators[control.Id].InverseTransformPoint(fingertip) - (control.Id == "gear" ? Vector3.zero : new Vector3(0, 0, -.022f));
        distance = cap.magnitude;
        // The original gear wheel travels on its own lever arc; its contact
        // plane must follow that wheel rather than its initial down position.
        return SwitchGrabGeometry.Contains(cap.x, cap.y, -cap.z, control.Id == "gear" ? -cap.z + .025f : -plate.z);
    }
    private void UpdateButtonGuide(int side, bool left, ControllerSample hand, Transform tracking, Vector3 fingertip, bool available)
    {
        // A held switch/eject handle keeps its grip pose, even if its tracked
        // fingertip passes close to another button during the movement.
        available &= !(_owner == side && (_active is "gear" or "radar" or "aots" || _active?.StartsWith("eject_") == true));
        var guide = _buttonGuides[side];
        var control = _buttonControls[side];
        var localTip = _root!.InverseTransformPoint(fingertip);
        if (control != null)
        {
            var p = Quaternion.Inverse(control.Rotation) * (localTip - control.Point);
            guide.Update(available && !CockpitLayout.Active, p.x, p.y, -p.z - .016f, control.Size.x * .5f, control.Size.y * .5f, tracked: hand.Valid);
            if (!guide.Captured && !guide.WaitingForWithdrawal) _buttonControls[side] = null;
            return;
        }
        if (!available || CockpitLayout.Active) return;
        var rotation = PilotHandVisuals.FreeHandRotation(left, hand, tracking);
        var best = float.MaxValue;
        foreach (var candidate in Fs20PanelLayout.Controls)
        {
            if (candidate.Type == Fs20PanelLayout.Kind.Switch) continue;
            var p = Quaternion.Inverse(candidate.Rotation) * (localTip - candidate.Point);
            var depth = -p.z - .016f;
            if (Mathf.Abs(p.x) > candidate.Size.x * .5f || Mathf.Abs(p.y) > candidate.Size.y * .5f || depth > .035f || depth < -.025f) continue;
            var normal = _root.rotation * candidate.Rotation * Vector3.forward;
            if (Vector3.Dot(rotation * Vector3.forward, normal) < .55f) continue;
            if (Mathf.Abs(depth) >= best) continue;
            best = Mathf.Abs(depth); control = candidate;
        }
        if (control == null) return;
        var point = Quaternion.Inverse(control.Rotation) * (localTip - control.Point);
        if (!guide.Update(true, point.x, point.y, -point.z - .016f, control.Size.x * .5f, control.Size.y * .5f)) return;
        _buttonControls[side] = control;
        var face = _root.rotation * control.Rotation;
        var aligned = Quaternion.FromToRotation(rotation * Vector3.forward, face * Vector3.forward) * rotation;
        _buttonRotations[side] = Quaternion.Inverse(face) * aligned;
    }
    private bool State(string id) => id switch
    {
        "gear" => !_aircraft!.gearDeployed,
        "radar" => _aircraft!.radar?.activated == true,
        "aots" => ModConfiguration.Instance.OpticalHud.Enabled.Value,
        "brake" => BrakeHeld || _aircraft!.GetInputs().brake > .05f, _ => false
    };
    private void Activate(string id, Vector3 world)
    {
        if (id.EndsWith("_lock")) CockpitLayout.Instance?.ToggleHandle(SideHandle(id == "left_lock"));
        else if (id.StartsWith("seat_"))
        {
            var delta = id switch { "seat_up" => Vector3.up, "seat_down" => Vector3.down, "seat_left" => Vector3.left,
                "seat_right" => Vector3.right, "seat_forward" => Vector3.forward, _ => Vector3.back };
            CockpitProfiles.Instance?.MoveSeat(delta*.02f); ModConfiguration.Instance.Config.Save();
        }
        else if (id == "gear")
        {
            if (_aircraft!.radarAlt <= .2f) Feedback("GEAR LOCKED ON GROUND");
            else ControllerActions.Instance?.QueuePanelAction("Gear");
        }
        else if (id == "radar")
        {
            if (_aircraft!.radar == null) Feedback("RADAR NOT FITTED");
            else ControllerActions.Instance?.QueuePanelAction("Radar");
        }
        else if (id == "aots") OpticalTargetingHud.Toggle();
    }
    private void Feedback(string text) { _feedback = text; _feedbackUntil = Time.unscaledTime + 2; }
    private void ShowHint()
    {
        var id = _active ?? _near[0] ?? _near[1];
        _hint!.text = _request.BlocksInput ? "EJECT REQUESTED" : Time.unscaledTime < _feedbackUntil ? _feedback :
            id?.StartsWith("eject_") == true ? "GRIP + PULL UP TO EJECT" :
            id is "gear" or "radar" or "aots" ? _active == id ? "FLICK UP / DOWN · RELEASE TO LET GO" :
                (_near[0] == id && _switchGrips[0].Held || _near[1] == id && _switchGrips[1].Held) ? "RELEASE GRIP, THEN GRAB SWITCH" : "LIGHT GRIP · SHORT FLICK UP / DOWN" :
            "";
        _hint.transform.localPosition = id?.StartsWith("eject_") == true && _seat != null
            ? _root!.InverseTransformPoint(_seat.TransformPoint(new Vector3(id == "eject_left" ? -.258f : .258f, .14f, .33f)))
            : new Vector3(0,.157f,.470f);
    }
    private void LateUpdate()
    {
        if (!_ready) return;
        _nativeGear?.Update(!_aircraft!.gearDeployed, Time.unscaledDeltaTime);
        foreach (var c in Fs20PanelLayout.Controls)
        {
            if (c.Id == "gear") continue; // Original wheel/stem and its state-driven lever movement.
            var hover = _near[0] == c.Id || _near[1] == c.Id; var active = _active == c.Id;
            var state = c.Id.EndsWith("_lock") ? CockpitLayout.Unlocked(SideHandle(c.Id == "left_lock")) : State(c.Id); var actuator = _actuators[c.Id];
            var material = active || hover ? _hover : state ? _on : _cap;
            if (_faces[c.Id].sharedMaterial != material) _faces[c.Id].sharedMaterial = material;
            if (c.Type == Fs20PanelLayout.Kind.Switch)
            {
                // The cap extends along local -Z: positive X rotation lifts it
                // toward +Y. Match the flick's UP=ON / DOWN=OFF state mapping.
                actuator.localRotation = Quaternion.Slerp(actuator.localRotation, Quaternion.Euler(state ? 25 : -25,0,0), FlightControlMath.SmoothingWeight(Time.unscaledDeltaTime, 20));
            }
            else actuator.localPosition = new Vector3(0,0,active ? -.008f : -.011f);
            var text = c.Id.EndsWith("_lock") ? (SideHandle(c.Id == "left_lock") == CockpitLayout.Element.Stick ? "STICK POS" : "THROTTLE POS") + "\n" + (CockpitLayout.Unlocked(SideHandle(c.Id == "left_lock")) ? "UNLOCKED" : "LOCKED")
                : c.Id.StartsWith("seat_") ? "SEAT\n" + c.Label
                : c.Label + "\n" + (c.Id == "gear" ? (state ? "UP" : "DOWN") : c.Id == "brake" ? "HOLD" : state ? "ON" : "OFF");
            if (_labels[c.Id].text != text) _labels[c.Id].text = text;
        }
    }
    private void CreateVisuals()
    {
        _base = CockpitMaterials.Hardware("FS-20 recessed switch plate", new Color(.045f,.050f,.053f));
        _cap = CockpitMaterials.Hardware("FS-20 switch cap", new Color(.22f,.24f,.25f));
        _hover = CockpitMaterials.Hardware("FS-20 touched switch", new Color(.45f,.38f,.18f));
        _on = CockpitMaterials.Hardware("FS-20 active switch", new Color(.16f,.32f,.25f));
        _metal = CockpitMaterials.Hardware("FS-20 brushed switch hardware", new Color(.42f,.44f,.46f), .7f);
        var cockpitMaterial = _root!.parent.GetComponent<MeshRenderer>()?.sharedMaterial;
        CockpitMaterials.Fs20Finish(_base,cockpitMaterial,new Color(.55f,.55f,.55f));
        CockpitMaterials.Fs20Finish(_cap,cockpitMaterial,Color.white);
        CockpitMaterials.Fs20Finish(_hover,cockpitMaterial,new Color(1.3f,1.12f,.75f));
        CockpitMaterials.Fs20Finish(_on,cockpitMaterial,new Color(.8f,1.25f,.9f));
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _textMaterial = new Material(Shader.Find("UI/Default")); _textMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual);
        _textMaterial.color = Color.white; RefreshFont(_font); Font.textureRebuilt += RefreshFont;
        foreach (var c in Fs20PanelLayout.Controls)
        {
            if (c.Id == "gear")
            {
                // Invisible contact anchor for the original cockpit knob. If
                // extraction is unavailable, the untouched native mesh remains.
                var contact = new GameObject("Native gear knob contact").transform;
                contact.SetParent(_root, false); contact.localPosition = Fs20NativeGear.NativePoint;
                contact.localRotation = c.Rotation; _actuators[c.Id] = contact;
                continue;
            }
            var plate = new GameObject("FS-20 " + c.Label).transform; plate.SetParent(_root, false); plate.localPosition = c.Point; plate.localRotation = c.Rotation;
            Box(plate, Vector3.zero, new Vector3(c.Size.x,c.Size.y,.009f), _base);
            // Recessed corner fasteners and raised bezels seat these controls in
            // the existing instrument cluster instead of floating UI rectangles.
            foreach (var x in new[] { -1f, 1f })
                Cylinder(plate, new Vector3(x*(c.Size.x*.5f-.005f),0,-.005f), .005f, .002f, _metal);
            var actuator = new GameObject("Physical actuator").transform; actuator.SetParent(plate, false); _actuators[c.Id] = actuator;
            if (c.Type == Fs20PanelLayout.Kind.Switch)
            {
                actuator.localPosition = new Vector3(c.Size.x*.31f, 0, -.012f);
                Cylinder(plate, actuator.localPosition + Vector3.forward*.004f, .018f, .008f, _metal);
                Cylinder(actuator,new Vector3(0,0,-.009f),.007f,.026f,_metal);
                _faces[c.Id] = Box(actuator,new Vector3(0,0,-.022f),new Vector3(.020f,.024f,.012f),_cap);
                _labels[c.Id] = Label(plate,new Vector3(-.012f,0,-.005f),.0015f);
            }
            else
            {
                actuator.localPosition = new Vector3(0,0,-.011f);
                _faces[c.Id] = Box(actuator,Vector3.zero,new Vector3(c.Size.x-.008f,c.Size.y-.008f,.009f),_cap);
                _labels[c.Id] = Label(actuator,new Vector3(0,0,-.005f),.0015f);
            }
        }
        _hint = Label(_root!,Vector3.zero,.0017f); _hint.transform.localRotation = Fs20PanelLayout.Face;
    }
    private TextMesh Label(Transform parent, Vector3 position, float size)
    {
        var go = new GameObject("White control label", typeof(TextMesh)); go.transform.SetParent(parent,false); go.transform.localPosition = position;
        go.layer = (int)LayerHelper.Layers.Cockpit; var text = go.GetComponent<TextMesh>();
        text.font = _font; text.fontSize = 48; text.characterSize = size; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = Color.white;
        go.GetComponent<MeshRenderer>().sharedMaterial = _textMaterial; return text;
    }
    private Renderer Box(Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var go=new GameObject("Machined control housing",typeof(MeshFilter),typeof(MeshRenderer));
        go.transform.SetParent(parent,false);go.transform.localPosition=position;go.layer=(int)LayerHelper.Layers.Cockpit;
        var mesh=CockpitHardware.Keycap(scale);_hardwareMeshes.Add(mesh);go.GetComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;return renderer;
    }
    private static Renderer Cylinder(Transform parent, Vector3 position, float diameter, float length, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        go.transform.SetParent(parent,false); go.transform.localPosition = position; go.transform.localRotation=Quaternion.Euler(90,0,0);go.transform.localScale = new Vector3(diameter,length*.5f,diameter); go.layer = (int)LayerHelper.Layers.Cockpit;
        var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; return renderer;
    }
    private void RefreshFont(Font font)
    {
        if (font != _font || _textMaterial == null) return;
        var texture = font.material.mainTexture; _textMaterial.mainTexture = texture;
        _textMaterial.SetVector("_TextureSampleAdd", texture is Texture2D atlas && atlas.format == TextureFormat.Alpha8 ? new Vector4(1,1,1,0) : Vector4.zero);
    }
    private void End() { _active = null; _owner = -1; _seatLift = 0; _pull.Reset(); }
    private void Cancel()
    {
        End(); _request.Reset();
        for (var i = 0; i < 2; i++) { _buttonGuides[i].Cancel(); _grips[i].Reset(); _switchGrips[i].Reset(); _seatTouches[i].Suspend(); _presses[i].Reset(); _near[i] = null; foreach (var gate in _touch[i].Values) gate.Reset(); }
    }
    private void Pause()
    {
        _nativeCover?.SetVisible(false);
        _ready = false; Cancel(); if (_root != null) _root.gameObject.SetActive(false);
    }
    private void Release()
    {
        Pause(); _nativeGear?.Dispose(); _nativeGear = null; _nativeCover?.Dispose(); _nativeCover = null; Font.textureRebuilt -= RefreshFont;
        if (_root != null) Destroy(_root.gameObject);
        foreach (var material in new[] { _base,_cap,_hover,_on,_metal,_textMaterial }) if (material != null) Destroy(material);
        foreach (var mesh in _hardwareMeshes) Destroy(mesh); _hardwareMeshes.Clear();
        _root = _seat = null; _aircraft = null; _actuators.Clear(); _faces.Clear(); _labels.Clear(); foreach (var gates in _touch) gates.Clear();
        _switchReadyAt.Clear();
        for (var i = 0; i < 2; i++) { _buttonGuides[i].Reset(); _buttonControls[i] = null; _seatTouches[i].Reset(); }
    }
    private void OnDisable() => Release();
    private void OnDestroy() { Release(); if (Instance == this) Instance = null; }
}
