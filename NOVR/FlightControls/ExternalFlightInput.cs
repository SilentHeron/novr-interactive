using NuclearOption.MissionEditorScripts;
using NuclearOption.UI;
using NOVR.VrCamera;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

namespace NOVR.MotionControls;

// Runtime side of ExternalFlightSticks: one sample per frame, shared by the
// native pilot input hooks, the orbit camera hook and the button dispatcher.
// The virtual stick and throttle only exist in the cockpit, so the outside view
// flies with thumbsticks instead. It needs motion controls enabled; with them
// off (a physical HOTAS) the sticks keep their spectator camera roles.
internal static class ExternalFlightInput
{
    private const float GripHeld = .55f;
    private static readonly ExternalFlightSticks Sticks = new();
    private static int _frame = -1, _cameraFrame = -1;
    private static Aircraft? _aircraft;
    private static ExternalFlightMotion _motion;
    private static float _throttleStep;
    private static bool _announced;
    private static int _hints;
    private const int MaxHints = 3;

    // True while the local pilot flies from the outside view this frame.
    internal static bool Active
    {
        get; private set;
    }
    internal static bool Flying => Active && _motion.Flying;
    internal static float Pitch => _motion.Pitch;
    internal static float Roll => _motion.Roll;
    internal static bool Rudder => Active && _motion.Rudder;
    internal static float Yaw => _motion.Yaw;

    internal static bool Sample()
    {
        if (_frame == Time.frameCount)
            return Active;
        var continuous = _frame == Time.frameCount - 1;
        _frame = Time.frameCount;
        var active = Available(out var aircraft);
        if (!active || !continuous || aircraft != _aircraft)
        {
            Sticks.Reset();
            _throttleStep = 0;
        }
        _aircraft = active ? aircraft : null;
        Active = active;
        // One announcement per visit to the outside view, not per menu or map.
        if (!PilotView.IsExternalMode(CameraStateManager.cameraMode))
            _announced = false;
        if (!active)
        {
            _motion = default;
            return false;
        }
        var leftHanded = ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        if (!_announced)
        {
            _announced = true;
            Announce(leftHanded);
        }
        var stick = TrackedControllers.Read(leftHanded);
        var other = TrackedControllers.Read(!leftHanded);
        // A hand that owns the quick wheel is choosing a sector, not flying.
        var stickValid = stick.Valid && stick.HasAxis && !NativeHandOptionWheel.OwnsHand(leftHanded);
        var otherValid = other.Valid && other.HasAxis && !NativeHandOptionWheel.OwnsHand(!leftHanded);
        _motion = Sticks.Update(true, stickValid, otherValid, stick.Axis.x, stick.Axis.y, other.Axis.x, other.Axis.y,
            other.Valid && other.Grip >= GripHeld, stick.Valid && stick.Grip >= GripHeld, Time.unscaledDeltaTime);
        // The pilot hooks may run in physics steps: none or several per frame.
        _throttleStep += _motion.ThrottleStep;
        return true;
    }

    internal static bool SampleFor(Pilot pilot) => Sample() && pilot != null && !pilot.dead &&
        pilot.aircraft == _aircraft && pilot.currentState == pilot.playerState;

    internal static bool TakeThrottleStep(out float step)
    {
        step = _throttleStep;
        _throttleStep = 0;
        return step != 0;
    }

    // Camera motion for this frame, handed out once so several native camera
    // callbacks in one frame cannot apply it twice.
    internal static bool TakeCamera(CameraStateManager camera, out SpectatorStickMotion motion)
    {
        motion = default;
        if (!Sample() || _cameraFrame == Time.frameCount || camera == null || !camera.allowInputs)
            return false;
        _cameraFrame = Time.frameCount;
        motion = _motion.Camera;
        return motion.HasOrbitMotion;
    }

    // The game's own message feed, which NOVR already shows in outside views.
    // Feed rows are one line each, so every control gets a short line.
    internal static string[] HintLines(bool leftHanded, string cockpitButton)
    {
        var stick = leftHanded ? "Left" : "Right";
        var other = leftHanded ? "Right" : "Left";
        return new[]
        {
            $"{stick} stick flies, {other.ToLowerInvariant()} stick looks",
            $"{other} grip + stick: throttle, rudder",
            $"{stick} grip + {other.ToLowerInvariant()} stick: zoom. {cockpitButton}: cockpit"
        };
    }

    private static void Announce(bool leftHanded)
    {
        var ui = SceneSingleton<GameplayUI>.i;
        if (_hints >= MaxHints || ui == null)
            return;
        _hints++;
        var button = ControllerButtonNames.Describe(ControllerActions.CockpitReturnButton, leftHanded, TrackedControllers.NativeFrame(leftHanded));
        foreach (var line in HintLines(leftHanded, button))
            ui.GameMessage(line);
    }

    private static bool Available(out Aircraft aircraft)
    {
        aircraft = null!;
        return ModConfiguration.Instance.MotionControls.Enabled.Value && TrackedControllers.Available &&
            PilotView.IsExternalMode(CameraStateManager.cameraMode) && PilotView.TryGetPiloted(out _, out aircraft) &&
            Time.timeScale > 0 && GameManager.flightControlsEnabled && !DynamicMap.mapMaximized &&
            CursorManager.GetFlags() == CursorFlags.None && !WristMenu.IsOpen && !CockpitLayout.Active &&
            !CockpitHudWidgets.Editing && !InputFieldChecker.InsideInputField && !LeaderboardMenu.IsOpen() &&
            (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen) &&
            !NativeRadialMenuState.IsBlockingInput;
    }
}
