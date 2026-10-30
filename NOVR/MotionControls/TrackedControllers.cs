using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace NOVR.MotionControls;

internal struct ControllerSample
{
    public bool Valid, HasAxis, StickClick, Primary, Secondary, Menu, Shoulder, Top, Outside, NativeFrame;
    // Presence is visual-only and never aliases a button press or flight action.
    public bool HasThumbTouch, HasTriggerTouch, ThumbstickTouched, ButtonTouched, PrimaryTouched, ThumbrestTouched, TriggerTouched, GripTouched;
    public InputDevice Device;
    public float Grip, Trigger;
    public Vector3 Position, AimPosition;
    public Quaternion Rotation, AimRotation;
    public Vector2 Axis;
}

internal static class TrackedControllers
{
    private static InputDevice? _reportedLeft, _reportedRight;
    private static int _availableFrame = -1;
    private static bool _available;
    private static readonly int[] ReadFrames = { -1, -1 };
    private static readonly ControllerSample[] Samples = new ControllerSample[2];
    private static Vector3 _sampleSeat, _sampleAnchor, _sampleTranslation;
    private static Quaternion _sampleRotation;
    // One coherent runtime sample per hand per frame, shared by input, UI and visuals.
    // Recenter/seat edits invalidate mapped samples immediately.
    public static bool Available
    {
        get
        {
            if (_availableFrame != Time.frameCount) { _availableFrame = Time.frameCount; _available = CheckAvailable(); }
            return _available;
        }
    }
    private static bool CheckAvailable()
    {
            // The desktop mirror can lose focus while the streamed headset still
            // owns the OpenXR session. The runtime, not the window, owns VR input.
            if (!XRSettings.enabled || !OpenXRUtility.IsSessionFocused) return false;
            var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            return head.isValid && head.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) && tracked &&
                   (!head.TryGetFeatureValue(CommonUsages.userPresence, out var present) || present);
    }

    internal static bool NativeFrame(bool left) => (left ? XRController.leftHand : XRController.rightHand)
        is Valve.OpenXR.Utils.SteamFrameControllerProfile.SteamFrameController;

    public static ControllerSample Read(bool left)
    {
        var seat = CockpitProfiles.SeatOffset;
        if (_sampleSeat != seat || _sampleAnchor != NOVRHeadsetData.TranslationAnchor ||
            _sampleTranslation != NOVRHeadsetData.TranslationCalibrationOffset || _sampleRotation != NOVRHeadsetData.RotationCalibrationOffset)
        {
            ReadFrames[0] = ReadFrames[1] = -1; _sampleSeat = seat; _sampleAnchor = NOVRHeadsetData.TranslationAnchor;
            _sampleTranslation = NOVRHeadsetData.TranslationCalibrationOffset; _sampleRotation = NOVRHeadsetData.RotationCalibrationOffset;
        }
        var i = left ? 0 : 1;
        if (ReadFrames[i] == Time.frameCount) return Samples[i];
        ReadFrames[i] = Time.frameCount;
        return Samples[i] = ReadRuntime(left);
    }
    private static ControllerSample ReadRuntime(bool left)
    {
        if (!Available) return default;
        var device = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
        if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) || !tracked ||
            !device.TryGetFeatureValue(CommonUsages.trackingState, out var tracking) ||
            (tracking & (InputTrackingState.Position | InputTrackingState.Rotation)) !=
            (InputTrackingState.Position | InputTrackingState.Rotation) ||
            !device.TryGetFeatureValue(CommonUsages.devicePosition, out var position) ||
            !device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation) ||
            !Finite(position) || !Finite(rotation)) return default;

        var hasGrip = device.TryGetFeatureValue(CommonUsages.grip, out var grip);
        device.TryGetFeatureValue(CommonUsages.gripButton, out var gripButton);
        if (!device.TryGetFeatureValue(CommonUsages.trigger, out var trigger))
            trigger = device.TryGetFeatureValue(CommonUsages.triggerButton, out var triggerButton) && triggerButton ? 1f : 0f;
        var hasAxis = device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var axis) &&
                      FlightControlMath.IsFinite(axis.x) && FlightControlMath.IsFinite(axis.y);
        device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out var stickClick);
        device.TryGetFeatureValue(CommonUsages.primaryButton, out var primary);
        device.TryGetFeatureValue(CommonUsages.secondaryButton, out var secondary);
        device.TryGetFeatureValue(CommonUsages.menuButton, out var menu);

        var inputDevice = left ? XRController.leftHand : XRController.rightHand;
        // OpenXR generates device-specific layouts derived from the registered
        // profile, so their layout names need not equal the base layout name.
        var nativeFrame = NativeFrame(left);
        if ((left ? _reportedLeft : _reportedRight)?.Equals(device) != true)
        {
            if (left) _reportedLeft = device; else _reportedRight = device;
            Debug.Log($"[NOVR] {(left ? "Left" : "Right")} controller: {device.name}; layout {inputDevice?.layout ?? "unavailable"}; {(nativeFrame ? "native Steam Frame bindings" : "generic XR / Touch-compatible bindings")}.");
        }
        bool Button(string name) => inputDevice?.TryGetChildControl<ButtonControl>(name)?.isPressed == true;
        grip = FlightControlMath.GripValue(hasGrip, grip, gripButton || nativeFrame && Button("gripPressed"));
        bool Touch(string name, out bool supported)
        {
            var control = inputDevice?.TryGetChildControl<ButtonControl>(name);
            supported = control != null; return control?.isPressed == true;
        }
        var stickTouch = Touch("thumbstickTouched", out var hasStickTouch);
        var triggerTouch = Touch("triggerTouched", out var hasTriggerTouch);
        var primaryTouch = Touch(nativeFrame ? "faceButtonBottomTouched" : "primaryTouched", out var hasButtonTouch);
        var secondaryTouch = Button(nativeFrame ? "faceButtonInsideTouched" : "secondaryTouched");
        var faceTouch = primaryTouch || secondaryTouch || nativeFrame && (Button("faceButtonTopTouched") || Button("faceButtonOutsideTouched"));
        var restTouch = Touch("thumbrestTouched", out var hasRestTouch);
        var aimPosition = position;
        var aimRotation = rotation;
        // Use the runtime's aim pose, which differs from the grip pose on Frame.
        var pointer = inputDevice?.TryGetChildControl<UnityEngine.XR.OpenXR.Input.PoseControl>("pointer");
        if (pointer != null && pointer.isTracked.isPressed && (pointer.trackingState.ReadValue() & 3) == 3 &&
            Finite(pointer.position.ReadValue()) && Finite(pointer.rotation.ReadValue()))
        {
            aimPosition = pointer.position.ReadValue();
            aimRotation = pointer.rotation.ReadValue();
        }
        return new ControllerSample
        {
            Valid = FlightControlMath.IsFinite(grip) && FlightControlMath.IsFinite(trigger),
            Device = device, Grip = grip, Trigger = trigger, HasAxis = hasAxis,
            Axis = hasAxis ? axis : Vector2.zero,
            StickClick = nativeFrame ? Button("thumbstickClicked") : stickClick,
            Primary = nativeFrame ? Button("faceButtonBottom") : primary,
            Secondary = nativeFrame ? Button("faceButtonInside") : secondary,
            Menu = nativeFrame ? Button("menu") : menu,
            Shoulder = Button("shoulder"), Top = Button("faceButtonTop"), Outside = Button("faceButtonOutside"),
            NativeFrame = nativeFrame,
            HasThumbTouch = hasStickTouch || hasButtonTouch || hasRestTouch, HasTriggerTouch = hasTriggerTouch,
            ThumbstickTouched = stickTouch, ButtonTouched = faceTouch, PrimaryTouched = primaryTouch, ThumbrestTouched = restTouch,
            TriggerTouched = triggerTouch, GripTouched = Button("gripTouched"),
            Position = MapPosition(position), Rotation = NOVRHeadsetData.RotationCalibrationOffset * rotation,
            AimPosition = MapPosition(aimPosition), AimRotation = NOVRHeadsetData.RotationCalibrationOffset * aimRotation
        };
    }

    public static Vector3 MapPosition(Vector3 position) =>
        NOVRHeadsetData.MapTrackingPosition(position) + CockpitProfiles.SeatOffset;

    public static bool Finite(Vector3 value) => FlightControlMath.IsFinite(value.x) &&
        FlightControlMath.IsFinite(value.y) && FlightControlMath.IsFinite(value.z);
    private static bool Finite(Quaternion value) => Finite(new Vector3(value.x, value.y, value.z)) &&
        FlightControlMath.IsFinite(value.w) && Quaternion.Dot(value, value) > 0.5f;
}
