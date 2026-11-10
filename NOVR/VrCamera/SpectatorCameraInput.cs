using System;
using System.Reflection;
using HarmonyLib;
using NuclearOption.MissionEditorScripts;
using NuclearOption.UI;
using NOVR.MotionControls;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR.VrCamera;

// Native camera motion remains responsible for following, ground collision and
// smoothing. Only its input accumulators are changed; the tracked child keeps
// normal stereo/head tracking at the native camera's external stand-off.
internal static class SpectatorCameraInput
{
    private static readonly FieldInfo? OrbitPan = AccessTools.Field(typeof(CameraOrbitState), "panView");
    private static readonly FieldInfo? OrbitTilt = AccessTools.Field(typeof(CameraOrbitState), "tiltView");
    private static readonly FieldInfo? OrbitDistance = AccessTools.Field(typeof(CameraOrbitState), "viewDistAdjust");
    private static readonly FieldInfo? FreePan = AccessTools.Field(typeof(CameraFreeState), "panView");
    private static readonly FieldInfo? FreeTilt = AccessTools.Field(typeof(CameraFreeState), "tiltView");
    private static readonly FieldInfo? FreeTrack = AccessTools.Field(typeof(CameraFreeState), "trackTarget");
    private static readonly SpectatorStickInput Input = new();
    private static CameraStateManager? _camera;
    private static CameraBaseState? _state;
    private static Unit? _target;
    private static int _sampleFrame = -1, _consumedFrame = -1;
    private static SpectatorStickMotion _motion;
    private static bool _warned, _piloted, _boost;
    // The game's free camera moves ten times faster with Shift held.
    private const float FreeAcceleration = 120f, FreeBoost = 10f, GripHeld = .55f;

    private static SpectatorStickMotion Sample(CameraStateManager cam)
    {
        // Flying the local aircraft from outside: its thumbsticks belong to
        // ExternalFlightInput, which hands over only the camera part.
        var piloted = ExternalFlightInput.Sample();
        var changed = _camera != cam || _state != cam.currentState || _target != cam.followingUnit || _piloted != piloted;
        if (_sampleFrame == Time.frameCount && !changed)
            return _motion;
        if (changed || _sampleFrame != Time.frameCount - 1)
            Input.Reset();
        _camera = cam;
        _state = cam.currentState;
        _target = cam.followingUnit;
        _piloted = piloted;
        _sampleFrame = Time.frameCount;
        if (piloted)
        {
            _boost = false;
            return _motion = ExternalFlightInput.TakeCamera(cam, out var camera) ? camera : default;
        }
        var spectatorMode = CameraStateManager.cameraMode == CameraMode.orbit ||
            CameraStateManager.cameraMode == CameraMode.free || CameraStateManager.cameraMode == CameraMode.chase ||
            CameraStateManager.cameraMode == CameraMode.tv;
        // Following a unit, or roaming with the game's free camera when nothing
        // is followed (a spectator who has not picked a unit, or a player who
        // closed the spawn map). An aircraft preview is neither, and the map and
        // selection pages below keep their own controls.
        var following = cam.followingUnit != null || CameraStateManager.cameraMode == CameraMode.free;
        var available = XRSettings.enabled && spectatorMode && following &&
            (GameManager.gameState == GameState.SinglePlayer || GameManager.gameState == GameState.Multiplayer) &&
            (CursorManager.GetFlags() & (CursorFlags.Map | CursorFlags.SelectionMenu)) == CursorFlags.None &&
            TrackedControllers.Available &&
            Time.timeScale > 0 && GameManager.flightControlsEnabled && cam.allowInputs &&
            !DynamicMap.mapMaximized && !WristMenu.IsOpen && !NativeHandOptionWheel.IsOpen &&
            !NativeHandOptionWheel.OwnsHand(true) && !NativeHandOptionWheel.OwnsHand(false) &&
            !CockpitLayout.Active && !CockpitHudWidgets.Editing && !InputFieldChecker.InsideInputField &&
            !LeaderboardMenu.IsOpen() && !CursorManager.GetFlag(CursorFlags.GameMenu) &&
            (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen) &&
            !NativeRadialMenuState.IsBlockingInput;
        var left = available ? TrackedControllers.Read(true) : default;
        var right = available ? TrackedControllers.Read(false) : default;
        _boost = left.Valid && left.Grip >= GripHeld || right.Valid && right.Grip >= GripHeld;
        return _motion = Input.Update(available, left.Valid && left.HasAxis, right.Valid && right.HasAxis,
            left.Axis.x, left.Axis.y, right.Axis.x, right.Axis.y, Time.unscaledDeltaTime);
    }

