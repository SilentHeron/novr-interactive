using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using NOVR.VrCamera;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace NOVR.MotionControls;

[DefaultExecutionOrder(-1200)]
public sealed class MotionFlightController : MonoBehaviour
{
    internal static MotionFlightController? Instance { get; private set; }
    private readonly FlightGrip _stickGrip = new();
    private readonly FlightGrip _throttleGrip = new();
    private readonly StickLimitFeedback _stickLimitFeedback = new();
    private MotionControlVisuals? _visuals;
    private bool _visualsFailed;
    private Aircraft? _aircraft;
    private Aircraft? _throttleCommandAircraft;
    private float _throttleCommand;
    private Quaternion _neutralStick;
    private float _throttleStartPosition;
    private float _throttleStartValue;
    private float _milPower = 1f;
    private float _nextMilCheck;
    private bool _hasVtol;
    private TiltWingController? _tiltWing;
    private int _throttleZone;
    private float _nextThrottlePulse;
    private bool MilDetent => Settings.MilDetent.Value && !LiftCollective && _milPower < 1f;
    private float _calibrationHold;
    private bool _calibrationLatched;
    private Vector3 _lastCalibrationPosition;
    private Vector3 _lastTranslationAnchor;
    private Quaternion _lastCalibrationRotation;
    private bool _ready;
    private float _placementTime = -1f;
    private float _placementConfirmationUntil;
    private string _placementFeedback = "";
    internal string? PlacementHint => _placementTime >= 0
        ? (Time.unscaledTime < _placementTime
            ? $"PLACE YOUR HANDS  •  {Mathf.CeilToInt(_placementTime - Time.unscaledTime)}"
            : "PLACEMENT  •  " + Status)
        : Time.unscaledTime < _placementConfirmationUntil ? _placementFeedback : null;
    internal string? TrackingHint => _ready && (!_stickHand.Valid || !_throttleHand.Valid) ? Status : null;
    private string? _reportedStatus;
    private float _nextStatusReport;
    internal string Status { get; private set; } = "Waiting for cockpit";
    internal void SchedulePlacement()
    {
        _placementTime = Time.unscaledTime + 3f;
        Status = "Place your hands; waiting 3 seconds";
        Debug.Log("[NOVR] Handle placement scheduled for three seconds from now.");
    }
    private ControllerSample _stickHand;
    private ControllerSample _throttleHand;
    private MotionControlSettings Settings => ModConfiguration.Instance.MotionControls;

    internal bool FlightActive => _ready && !CockpitLayout.Active && !VrUi.WristMenu.IsOpen;
    internal void CancelPlacement() { _placementTime = -1; ResetGrips(); }
    internal bool StickHeld => _ready && _stickGrip.Held;
    internal bool ThrottleHeld => _ready && _throttleGrip.Held;
    // Render-only attachment: flight input still reads the real controller pose.
    internal bool TryGetHeldHandPose(bool left, out Pose pose, out bool stick, out bool collective)
    {
        pose = default; stick = left == Settings.LeftHanded.Value; collective = !stick && LiftCollective;
        var editing = CockpitLayout.HeldByHand(left);
        if (_visuals != null && editing is CockpitLayout.Element.Stick or CockpitLayout.Element.Throttle)
        { stick = editing == CockpitLayout.Element.Stick; collective = !stick && LiftCollective; pose = _visuals.GripPose(stick); return true; }
        if (!FlightActive || _visuals == null || !(stick ? StickHeld : ThrottleHeld)) return false;
        pose = _visuals.GripPose(stick);
        return true;
    }
    private float CommandTravel => MilDetent ? ThrottleDetent.CommandTravel(Settings.ThrottleTravel.Value) : Settings.ThrottleTravel.Value;
    internal float Pitch { get; private set; }
    internal float Roll { get; private set; }
    internal float Yaw { get; private set; }
    internal bool HasYaw => Settings.TwistYaw.Value || Settings.ThumbstickYaw.Value && _stickHand.HasAxis;
    internal float Throttle { get; private set; }
    internal float VtolAxis
    {
        get
        {
            if (!FlightActive || !ThrottleHeld || !_throttleHand.Valid || !_throttleHand.HasAxis || !_hasVtol || (!Settings.LeftHanded.Value && UniversalCockpitPanel.LeftStickSlaved) ||
                _aircraft?.IsAutoHoverEnabled() == true || Mathf.Abs(_throttleHand.Axis.y) < .35f) return 0;
            // Native relative-axis semantics: +/-1 steps the command and also
            // tells the nozzle controller to leave its automatic operating mode.
            if (_tiltWing != null) HarmonyLib.AccessTools.Field(typeof(TiltWingController), "autoTilt")?.SetValue(_tiltWing, false);
            return Mathf.Sign(_throttleHand.Axis.y);
        }
    }
    private Vector3 StickPosition => HandedPosition(CockpitProfiles.StickPosition);
    private Vector3 ThrottlePosition => HandedPosition(CockpitProfiles.ThrottlePosition);
    internal static bool UsesLiftCollective(Aircraft? aircraft) => aircraft != null && aircraft.GetControlsFilter() is HeloControlsFilter;
    private bool LiftCollective => UsesLiftCollective(_aircraft);
    private float ThrottleHandPosition => LiftCollective ? _throttleHand.Position.y : _throttleHand.Position.z;
    internal Vector3 ThrottleOffset
    {
        get
        {
            var offset = FlightControlMath.ThrottleOffset(LiftCollective, MilDetent ? ThrottleDetent.Position(Throttle, _milPower, Settings.ThrottleTravel.Value) : Throttle, Settings.ThrottleTravel.Value);
            return new Vector3(0, offset.height, offset.forward);
        }
    }

