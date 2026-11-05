using NOVR.MotionControls;
using UnityEngine;

namespace NOVR.VrUi;

// The menu may outlive its aircraft, but an open panel must never inherit a new
// camera/target's coordinate frame or retain aircraft actions across a transition.
internal sealed class WristMenuGameplayContext
{
    internal readonly Camera Camera;
    internal readonly bool Cockpit;
    private readonly Transform? _root;
    private readonly Camera? _main;
    private readonly Aircraft? _aircraft;
    private readonly Unit? _unit;
    private readonly CameraMode _mode;

    internal static bool UsableCockpit => CameraStateManager.cameraMode == CameraMode.cockpit &&
        GameManager.GetLocalAircraft(out var aircraft) && !aircraft.disabled && !aircraft.HasEjected() &&
        aircraft.cockpit != null && !aircraft.cockpit.IsDetached();

    internal static bool Available(CursorFlags ownedCursorFlag) =>
        (GameManager.gameState == GameState.SinglePlayer || GameManager.gameState == GameState.Multiplayer) &&
        !GameplayUI.GameIsPaused && !NativeHandOptionWheel.IsOpen &&
        (CursorManager.GetFlags() & ~(CursorFlags.Map | CursorFlags.SelectionMenu | CursorFlags.CameraControlUI | ownedCursorFlag)) == CursorFlags.None &&
        !NativeRadialMenuState.IsBlockingInput &&
        (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen);

    internal WristMenuGameplayContext(Camera camera)
    {
        Camera = camera;
        Cockpit = UsableCockpit;
        _root = camera.transform.parent;
        _main = APIBus.MainCamera;
        _aircraft = GameManager.GetLocalAircraft(out var aircraft) ? aircraft : null;
        _unit = SceneSingleton<CameraStateManager>.i?.followingUnit;
        _mode = CameraStateManager.cameraMode;
    }

    internal bool IsCurrent()
    {
        var aircraft = GameManager.GetLocalAircraft(out var current) ? current : null;
        return Camera != null && APIBus.CockpitHudCamera == Camera && Camera.transform.parent == _root &&
            APIBus.MainCamera == _main && SceneSingleton<CameraStateManager>.i?.followingUnit == _unit &&
            CameraStateManager.cameraMode == _mode && aircraft == _aircraft && UsableCockpit == Cockpit;
    }
}