    internal static void ApplyOrbit(CameraOrbitState state, CameraStateManager cam)
    {
        var motion = Sample(cam);
        if (!motion.HasOrbitMotion || _consumedFrame == Time.frameCount)
            return;
        _consumedFrame = Time.frameCount;
        try
        {
            if (OrbitPan == null || OrbitTilt == null || OrbitDistance == null)
                throw new MissingFieldException("Native orbit input fields unavailable.");
            OrbitPan.SetValue(state, Mathf.DeltaAngle(0, ReadFloat(OrbitPan, state) + motion.YawDegrees));
            // Native orbit pitch raises the camera around its target; free
            // camera pitch instead rotates the viewing direction upward.
            OrbitTilt.SetValue(state, Mathf.Clamp(ReadFloat(OrbitTilt, state) - motion.PitchDegrees, -89, 89));
            OrbitDistance.SetValue(state, Mathf.Clamp(ReadFloat(OrbitDistance, state) + motion.Distance, 0, 10));
        }
        catch (Exception error) { Warn(error); }
    }

    private static void ApplyFree(CameraFreeState state, CameraStateManager cam)
    {
        var motion = Sample(cam);
        if (!motion.HasMotion || _consumedFrame == Time.frameCount)
            return;
        _consumedFrame = Time.frameCount;
        try
        {
            if (FreePan == null || FreeTilt == null || FreeTrack == null)
                throw new MissingFieldException("Native free-camera input fields unavailable.");
            FreePan.SetValue(state, Mathf.DeltaAngle(0, ReadFloat(FreePan, state) + motion.YawDegrees));
            FreeTilt.SetValue(state, Mathf.Clamp(ReadFloat(FreeTilt, state) + motion.PitchDegrees, -89, 89));
            FreeTrack.SetValue(state, false);
            // Feed the existing velocity integrator, retaining native damping
            // and ground clearance, without synthesizing flight-control axes.
            cam.cameraVelocity += (cam.transform.right * motion.PanX + cam.transform.forward * motion.PanForward) *
                (_boost ? FreeAcceleration * FreeBoost : FreeAcceleration);
        }
        catch (Exception error) { Warn(error); }
    }

    private static bool EnterManualOrbit(CameraStateManager cam)
    {
        var motion = Sample(cam);
        if (!motion.HasOrbitMotion || cam.followingUnit == null || cam.followingRB == null)
            return false;
        cam.SwitchState(cam.orbitState);
        // This is the deliberate transition driven by this sample, so carry
        // its neutral gate into orbit. Other target/state changes must rearm.
        _state = cam.currentState;
        _target = cam.followingUnit;
        cam.orbitState.UpdateState(cam);
        return true;
    }

    private static float ReadFloat(FieldInfo field, object instance) =>
        field.GetValue(instance) is float value && !float.IsNaN(value) && !float.IsInfinity(value)
            ? value : throw new InvalidOperationException("Invalid native camera input accumulator.");

    private static void Warn(Exception error)
    {
        Input.Reset();
        if (_warned)
            return;
        _warned = true;
        Debug.LogWarning("[NOVR] Spectator stick input unavailable; retaining native camera controls. " + error.Message);
    }

    [HarmonyPatch(typeof(CameraFreeState), nameof(CameraFreeState.UpdateState))]
    private static class FreeInputPatch
    {
        [HarmonyPrefix]
        private static void Prefix(CameraFreeState __instance, CameraStateManager cam) => ApplyFree(__instance, cam);
    }

    [HarmonyPatch(typeof(CameraChaseState), nameof(CameraChaseState.UpdateState))]
    private static class ChaseInputPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(CameraStateManager cam) => !EnterManualOrbit(cam);
    }

    [HarmonyPatch(typeof(CameraTVState), nameof(CameraTVState.UpdateState))]
    private static class TelevisionInputPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(CameraStateManager cam) => !EnterManualOrbit(cam);
    }
}
