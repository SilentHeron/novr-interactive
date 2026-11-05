using NOVR;
using NOVR.MotionControls;
using NOVR.VrUi;
using UnityEngine;

internal static class WristContextChecks
{
    internal static void Run(Action<bool, string> check)
    {
        NativeHandOptionWheel.Cancel();
        const CursorFlags own = (CursorFlags)(1 << 28);
        GameManager.gameState = GameState.SinglePlayer;
        GameplayUI.GameIsPaused = false;
        GameManager.controlMapper = null;
        SceneSingleton<RadialMenuMain>.i = null;
        GameManager.Aircraft = null;
        CameraStateManager.cameraMode = CameraMode.free;
        var camera = new GameObject("HUD").AddComponent<Camera>();
        var root = new GameObject("Stable tracking root").transform;
        camera.transform.SetParent(root, false);
        APIBus.CockpitHudCamera = camera;
        APIBus.MainCamera = new GameObject("Presentation camera").AddComponent<Camera>();
        SceneSingleton<CameraStateManager>.i = new() { followingUnit = new() };
        foreach (var flags in new[] { CursorFlags.None, own, CursorFlags.Map, CursorFlags.SelectionMenu | own, CursorFlags.CameraControlUI })
        {
            CursorManager.Flags = flags;
            check(WristMenuGameplayContext.Available(own), "VR Menu is available without an aircraft in supported gameplay cursor context: " + flags);
        }
        foreach (var flags in new[] { CursorFlags.GameMenu, CursorFlags.Chat, CursorFlags.Dialogue, CursorFlags.Loading, CursorFlags.NotInGame })
        {
            CursorManager.Flags = flags | own;
            check(!WristMenuGameplayContext.Available(own), "VR Menu rejects conflicting UI even with its own cursor bit: " + flags);
        }
        CursorManager.Flags = own;
        var outside = new WristMenuGameplayContext(camera);
        check(!outside.Cockpit && outside.IsCurrent(), "An aircraft-free spectator menu has a valid captured session.");
        camera.transform.position = new Vector3(1, 2, 3);
        root.position = new Vector3(4, 5, 6);
        check(outside.IsCurrent(), "Ordinary head/root movement does not invalidate the menu's stationary tracking-frame anchor.");
        foreach (var change in new[] { "HUD", "main", "root", "mode", "target", "spawn" })
        {
            APIBus.CockpitHudCamera = camera;
            camera.transform.SetParent(root, false);
            GameManager.Aircraft = null;
            CameraStateManager.cameraMode = CameraMode.free;
            var context = new WristMenuGameplayContext(camera);
            if (change == "HUD") APIBus.CockpitHudCamera = new GameObject("Replacement HUD").AddComponent<Camera>();
            if (change == "main") APIBus.MainCamera = new GameObject("Replacement main").AddComponent<Camera>();
            if (change == "root") camera.transform.SetParent(new GameObject("Replacement root").transform, false);
            if (change == "mode") CameraStateManager.cameraMode = CameraMode.orbit;
            if (change == "target") SceneSingleton<CameraStateManager>.i!.followingUnit = new();
            if (change == "spawn") GameManager.Aircraft = new();
            check(!context.IsCurrent(), "Menu context invalidates safely on " + change + " transition.");
        }
        APIBus.CockpitHudCamera = camera;
        CameraStateManager.cameraMode = CameraMode.cockpit;
        foreach (var change in new[] { "death", "eject", "detach", "missing cockpit", "replacement" })
        {
            GameManager.Aircraft = new();
            var context = new WristMenuGameplayContext(camera);
            check(context.Cockpit && context.IsCurrent(), "Live cockpit retains aircraft-specific menu access.");
            if (change == "death") GameManager.Aircraft.disabled = true;
            if (change == "eject") GameManager.Aircraft.Ejected = true;
            if (change == "detach") GameManager.Aircraft.cockpit!.Detached = true;
            if (change == "missing cockpit") GameManager.Aircraft.cockpit = null;
            if (change == "replacement") GameManager.Aircraft = new();
            check(!context.IsCurrent(), "Aircraft actions cannot carry through " + change + " transition.");
        }
        var destroyed = new WristMenuGameplayContext(camera);
        UnityEngine.Object.Destroy(camera.gameObject);
        check(!destroyed.IsCurrent(), "Destroyed HUD camera is rejected using Unity object lifetime semantics.");
    }
}