    private void Awake() => Instance = this;

    private void Update()
    {
        if (Keyboard.current?.f7Key.wasPressedThisFrame == true)
        {
            CockpitLayout.Instance?.Finish(false);
            Settings.Enabled.Value = !Settings.Enabled.Value;
            if (!Settings.Enabled.Value) _placementTime = -1f;
            ResetGrips();
            Debug.Log($"[NOVR] Motion flight controls {(Settings.Enabled.Value ? "enabled" : "disabled")}.");
        }
        if (Keyboard.current?.f8Key.wasPressedThisFrame == true)
        {
            Settings.Enabled.Value = true;
            VrUi.WristMenu.Close();
            _placementTime = Time.unscaledTime;
        }
        Sample();
        // Brief tracking/focus or menu transitions must not silently discard the
        // user's placement request. Retry for a bounded period once ready.
        if (_placementTime >= 0f && Time.unscaledTime > _placementTime + 10f)
        {
            _placementTime = -1f;
            _placementFeedback = "PLACEMENT TIMED OUT  •  " + Status;
            _placementConfirmationUntil = Time.unscaledTime + 6f;
            Debug.LogWarning($"[NOVR] Handle placement timed out: {Status}. Try Place handles again after resolving this state.");
        }
        if (!_ready || CockpitLayout.Active || VrUi.WristMenu.IsOpen) return;
        if (_placementTime >= 0f && Time.unscaledTime >= _placementTime && _stickHand.Valid && _throttleHand.Valid)
        {
            _placementTime = -1f;
            PlaceAtHands();
        }

        // Thumbstick clicks may be used for braking/CM selection while flying.
        // Placement is only a hands-free gesture, never a side effect of those bindings.
        var placementGesture = !UniversalCockpitPanel.LeftStickSlaved && _stickHand.Valid && _throttleHand.Valid && !StickHeld && !ThrottleHeld &&
                               _stickHand.StickClick && _throttleHand.StickClick;
        if (placementGesture)
        {
            _calibrationHold += Time.unscaledDeltaTime;
            if (_calibrationHold >= 0.8f && !_calibrationLatched)
            {
                PlaceAtHands();
                _calibrationLatched = true;
            }
        }
        else
        {
            _calibrationHold = 0f;
            _calibrationLatched = false;
        }
    }

