using System;
using NOVR.VrCamera;
using NOVR.VrUi;

namespace NOVR.MotionControls;

internal enum NativeWheelMenuAction
{
    None, GameMenu, VrMenu, Map, Eject,
    // Camera choices: leave the cockpit for the outside view, return to it, or
    // let go of a spectated unit and roam with the free camera.
    OutsideView, CockpitView, FreeCamera
}

internal static class NativeWheelMenuActions
{
    // PauseGame opens the native game menu. In multiplayer the game decides
    // whether simulation continues; NOVR never writes Time.timeScale here.
    internal static bool Open(NativeWheelMenuAction action, Action releaseWheel)
    {
        releaseWheel();
        MotionFlightController.Instance?.ResetGrips();
        ControllerActions.Instance?.ResetButtons();
        if (GameManager.gameState != GameState.SinglePlayer && GameManager.gameState != GameState.Multiplayer || GameplayUI.GameIsPaused)
            return false;
        var flags = CursorManager.GetFlags();
        if ((flags & ~(CursorFlags.Map | CursorFlags.SelectionMenu | CursorFlags.CameraControlUI)) != CursorFlags.None)
            return false;
        if (action == NativeWheelMenuAction.VrMenu)
            return WristMenu.OpenFromHandWheel();
        // Each camera choice rechecks its own native state after the wheel is
        // released; a stale sector simply does nothing.
        if (action == NativeWheelMenuAction.OutsideView)
            return PilotView.ToExternal();
        if (action == NativeWheelMenuAction.CockpitView)
            return PilotView.ToCockpit();
        if (action == NativeWheelMenuAction.FreeCamera)
            return PilotView.ReleaseTarget();
        if (action == NativeWheelMenuAction.Map)
        {
            // The open wheel itself blocks ordinary map input. Recheck after
            // releasing it, retaining the native selection/menu restrictions.
            if (!ControllerActions.MapAccessAvailable)
                return false;
            var wasOpen = DynamicMap.mapMaximized;
            ControllerActions.ToggleMap();
            return DynamicMap.mapMaximized != wasOpen;
        }
        var ui = SceneSingleton<GameplayUI>.i;
        // Aircraft selection reserves Escape for returning to the map. This
        // explicit Game Menu selection can use PauseGame's native canvas handoff
        // without changing that keyboard guard or destroying the selection UI.
        var selectionMenu = (flags & CursorFlags.SelectionMenu) != 0;
        if (action != NativeWheelMenuAction.GameMenu || ui == null || !ui.isActiveAndEnabled ||
            !GameplayUI.AllowPauseKeybind && !selectionMenu)
            return false;
        ui.PauseGame();
        return true;
    }
}
