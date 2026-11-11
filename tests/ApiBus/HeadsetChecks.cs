using System.Reflection;
using NOVR;
using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.XR;

internal static class HeadsetChecks
{
    internal static void Run()
    {
        var count = 0;
        void Check(bool value, string message)
        {
            count++;
            if (!value)
                throw new InvalidOperationException(message);
        }
        void Near(Vector3 actual, Vector3 expected, string message) => Check((actual - expected).magnitude < .00001f, message);
        void Rotation(Quaternion actual, Quaternion expected, string message)
        {
            Near(actual * new Vector3(1, 0, 0), expected * new Vector3(1, 0, 0), message + " (right)");
            Near(actual * new Vector3(0, 0, 1), expected * new Vector3(0, 0, 1), message + " (forward)");
        }
        static Action Call(object value, string method) => (Action)value.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.CreateDelegate(typeof(Action), value);
        var config = ModConfiguration.Instance.Config;
        var originalRender = Application.Subscribers;
        var originalConfig = config.Subscribers;
        var a = new ObservedBehaviour();
        var b = new ObservedBehaviour();
        a.Enable();
        b.Enable();
        Check(Application.Subscribers == originalRender + 2 && config.Subscribers == originalConfig + 2, "Each owner registers its render/config callbacks");
        Application.Render();
        config.Raise();
        Check(a.Renders == 1 && b.Renders == 1 && a.Changes == 1 && b.Changes == 1, "Enabled callbacks dispatch once");
        a.Disable();
        Application.Render();
        config.Raise();
        Check(a.Renders == 1 && a.Changes == 1 && b.Renders == 2 && b.Changes == 2, "Disable removes only that owner's callbacks");
        a.Enable();
        Application.Render();
        config.Raise();
        Check(a.Renders == 2 && a.Changes == 2 && b.Renders == 3 && b.Changes == 3, "Re-enable resumes without duplicated callbacks");
        a.Disable();
        b.Disable();
        Check(Application.Subscribers == originalRender && config.Subscribers == originalConfig, "Final owner teardown balances event subscriptions");

        InputTracking.Position = new(1, 2, 3);
        InputTracking.Rotation = Quaternion.Euler(0, 30, 0);
        NOVRHeadsetData.CalibrateTranslation();
        NOVRHeadsetData.CalibrateRotation();
        Check(InputTracking.PositionReads == 0 && InputTracking.RotationReads == 0, "Calibration before headset initialization remains a no-op");
        var parent = new GameObject("tracking owner").transform;
        var headset = NOVRBehaviour.Create<NOVRHeadsetData>(parent);
        Check(headset.transform.parent == parent && headset.GetComponent<Camera>() == null, "Headset data lives on its original dedicated camera-less child");
        Call(headset, "Awake")();
        Call(headset, "OnEnable")();
        var update = Call(headset, "Update");
        var late = Call(headset, "LateUpdate");
        var driver = new GameObject("tracked camera").AddComponent<NOVRPoseDriver>();
        Call(driver, "OnEnable")();
        var driveUpdate = Call(driver, "Update");
        var driveLate = Call(driver, "LateUpdate");
        CockpitProfiles.SeatOffset = new(.1f, -.2f, .3f);
        NOVRHeadsetData.SetAnchor(new(4, 5, 6));
        foreach (var phase in new[] { 0, 1, 2 })
        {
            InputTracking.Position = new(phase + .25f, phase + .5f, phase + .75f);
            InputTracking.Rotation = Quaternion.Euler(0, phase * 15, 0);
            var p = InputTracking.PositionReads;
            var r = InputTracking.RotationReads;
            if (phase == 0)
            {
                update();
                driveUpdate();
            }
            else if (phase == 1)
            {
                late();
                driveLate();
            }
            else
                Application.Render();
            Check(InputTracking.PositionReads == p + 1 && InputTracking.RotationReads == r + 1, "Each original update phase samples one fresh position and rotation");
            Check(InputTracking.PositionNode == XRNode.CenterEye && InputTracking.RotationNode == XRNode.CenterEye, "Both exact APIs receive CenterEye");
            Near(NOVRHeadsetData.Translation, NOVRHeadsetData.TranslationAnchor + InputTracking.Position, "Uncalibrated position preserves anchor");
            Near(driver.transform.localPosition, NOVRHeadsetData.Translation + CockpitProfiles.SeatOffset, "Pose driver preserves seat offset in every phase");
            Rotation(driver.transform.localRotation, InputTracking.Rotation, "Pose driver consumes fresh headset orientation");
        }
        InputTracking.Position = new(1, 2, 3);
        InputTracking.Rotation = Quaternion.Euler(0, 35, 0);
        NOVRHeadsetData.CalibrateTranslation();
        NOVRHeadsetData.CalibrateRotation();
        update();
        Near(NOVRHeadsetData.Translation, NOVRHeadsetData.TranslationAnchor, "Default full translation calibration returns to the anchor");
        Rotation(NOVRHeadsetData.Rotation, Quaternion.identity, "Default yaw calibration cancels current heading");
        InputTracking.Position = new(1.4f, 1.7f, 3.2f);
        update();
        Near(NOVRHeadsetData.Translation, NOVRHeadsetData.TranslationAnchor + Quaternion.Euler(0, -35, 0) * new Vector3(.4f, -.3f, .2f), "Recenter rotates positional movement using the unchanged rigid transform");
        InputTracking.Position = new(7, 8, 9);
        NOVRHeadsetData.CalibrateTranslation(CalibrationAxes.X, true);
        Near(NOVRHeadsetData.TranslationCalibrationOffset, new(-7, -2, -3), "Partial calibration retains untouched offsets when requested");
        NOVRHeadsetData.CalibrateTranslation(CalibrationAxes.Y);
        Near(NOVRHeadsetData.TranslationCalibrationOffset, new(0, -8, 0), "Partial calibration retains original default clearing behavior");
        InputTracking.Rotation = Quaternion.Euler(20, 10, 5);
        NOVRHeadsetData.CalibrateRotation(CalibrationAxes.Pitch, true);
        Rotation(NOVRHeadsetData.RotationCalibrationOffset, Quaternion.Euler(-20, -35, 0), "Partial rotation retains existing yaw when requested");
        NOVRHeadsetData.CalibrateRotation();
        Rotation(NOVRHeadsetData.RotationCalibrationOffset, Quaternion.Euler(0, -10, 0), "Default rotation calibration clears pitch and roll as before");
        // Snap turn at aircraft selection: the view turns, the head stays where it is.
        InputTracking.Position = new(.3f, 1.6f, -.2f);
        InputTracking.Rotation = Quaternion.Euler(0, 25, 0);
        update();
        var headBefore = NOVRHeadsetData.Translation;
        var viewBefore = NOVRHeadsetData.Rotation;
        var anchorBefore = NOVRHeadsetData.TranslationAnchor;
        var offsetBefore = NOVRHeadsetData.RotationCalibrationOffset;
        NOVRHeadsetData.TurnAbout(headBefore, 45);
        update();
        Check((NOVRHeadsetData.Translation - headBefore).magnitude < .0001f, "A snap turn leaves the head where it was");
        Rotation(NOVRHeadsetData.Rotation, Quaternion.Euler(0, 45, 0) * viewBefore, "A snap turn turns the view by the asked angle");
        Check((NOVRHeadsetData.TranslationAnchor - anchorBefore).magnitude > .1f, "The anchor swings around the head to keep it in place");
        InputTracking.Position = new(.3f, 1.6f, .8f);
        update();
        Check((NOVRHeadsetData.Translation - headBefore - Quaternion.Euler(0, 45, 0) * offsetBefore * new Vector3(0, 0, 1)).magnitude < .0001f,
            "A step in the room after the turn follows the turned heading");
        InputTracking.Position = new(.3f, 1.6f, -.2f);
        update();
        NOVRHeadsetData.TurnAbout(NOVRHeadsetData.Translation, -45);
        update();
        Check((NOVRHeadsetData.TranslationAnchor - anchorBefore).magnitude < .0001f && (NOVRHeadsetData.Translation - headBefore).magnitude < .0001f,
            "Turning back from the same spot restores the anchor");
        Rotation(NOVRHeadsetData.RotationCalibrationOffset, offsetBefore, "Turning back restores the heading");
        NOVRHeadsetData.TurnAbout(new(9, 0, -4), 90);
        NOVRHeadsetData.RestoreRotation(offsetBefore);
        NOVRHeadsetData.SetAnchor(anchorBefore);
        update();
        Check((NOVRHeadsetData.Translation - headBefore).magnitude < .0001f, "Leaving the screen can put heading and anchor back exactly");
        for (var i = 0; i < 1000; i++)
        {
            update();
            late();
            Application.Render();
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++)
        {
            update();
            late();
            Application.Render();
        }
        Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Typed center-eye sampling allocates no managed objects across all three phases");
        Call(headset, "OnDisable")();
        var reads = InputTracking.PositionReads;
        Application.Render();
        Check(InputTracking.PositionReads == reads, "Disabling headset removes before-render sampling");
        Call(driver, "OnDisable")();
        Check(Application.Subscribers == originalRender && config.Subscribers == originalConfig, "Headset and pose-driver teardown release real lifecycle subscriptions");
        Console.WriteLine($"{count} source-linked headset sampling and behaviour lifecycle checks passed.");
    }

    private sealed class ObservedBehaviour : NOVRBehaviour
    {
        internal int Renders, Changes;
        internal void Enable() => OnEnable();
        internal void Disable() => OnDisable();
        protected override void OnBeforeRender() => Renders++;
        protected override void OnSettingChanged() => Changes++;
    }
}