    private void LateUpdate()
    {
        if (!_ready || _visuals == null) return;
        _visuals.Update(StickPosition, ThrottlePosition, Settings.ThrottleTravel.Value,
            Settings.StickAngle.Value, Pitch, Roll, Throttle, StickHeld, ThrottleHeld,
            CockpitLayout.Active ? CockpitLayout.Hovered(CockpitLayout.Element.Stick) : IsNearStick(),
            CockpitLayout.Active ? CockpitLayout.Hovered(CockpitLayout.Element.Throttle) : IsNearThrottle(), _stickHand.Valid, _stickHand.Position,
            _throttleHand.Valid, _throttleHand.Position, _stickGrip.Latched, _throttleGrip.Latched,
            Settings.StickTapGrip.Value, Settings.ThrottleTapGrip.Value, _aircraft != null && _aircraft.GetInputs().brake > .05f,
            _aircraft?.IsAutoHoverEnabled() == true ? "HOVER INPUT" :
                _aircraft?.GetAircraftParameters().takeoffDistance == 0 ? "COLLECTIVE" : "THROTTLE", LiftCollective,
            Yaw * Settings.YawAngle.Value, MilDetent ? _milPower : -1f);
    }

    // Capture the pilot's command before native hover/flight filters turn it
    // into engine power. Regrabbing must not adopt the hover controller's output.
    internal void ObserveThrottle(Pilot pilot, float command)
    {
        if (pilot == null || pilot.currentState != pilot.playerState ||
            !GameManager.IsLocalAircraft(pilot.aircraft) || !FlightControlMath.IsFinite(command)) return;
        _throttleCommandAircraft = pilot.aircraft;
        _throttleCommand = Mathf.Clamp01(command);
    }

    // Called from both the render loop and the native pilot input hooks, so a physics tick
    // never relies on stale controller state just because Unity ran FixedUpdate first.
    internal bool SampleFor(Pilot pilot)
    {
        if (!isActiveAndEnabled)
        {
            Suspend();
            return false;
        }
        Sample();
        // Pilot.playerControlled is a legacy serialized field with no writers in
        // 0.34.3. Local aircraft ownership and the active pilot state are authoritative.
        return FlightActive && pilot != null && !pilot.dead &&
               pilot.aircraft == _aircraft && pilot.currentState == pilot.playerState;
    }

