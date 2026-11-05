using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using Valve.OpenXR.Utils;

var assertions = 0;
void yes(bool value) { assertions++; if (!value) throw new Exception("Controller runtime assertion " + assertions); }
void near(float a, float b) => yes(Math.Abs(a - b) < .0001f);
TrackingAvailabilityChecks.Run(yes);
DeviceState tracking() => new DeviceState().Set(CommonUsages.isTracked, true)
    .Set(CommonUsages.trackingState, InputTrackingState.Position | InputTrackingState.Rotation)
    .Set(CommonUsages.devicePosition, new Vector3(.2f, 1.1f, -.3f)).Set(CommonUsages.deviceRotation, Quaternion.identity);
void install(bool left, XRController controller, DeviceState state)
{
    if (left)
        XRController.leftHand = controller;
    else
        XRController.rightHand = controller;
    InputDevices.Devices[left ? XRNode.LeftHand : XRNode.RightHand] = new InputDevice(state);
    Time.frameCount++;
}
void button(XRController controller, string name, bool pressed = true) => controller.Controls[name] = new ButtonControl { Value = pressed ? 1 : 0 };
InputDevices.Devices[XRNode.Head] = new InputDevice(tracking());

// Real source sampling, with only runtime I/O replaced. Names match the bundled
// OpenXR action maps, including generated layouts derived from their classes.
foreach (var controller in new XRController[] { new DerivedTouch(), new MetaQuestTouchPlusControllerProfile.QuestTouchPlusController(),
             new MetaQuestTouchProControllerProfile.QuestProTouchController() })
    foreach (var left in new[] { true, false })
    {
        foreach (var name in new[] { "primaryButton", "secondaryButton", "thumbstickClicked", "menu", "primaryTouched", "secondaryTouched", "triggerTouched" })
            button(controller, name);
        controller.Controls["grip"] = new AxisControl { Value = .72f };
        controller.Controls["trigger"] = new AxisControl { Value = .43f };
        controller.Controls["thumbstick"] = new Vector2Control { Value = new Vector2(.2f, -.6f) };
        install(left, controller, tracking());
        var sample = TrackedControllers.Read(left);
        yes(sample.Valid && sample.Profile == ControllerHardwareProfile.Touch && !sample.NativeFrame);
        yes(TrackedControllers.Profile(left) == ControllerHardwareProfile.Touch && !TrackedControllers.NativeFrame(left));
        yes(sample.Primary && sample.Secondary && sample.StickClick && sample.HasAxis);
        yes(sample.Menu == left && !sample.Shoulder && !sample.Top && !sample.Outside);
        near(sample.Grip, .72f);
        near(sample.Trigger, .43f);
        near(sample.Axis.y, -.6f);
        yes(sample.PrimaryTouched && sample.SecondaryTouched && !sample.TopTouched && !sample.OutsideTouched && sample.TriggerTouched && sample.HasThumbTouch && sample.HasTriggerTouch);
    }

var touch = new DerivedTouch();
foreach (var name in new[] { "primaryButton", "secondaryButton", "thumbstickClicked", "menu", "gripPressed", "triggerPressed" }) button(touch, name);
touch.Controls["grip"] = new AxisControl { Value = .9f };
touch.Controls["trigger"] = new AxisControl { Value = .9f };
var common = tracking().Set(CommonUsages.grip, .27f).Set(CommonUsages.trigger, .36f)
    .Set(CommonUsages.gripButton, false).Set(CommonUsages.primaryButton, false).Set(CommonUsages.secondaryButton, false)
    .Set(CommonUsages.primary2DAxisClick, false).Set(CommonUsages.menuButton, false);
install(true, touch, common);
var legacy = TrackedControllers.Read(true);
yes(!legacy.Primary && !legacy.Secondary && !legacy.StickClick && !legacy.Menu);
near(legacy.Grip, .27f);
near(legacy.Trigger, .36f);
// A coherent sample stays unchanged within a frame, then sees the next input.
common.Set(CommonUsages.trigger, .81f);
near(TrackedControllers.Read(true).Trigger, .36f);
Time.frameCount++;
near(TrackedControllers.Read(true).Trigger, .81f);

