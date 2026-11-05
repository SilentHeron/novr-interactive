using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using Valve.OpenXR.Utils;

internal static class TrackingAvailabilityChecks
{
    internal static void Run(Action<bool> yes)
    {
        DeviceState tracked() => new DeviceState().Set(CommonUsages.isTracked, true)
            .Set(CommonUsages.trackingState, InputTrackingState.Position | InputTrackingState.Rotation)
            .Set(CommonUsages.devicePosition, new Vector3(.2f, 1.1f, -.3f))
            .Set(CommonUsages.deviceRotation, Quaternion.identity);
        void check(bool available, bool left, bool right)
        {
            Time.frameCount++;
            yes(TrackedControllers.Available == available);
            yes(TrackedControllers.Read(true).Valid == left);
            yes(TrackedControllers.Read(false).Valid == right);
        }

        foreach (var frame in new[] { false, true })
        {
            XRController.leftHand = frame ? new SteamFrameControllerProfile.SteamFrameController()
                : new OculusTouchControllerProfile.OculusTouchController();
            XRController.rightHand = frame ? new SteamFrameControllerProfile.SteamFrameController()
                : new OculusTouchControllerProfile.OculusTouchController();
            var head = tracked().Set(CommonUsages.userPresence, false);
            var leftHand = tracked();
            InputDevices.Devices[XRNode.Head] = new InputDevice(head);
            InputDevices.Devices[XRNode.LeftHand] = new InputDevice(leftHand);
            InputDevices.Devices[XRNode.RightHand] = new InputDevice(tracked());
            XRSettings.enabled = true;
            OpenXRUtility.IsSessionFocused = true;

            // Reported WiVRn startup: presence stays false until a doff/don,
            // despite a focused session and fully tracked head/controllers.
            // No timeout or headset-removal cycle should be needed.
            check(true, true, true);
            for (var i = 0; i < 120; i++)
                check(true, true, true);
            head.Set(CommonUsages.userPresence, true);
            check(true, true, true);
            head.Set(CommonUsages.userPresence, false);
            check(true, true, true);
            head.Features.Remove(CommonUsages.userPresence.name);
            check(true, true, true);

            // Runtime focus and actual tracking still own input eligibility.
            foreach (var present in new[] { false, true })
            {
                head.Set(CommonUsages.userPresence, present);
                OpenXRUtility.IsSessionFocused = false;
                check(false, false, false);
                OpenXRUtility.IsSessionFocused = true;
                check(true, true, true);
                head.Set(CommonUsages.isTracked, false);
                check(false, false, false);
                head.Features.Remove(CommonUsages.isTracked.name);
                check(false, false, false);
                head.Set(CommonUsages.isTracked, true);
                InputDevices.Devices[XRNode.Head] = default;
                check(false, false, false);
                InputDevices.Devices[XRNode.Head] = new InputDevice(head);
                XRSettings.enabled = false;
                check(false, false, false);
                XRSettings.enabled = true;
                check(true, true, true);
            }

            leftHand.Set(CommonUsages.trackingState, InputTrackingState.Rotation);
            check(true, false, true);
            leftHand.Set(CommonUsages.trackingState, InputTrackingState.Position | InputTrackingState.Rotation);
            check(true, true, true);
        }
        InputDevices.Devices.Clear();
        XRController.leftHand = XRController.rightHand = null;
        Time.frameCount++;
    }
}