    private void Sample()
    {
        CockpitProfiles.Instance?.Refresh();
        if (!Settings.Enabled.Value) _visualsFailed = false;
        if (!TryGetCockpit(out var aircraft, out var trackingRoot))
        {
            Suspend();
            return;
        }

        if (_aircraft != aircraft) _visualsFailed = false;
        if (_visualsFailed)
        {
            ReportStatus("Handle models unavailable; toggle motion controls to retry");
            Suspend();
            return;
        }
        if (_aircraft != aircraft || _visuals == null || _visuals.Root == null || _visuals.Root.parent != trackingRoot)
        {
            _visuals?.Dispose();
            _visuals = null;
            _aircraft = aircraft;
            _milPower = MilitaryPower.Find(aircraft);
            _nextMilCheck = Time.unscaledTime + 1f;
            _tiltWing = aircraft.GetComponentInChildren<TiltWingController>(true);
            _hasVtol = _tiltWing != null || aircraft.GetComponentInChildren<DuctedThrustSystem>(true) != null ||
                aircraft.GetComponentInChildren<SwivelDuctSystem>(true) != null;
            try { _visuals = new MotionControlVisuals(trackingRoot); _visuals.UseAircraftModels(aircraft); }
            catch (System.Exception exception)
            {
                _visualsFailed = true;
                ReportStatus("Handle models unavailable; toggle motion controls to retry");
                Debug.LogError($"[NOVR] Could not create motion-control models: {exception}");
                Suspend();
                return;
            }
            Debug.Log($"[NOVR] Created motion control models for {aircraft.name}; tracking root={trackingRoot.name}, camera={APIBus.MainCamera?.name}, cockpit layer={(int)LayerHelper.Layers.Cockpit}.");
            ResetGrips();
        }

        // The engine registry can finish initializing after the cockpit appears.
        // Never change the command mapping in the middle of a held throttle.
        if (_milPower >= 1f && !_throttleGrip.Held && Time.unscaledTime >= _nextMilCheck)
        { _nextMilCheck = Time.unscaledTime + 2f; _milPower = MilitaryPower.Find(aircraft); }

        // NOVR's headset recentering changes the mapping from tracked hands into the cockpit.
        if (_lastCalibrationPosition != NOVRHeadsetData.TranslationCalibrationOffset ||
            _lastTranslationAnchor != NOVRHeadsetData.TranslationAnchor ||
            _lastCalibrationRotation != NOVRHeadsetData.RotationCalibrationOffset)
        {
            ResetGrips();
            _lastCalibrationPosition = NOVRHeadsetData.TranslationCalibrationOffset;
            _lastTranslationAnchor = NOVRHeadsetData.TranslationAnchor;
            _lastCalibrationRotation = NOVRHeadsetData.RotationCalibrationOffset;
        }

        _ready = true;
        _visuals.SetVisible(true);
        _visuals.Root.localPosition = Vector3.zero;
        _stickHand = TrackedControllers.Read(Settings.LeftHanded.Value);
        _throttleHand = TrackedControllers.Read(!Settings.LeftHanded.Value);
        if (!_throttleGrip.Held) Throttle = _throttleCommandAircraft == aircraft
            ? _throttleCommand : Mathf.Clamp01(aircraft.GetInputs().throttle);

        if (CockpitLayout.Active || VrUi.WristMenu.IsOpen) { ResetGrips(); ReportStatus(CockpitLayout.Active ? CockpitLayout.Hint : "Handle input paused while wrist menu is open"); return; }

        var stickWasHeld = _stickGrip.Held;
        // A physical cockpit interaction owns its hand until release, including
        // physics-driven SampleFor calls. Reset the flight gate while owned so
        // a held switch cannot transfer into a nearby flight handle mid-grip.
        _stickGrip.Update(_stickHand.Valid && !Fs20NativeCockpit.OwnsHand(Settings.LeftHanded.Value) && !IfritNativeCockpit.OwnsHand(Settings.LeftHanded.Value), _stickHand.Grip,
            IsNearStick() && (stickWasHeld || !Fs20NativeCockpit.SwitchWithinReach(Settings.LeftHanded.Value, _stickHand, trackingRoot) && !IfritNativeCockpit.ControlWithinReach(Settings.LeftHanded.Value, _stickHand, trackingRoot)), Settings.StickTapGrip.Value,
            Time.realtimeSinceStartup, Vector3.Distance(_stickHand.Position, StickPosition) <= Mathf.Max(.4f, Settings.GrabRadius.Value * 2));
        if (_stickGrip.Held && !stickWasHeld)
        {
            _neutralStick = _stickHand.Rotation;
            Pulse(_stickHand.Device);
        }

        if (stickWasHeld && !_stickGrip.Held && _stickHand.Valid) InteractionHaptics.Pulse(_stickHand.Device, .12f, .02f, InteractionCue.Release);

        Pitch = Roll = Yaw = 0f;
        var stickTwist = 0f;
        if (_stickGrip.Held)
        {
            // Relative rotation in cockpit axes, independent of the controller's grip orientation.
            var relative = _stickHand.Rotation * Quaternion.Inverse(_neutralStick);
            var tilt = relative * Vector3.up;
            (Pitch, Roll) = FlightControlMath.Stick(tilt.x, tilt.y, tilt.z, Settings.StickAngle.Value,
                Settings.Deadzone.Value, Settings.ResponseExponent.Value);
            stickTwist = Settings.TwistYaw.Value ? FlightControlMath.StickTwist(relative.y, relative.w,
                Settings.YawAngle.Value, Settings.Deadzone.Value, Settings.ResponseExponent.Value) : 0f;
            var thumb = Settings.ThumbstickYaw.Value && _stickHand.HasAxis && !(Settings.LeftHanded.Value && UniversalCockpitPanel.LeftStickSlaved)
                ? FlightControlMath.Axis(_stickHand.Axis.x, 0.12f, 1f) : 0f;
            Yaw = Mathf.Clamp(stickTwist + thumb, -1f, 1f);
        }
        var limitPulse = _stickLimitFeedback.Update(_stickGrip.Held && _stickHand.Valid,
            Pitch, Roll, stickTwist, Time.unscaledTime, Time.frameCount);
        if (limitPulse.strength > 0f)
            InteractionHaptics.PulseOnly(_stickHand.Device, limitPulse.strength, limitPulse.seconds);

        var throttleWasHeld = _throttleGrip.Held;
        _throttleGrip.Update(_throttleHand.Valid && !Fs20NativeCockpit.OwnsHand(!Settings.LeftHanded.Value) && !IfritNativeCockpit.OwnsHand(!Settings.LeftHanded.Value), _throttleHand.Grip,
            IsNearThrottle() && (throttleWasHeld || !Fs20NativeCockpit.SwitchWithinReach(!Settings.LeftHanded.Value, _throttleHand, trackingRoot) && !IfritNativeCockpit.ControlWithinReach(!Settings.LeftHanded.Value, _throttleHand, trackingRoot)), Settings.ThrottleTapGrip.Value,
            Time.realtimeSinceStartup, Vector3.Distance(_throttleHand.Position,
                ThrottlePosition + ThrottleOffset) <= Mathf.Max(.4f, Settings.GrabRadius.Value * 2));
        if (_throttleGrip.Held && !throttleWasHeld)
        {
            _throttleStartPosition = ThrottleHandPosition;
            _throttleStartValue = MilDetent ? ThrottleDetent.Input(Throttle, _milPower,
                ThrottleDetent.TravelMeters / CommandTravel) : Throttle;
            _throttleZone = Throttle <= 0 ? -2 : Throttle >= 1 ? 2 : MilDetent && Throttle == _milPower ? 0 : MilDetent && Throttle > _milPower ? 1 : -1;
            Pulse(_throttleHand.Device);
        }
        if (throttleWasHeld && !_throttleGrip.Held && _throttleHand.Valid) InteractionHaptics.Pulse(_throttleHand.Device, .12f, .02f, InteractionCue.Release);
        if (_throttleGrip.Held)
        {
            var input = _throttleStartValue + (ThrottleHandPosition - _throttleStartPosition) / CommandTravel;
            Throttle = MilDetent ? ThrottleDetent.Output(input, _milPower, ThrottleDetent.TravelMeters / CommandTravel)
                : Mathf.Clamp01(input);
            var zone = Throttle <= 0 ? -2 : Throttle >= 1 ? 2 : MilDetent && Throttle == _milPower ? 0 : MilDetent && Throttle > _milPower ? 1 : -1;
            if (zone != _throttleZone && (zone == 0 || zone == 1 || zone == -2 || zone == 2) && Time.unscaledTime >= _nextThrottlePulse)
            {
                if (zone == 0 || zone == 1) InteractionHaptics.Pulse(_throttleHand.Device, InteractionCue.Detent);
                else InteractionHaptics.Pulse(_throttleHand.Device, .3f, .045f, InteractionCue.Detent);
                _nextThrottlePulse = Time.unscaledTime + .12f;
            }
            _throttleZone = zone;
        }
        if (!_stickHand.Valid || !_throttleHand.Valid)
            ReportStatus(!_stickHand.Valid && !_throttleHand.Valid ? "Both controllers need tracking" :
                ((!_stickHand.Valid ? Settings.LeftHanded.Value : !Settings.LeftHanded.Value) ? "Left controller needs tracking" : "Right controller needs tracking"));
        else if (_stickGrip.Latched || _throttleGrip.Latched)
            ReportStatus($"Stick {(_stickGrip.Latched ? "latched" : _stickGrip.Held ? "held" : "free")} • {(LiftCollective ? "Collective" : "Throttle")} {(_throttleGrip.Latched ? "latched" : _throttleGrip.Held ? "held" : "free")}");
        else ReportStatus("Ready; release grips, then grab a handle");
    }