touch.Controls.Clear();
button(touch, "primaryTouched");
button(touch, "thumbstickTouched");
install(true, touch, tracking());
var presence = TrackedControllers.Read(true);
yes(presence.PrimaryTouched && presence.ThumbstickTouched && !presence.Primary && !presence.StickClick);
near(presence.Grip, 0);
near(presence.Trigger, 0);
button(touch, "gripPressed");
button(touch, "triggerPressed");
Time.frameCount++;
var clicks = TrackedControllers.Read(true);
near(clicks.Grip, 1);
near(clicks.Trigger, 1);

var frame = new DerivedFrame();
foreach (var name in new[] { "faceButtonBottom", "faceButtonInside", "faceButtonTop", "faceButtonOutside", "shoulder", "menu", "thumbstickClicked", "gripPressed", "faceButtonBottomTouched", "faceButtonInsideTouched", "faceButtonTopTouched", "faceButtonOutsideTouched" }) button(frame, name);
var frameState = tracking().Set(CommonUsages.primaryButton, false).Set(CommonUsages.secondaryButton, false)
    .Set(CommonUsages.primary2DAxisClick, false).Set(CommonUsages.menuButton, false).Set(CommonUsages.grip, .1f).Set(CommonUsages.gripButton, false);
install(false, frame, frameState);
var frameSample = TrackedControllers.Read(false);
yes(frameSample.NativeFrame && frameSample.Profile == ControllerHardwareProfile.SteamFrame);
yes(frameSample.Primary && frameSample.Secondary && frameSample.Top && frameSample.Outside && frameSample.Shoulder && frameSample.Menu && frameSample.StickClick);
near(frameSample.Grip, 1);
yes(frameSample.PrimaryTouched && frameSample.SecondaryTouched && frameSample.TopTouched && frameSample.OutsideTouched && frameSample.ButtonTouched);

// Aim validity is explicit so free gloves can use a stable hardware fallback.
yes(!frameSample.HasAimPose);
var pointer = new UnityEngine.XR.OpenXR.Input.PoseControl();
pointer.isTracked.Value = 1;
pointer.trackingState.Value = 3;
pointer.position.Value = new Vector3(.21f, 1.12f, -.25f);
pointer.rotation.Value = new Quaternion(.3f, 0, 0, .9539392f);
frame.Controls["pointer"] = pointer;
Time.frameCount++;
var aimedFrame = TrackedControllers.Read(false);
yes(aimedFrame.HasAimPose && aimedFrame.Valid);
near(aimedFrame.AimRotation.x, .3f);
pointer.isTracked.Value = 0;
Time.frameCount++;
yes(!TrackedControllers.Read(false).HasAimPose);
pointer.isTracked.Value = 1;
pointer.rotation.Value = new Quaternion(float.NaN, 0, 0, 1);
Time.frameCount++;
yes(!TrackedControllers.Read(false).HasAimPose && TrackedControllers.Read(false).Valid);

touch.Controls.Clear();
var invalid = tracking().Set(CommonUsages.trigger, float.NaN);
install(true, touch, invalid);
yes(!TrackedControllers.Read(true).Valid);
touch.Controls["trigger"] = new AxisControl { Value = .25f };
Time.frameCount++;
yes(TrackedControllers.Read(true).Valid);
near(TrackedControllers.Read(true).Trigger, .25f);
OpenXRUtility.IsSessionFocused = false;
Time.frameCount++;
yes(!TrackedControllers.Read(true).Valid);
OpenXRUtility.IsSessionFocused = true;
XRSettings.enabled = false;
Time.frameCount++;
yes(!TrackedControllers.Read(true).Valid);
XRSettings.enabled = true;
invalid.Set(CommonUsages.trackingState, InputTrackingState.Rotation);
Time.frameCount++;
yes(!TrackedControllers.Read(true).Valid);
XRController.leftHand = new XRController();
yes(TrackedControllers.Profile(true) == ControllerHardwareProfile.Other);
XRController.leftHand = null;
yes(TrackedControllers.Profile(true) == ControllerHardwareProfile.Unknown);

