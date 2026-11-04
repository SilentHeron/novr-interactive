using System;
using NOVR.VrUi;

namespace NOVR.MotionControls;

internal enum NativeWheelMenuAction
{
    None, GameMenu, VrMenu
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
        if (action == NativeWheelMenuAction.VrMenu)
            return WristMenu.OpenFromHandWheel();
        var ui = SceneSingleton<GameplayUI>.i;
        if (action != NativeWheelMenuAction.GameMenu || ui == null || !ui.isActiveAndEnabled || !GameplayUI.AllowPauseKeybind)
            return false;
        ui.PauseGame();
        return true;
    }
}
