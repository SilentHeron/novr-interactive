using UnityEngine;
using UnityEngine.XR;

namespace NOVR.VrCamera;

// Cockpit <-> outside view for the aircraft the local player is flying, and the
// spectator's way back to a free camera. Both use the game's own camera states:
// SwitchState runs the native LeaveState/EnterState pair, which already hands
// back the cockpit renderers, pilot visibility, HUD canvas and shake handler.
internal static class PilotView
{
    private static bool Playing => GameManager.gameState == GameState.SinglePlayer || GameManager.gameState == GameState.Multiplayer;

    internal static bool IsExternalMode(CameraMode mode) =>
        mode == CameraMode.orbit || mode == CameraMode.chase || mode == CameraMode.tv;

    // A live local pilot in a usable aircraft that the camera is following.
    internal static bool TryGetPiloted(out CameraStateManager camera, out Aircraft aircraft)
    {
        camera = SceneSingleton<CameraStateManager>.i!;
        aircraft = null!;
        if (!Playing || camera == null || !GameManager.GetLocalAircraft(out aircraft) || aircraft == null)
            return false;
        if (aircraft.disabled || aircraft.HasEjected() || aircraft.cockpit == null || aircraft.cockpit.IsDetached() ||
            camera.followingUnit != aircraft || aircraft.pilots == null)
            return false;
        foreach (var pilot in aircraft.pilots)
            if (pilot != null && !pilot.dead && pilot.currentState == pilot.playerState)
                return true;
        return false;
    }

    // Flying the local aircraft while looking at it from outside.
    internal static bool External => IsExternalMode(CameraStateManager.cameraMode) && TryGetPiloted(out _, out _);

    private static bool Unobstructed => XRSettings.enabled && Time.timeScale > 0 && GameManager.flightControlsEnabled &&
        !DynamicMap.mapMaximized && CursorManager.GetFlags() == CursorFlags.None &&
        (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen);

    internal static bool CanGoExternal => CameraStateManager.cameraMode == CameraMode.cockpit && Unobstructed && TryGetPiloted(out _, out _);

    // The native flyby camera returns to the cockpit under the same condition.
    internal static bool CanGoCockpit => IsExternalMode(CameraStateManager.cameraMode) && Unobstructed &&
        TryGetPiloted(out _, out var aircraft) && aircraft.cockpitViewPoint != null;

    internal static bool ToExternal()
    {
        if (!CanGoExternal || !TryGetPiloted(out var camera, out _))
            return false;
        camera.SwitchState(camera.orbitState);
        return CameraStateManager.cameraMode == CameraMode.orbit;
    }

    internal static bool ToCockpit()
    {
        if (!CanGoCockpit || !TryGetPiloted(out var camera, out _))
            return false;
        camera.SwitchState(camera.cockpitState);
        return CameraStateManager.cameraMode == CameraMode.cockpit;
    }

    internal static bool Toggle() => CameraStateManager.cameraMode == CameraMode.cockpit ? ToExternal() : ToCockpit();

    // Spectating a unit that is not the player's own live aircraft. Releasing it
    // is the native orbit camera's own step into the free camera.
    internal static bool CanReleaseTarget
    {
        get
        {
            var camera = SceneSingleton<CameraStateManager>.i;
            return XRSettings.enabled && Playing && camera != null && camera.followingUnit != null &&
                IsExternalMode(CameraStateManager.cameraMode) && !DynamicMap.mapMaximized && !TryGetPiloted(out _, out _);
        }
    }

    internal static bool ReleaseTarget()
    {
        if (!CanReleaseTarget)
            return false;
        var camera = SceneSingleton<CameraStateManager>.i!;
        camera.SetFollowingUnit(null);
        return camera.followingUnit == null;
    }
}