var settings = new OpenXRSettings();
settings.EnableNovrControllerProfiles();
var existingGaze = settings.features.Single(f => f is EyeGazeInteraction);
yes(existingGaze.enabled && !existingGaze.required && existingGaze.openxrExtensionStrings == EyeGazeInteraction.extensionString);
var existingFrame = settings.features.Single(f => f is SteamFrameControllerProfile);
settings.EnableNovrControllerProfiles();
yes(settings.features.Count(f => f is SteamFrameControllerProfile) == 1 && settings.features.Contains(existingFrame));
yes(settings.features.Count(f => f is OculusTouchControllerProfile) == 1);
yes(settings.features.Single(f => f is MetaQuestTouchPlusControllerProfile).openxrExtensionStrings == "XR_META_touch_controller_plus");
yes(settings.features.Single(f => f is MetaQuestTouchProControllerProfile).openxrExtensionStrings == "XR_FB_touch_controller_pro");
yes(existingFrame.openxrExtensionStrings == "XR_VALVE_frame_controller_interaction" && settings.features.All(f => f.enabled));
yes(settings.features.Count(f => f is EyeGazeInteraction) == 1 && settings.features.Contains(existingGaze));

// Production report updates once at startup, then only while its page is open.
var performance = new PcVrCapabilities();
var updatePerformance = typeof(PcVrCapabilities).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
void performanceTick(float time) { Time.unscaledTime = time; updatePerformance.Invoke(performance, null); }
Debug.Logs.Clear();
Debug.Warnings.Clear();
performanceTick(0);
yes(PcVrCapabilities.Report.Contains("SteamVR") && PcVrCapabilities.Report.Contains("Direct3D11"));
yes(PcVrCapabilities.Report.Contains("3028 × 3028 per eye") && PcVrCapabilities.Report.Contains("1920 × 1080"));
yes(!PcVrCapabilities.Report.Contains("gaze", StringComparison.OrdinalIgnoreCase) && !PcVrCapabilities.Report.Contains("foveat", StringComparison.OrdinalIgnoreCase));
yes(Debug.Logs.Count == 1 && OpenXRRuntime.NameReads == 1);
XRSettings.eyeTextureWidth = 2400;
Screen.width = 1600;
performanceTick(2);
yes(OpenXRRuntime.NameReads == 1 && PcVrCapabilities.Report.Contains("3028"));
NOVR.VrUi.WristMenu.IsPerformancePage = true;
performanceTick(3);
yes(PcVrCapabilities.Report.Contains("2400 × 3028") && PcVrCapabilities.Report.Contains("1600 × 1080"));
yes(Debug.Logs.Count == 1 && OpenXRRuntime.NameReads == 2);
performanceTick(3.5f);
yes(OpenXRRuntime.NameReads == 2);
XRSettings.enabled = false;
performanceTick(4);
yes(PcVrCapabilities.Report == "VR runtime is not active.");
NOVR.VrUi.WristMenu.IsPerformancePage = false;
XRSettings.enabled = true;
OpenXRRuntime.Fail = true;
performanceTick(5);
performanceTick(6);
yes(Debug.Warnings.Count == 1 && PcVrCapabilities.Report.Contains("not available yet"));
OpenXRRuntime.Fail = false;
XRSettings.eyeTextureWidth = 0;
performanceTick(7);
yes(Debug.Logs.Count == 2 && PcVrCapabilities.Report.Contains("Waiting for eye textures"));
Console.WriteLine($"{assertions} controller runtime/profile assertions passed (source linked; no headset or game). ");

sealed class DerivedTouch : OculusTouchControllerProfile.OculusTouchController
{
}
sealed class DerivedFrame : SteamFrameControllerProfile.SteamFrameController
{
}