    private bool TryGetCockpit(out Aircraft aircraft, out Transform trackingRoot)
    {
        aircraft = null!;
        trackingRoot = null!;
        if (!Settings.Enabled.Value) return Blocked("Motion controls are off");
        if (!XRSettings.enabled) return Blocked("VR is not active");
        if (!OpenXRUtility.IsSessionFocused) return Blocked("Return to the game from SteamVR");
        if (CameraStateManager.cameraMode != CameraMode.cockpit) return Blocked("Switch to cockpit view");
        if (!GameManager.GetLocalAircraft(out aircraft)) return Blocked("No local aircraft");
        if (aircraft.cockpit == null || aircraft.cockpit.IsDetached()) return Blocked("No attached cockpit");
        if (!GameManager.flightControlsEnabled) return Blocked("Game flight input is disabled");
        if (Time.timeScale <= 0f) return Blocked("Game is paused");
        if (DynamicMap.mapMaximized) return Blocked("Close the tactical map");
        if (SceneSingleton<RadialMenuMain>.i != null && RadialMenuMain.IsInUse()) return Blocked("Close the radial menu");
        if (GameManager.controlMapper != null && GameManager.controlMapper.isOpen) return Blocked("Close the control settings");

        var hasLocalPilot = false;
        foreach (var pilot in aircraft.pilots)
        {
            if (pilot != null && !pilot.dead && pilot.currentState == pilot.playerState)
            {
                hasLocalPilot = true;
                break;
            }
        }
        if (!hasLocalPilot) return Blocked("No active local pilot in player state");

        var camera = APIBus.MainCamera;
        if (camera == null) return Blocked("Main camera is not ready");
        if (camera.GetComponent<NOVRPoseDriver>() == null) return Blocked($"Main camera has no VR pose driver ({camera.name})");
        if (camera.transform.parent == null) return Blocked($"Main camera has no tracking root ({camera.name})");
        var cameraState = SceneSingleton<CameraStateManager>.i;
        if (cameraState == null || cameraState.followingUnit != aircraft) return Blocked("Camera is not following the local aircraft");

        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!head.isValid || !head.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) || !tracked)
            return Blocked("Headset tracking is unavailable");
        if (head.TryGetFeatureValue(CommonUsages.userPresence, out var present) && !present) return Blocked("Headset reports it is not being worn");
        trackingRoot = camera.transform.parent;
        return true;
    }

    private bool Blocked(string reason) { ReportStatus(reason); return false; }

    private void ReportStatus(string status)
    {
        Status = status;
        if (status == _reportedStatus || Time.unscaledTime < _nextStatusReport) return;
        _reportedStatus = status;
        _nextStatusReport = Time.unscaledTime + 2f;
        Debug.Log($"[NOVR] Motion controls: {status}.");
    }

    private bool IsNearStick() => _stickHand.Valid &&
        Vector3.Distance(_stickHand.Position, StickPosition) <= Settings.GrabRadius.Value;

    private bool IsNearThrottle() => _throttleHand.Valid && Vector3.Distance(_throttleHand.Position,
        ThrottlePosition + ThrottleOffset) <= Settings.GrabRadius.Value;

    private Vector3 HandedPosition(Vector3 position) => Settings.LeftHanded.Value
        ? new Vector3(-position.x, position.y, position.z) : position;

    private void PlaceAtHands()
    {
        if (!_stickHand.Valid || !_throttleHand.Valid) return;
        CockpitProfiles.Instance?.SaveHandles(HandedPosition(_stickHand.Position),
            HandedPosition(_throttleHand.Position - ThrottleOffset));
        ModConfiguration.Instance.Config.Save();
        ResetGrips();
        Pulse(_stickHand.Device);
        Pulse(_throttleHand.Device);
        _placementFeedback = "HANDLES PLACED  •  RELEASE GRIPS, THEN GRAB";
        _placementConfirmationUntil = Time.unscaledTime + 3f;
        Debug.Log("[NOVR] Virtual stick and throttle placed at hands. Release grips before grabbing.");
    }

    internal void ResetGrips()
    {
        _stickGrip.Reset();
        _throttleGrip.Reset();
        _stickLimitFeedback.Reset();
        Pitch = Roll = Yaw = 0f;
    }

    private void Suspend()
    {
        ResetGrips();
        _ready = false;
        _calibrationHold = 0f;
        _calibrationLatched = false;
        if (_visuals?.Root != null) _visuals.SetVisible(false);
    }

    private void OnDisable() => Suspend();

    private void OnDestroy()
    {
        Suspend();
        _visuals?.Dispose();
        if (Instance == this) Instance = null;
    }

    private static void Pulse(UnityEngine.XR.InputDevice device) => InteractionHaptics.Pulse(device, cue: InteractionCue.Grab);

}
