using UnityEngine;
using NOVR.VrUi;

namespace NOVR.MotionControls;

[DefaultExecutionOrder(-1175)]
internal sealed class HelmetNightVision : MonoBehaviour
{
    private static HelmetNightVision? _instance;
    private readonly ControllerButtonGate[] _triggers = { new(), new() };
    private readonly bool[] _near = new bool[2];
    private float _nextToggle;
    internal static bool OwnsHand(bool left) => _instance != null && _instance._near[left ? 0 : 1];
    private void Awake() => _instance = this;
    private void Update()
    {
        var config = ModConfiguration.Instance;
        if (!config.MotionControls.HelmetNightVision.Value || !TrackedControllers.Available || NightVision.i == null ||
            CameraStateManager.cameraMode != CameraMode.cockpit || Time.timeScale <= 0 || !GameManager.flightControlsEnabled ||
            WristMenu.IsOpen || CockpitLayout.Active || DynamicMap.mapMaximized || CursorManager.GetFlags() != CursorFlags.None ||
            !GameManager.GetLocalAircraft(out var aircraft) || aircraft.cockpit == null || aircraft.cockpit.IsDetached() ||
            (SceneSingleton<RadialMenuMain>.i != null && RadialMenuMain.IsInUse()))
        { Reset(); return; }
        var head = NOVRHeadsetData.Translation + CockpitProfiles.SeatOffset;
        var inverse = Quaternion.Inverse(NOVRHeadsetData.Rotation);
        var controls = MotionFlightController.Instance;
        for (var i = 0; i < 2; i++)
        {
            var left = i == 0; var hand = TrackedControllers.Read(left);
            var held = left == config.MotionControls.LeftHanded.Value ? controls?.StickHeld == true : controls?.ThrottleHeld == true;
            var relative = inverse * (hand.Position - head);
            var near = hand.Valid && !held && HelmetGestureMath.AtBrow(relative.x, relative.y, relative.z);
            if (near && !_near[i]) InteractionHaptics.Pulse(hand.Device, .12f, .02f, InteractionCue.Grab);
            _near[i] = near;
            _triggers[i].Update(near, hand.Trigger);
            if (!_triggers[i].Pressed || Time.unscaledTime < _nextToggle) continue;
            NightVision.Toggle(); _nextToggle = Time.unscaledTime + .35f;
            InteractionHaptics.Pulse(hand.Device, .5f, .065f, InteractionCue.Helmet);
        }
    }
    private void Reset()
    { for (var i = 0; i < 2; i++) { _near[i] = false; _triggers[i].Reset(); } }
    private void OnDisable() => Reset();
    private void OnDestroy() { if (_instance == this) _instance = null; }
}
