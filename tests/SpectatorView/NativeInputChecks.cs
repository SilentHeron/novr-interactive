using System.Reflection;
using NOVR.MotionControls;
using NOVR.VrCamera;
using UnityEngine;
using UnityEngine.XR;

internal static class NativeInputChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
        var camera = new CameraStateManager();
        void Axis(float lx, float ly, float rx, float ry)
        {
            Time.frameCount++;
            TrackedControllers.Left.Axis = new(lx, ly);
            TrackedControllers.Right.Axis = new(rx, ry);
        }
        void Orbit(float lx = 0, float ly = 0, float rx = 0, float ry = 0)
        { Axis(lx, ly, rx, ry); SpectatorCameraInput.ApplyOrbit(camera.orbitState, camera); }
        void Fresh()
        {
            Time.frameCount += 2;
            camera = new();
            camera.currentState = camera.orbitState;
            CameraStateManager.cameraMode = CameraMode.orbit;
            GameManager.gameState = GameState.SinglePlayer;
            XRSettings.enabled = true;
            TrackedControllers.Available = true;
        }
        MethodInfo Hook(string name) => typeof(SpectatorCameraInput).GetNestedType(name, BindingFlags.NonPublic)!
            .GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)!;
        Fresh();
        Orbit(rx: 1);
        Check(camera.orbitState.Pan == 0, "Native orbit does not move from pre-existing flight stick deflection");
        Orbit(); Orbit(rx: 1, ry: 1, ly: -1);
        var pan = camera.orbitState.Pan;
        Check(pan > 0 && camera.orbitState.Tilt > 20 && camera.orbitState.Distance > 0, "Right-stick up raises the native orbit vantage and left-stick back increases distance");
        SpectatorCameraInput.ApplyOrbit(camera.orbitState, camera);
        Check(camera.orbitState.Pan == pan, "Multiple native camera callbacks consume input only once per frame");
        var blockCases = new (string Name, Action Block, Action Unblock)[]
        {
            ("XR disabled", () => XRSettings.enabled = false, () => XRSettings.enabled = true),
            ("outside gameplay", () => GameManager.gameState = GameState.Menu, () => GameManager.gameState = GameState.SinglePlayer),
            ("cockpit", () => CameraStateManager.cameraMode = CameraMode.cockpit, () => CameraStateManager.cameraMode = CameraMode.orbit),
            ("spawn selection", () => CameraStateManager.cameraMode = CameraMode.selection, () => CameraStateManager.cameraMode = CameraMode.orbit),
            ("relative camera", () => CameraStateManager.cameraMode = CameraMode.relative, () => CameraStateManager.cameraMode = CameraMode.orbit),
            ("encyclopedia", () => CameraStateManager.cameraMode = CameraMode.encyclopedia, () => CameraStateManager.cameraMode = CameraMode.orbit),
            ("no followed target", () => camera.followingUnit = null, () => camera.followingUnit = new()),
            ("tracking unavailable", () => TrackedControllers.Available = false, () => TrackedControllers.Available = true),
            ("maximized map", () => DynamicMap.mapMaximized = true, () => DynamicMap.mapMaximized = false),
            ("map cursor", () => CursorManager.Flags = CursorFlags.Map, () => CursorManager.Flags = CursorFlags.None),
            ("selection cursor", () => CursorManager.Flags = CursorFlags.SelectionMenu, () => CursorManager.Flags = CursorFlags.None),
            ("option wheel", () => NativeHandOptionWheel.IsOpen = true, () => NativeHandOptionWheel.IsOpen = false),
            ("left wheel hand", () => NativeHandOptionWheel.LeftOwned = true, () => NativeHandOptionWheel.LeftOwned = false),
            ("right wheel hand", () => NativeHandOptionWheel.RightOwned = true, () => NativeHandOptionWheel.RightOwned = false),
            ("VR menu", () => NOVR.VrUi.WristMenu.IsOpen = true, () => NOVR.VrUi.WristMenu.IsOpen = false),
            ("cockpit layout", () => CockpitLayout.Active = true, () => CockpitLayout.Active = false),
            ("HUD editing", () => NOVR.VrUi.SpecialBehavior.CockpitHudWidgets.Editing = true, () => NOVR.VrUi.SpecialBehavior.CockpitHudWidgets.Editing = false),
            ("game menu", () => CursorManager.Menu = true, () => CursorManager.Menu = false),
            ("control mapping", () => GameManager.controlMapper = new() { isOpen = true }, () => GameManager.controlMapper = null),
            ("text input", () => NuclearOption.MissionEditorScripts.InputFieldChecker.InsideInputField = true, () => NuclearOption.MissionEditorScripts.InputFieldChecker.InsideInputField = false),
            ("leaderboard", () => NuclearOption.UI.LeaderboardMenu.Open = true, () => NuclearOption.UI.LeaderboardMenu.Open = false),
            ("pause", () => Time.timeScale = 0, () => Time.timeScale = 1),
            ("native controls disabled", () => GameManager.flightControlsEnabled = false, () => GameManager.flightControlsEnabled = true),
            ("camera input disabled", () => camera.allowInputs = false, () => camera.allowInputs = true),
            ("native radial", () => { SceneSingleton<RadialMenuMain>.i = new(); RadialMenuMain.Open = true; }, () => { SceneSingleton<RadialMenuMain>.i = null; RadialMenuMain.Open = false; })
        };
        foreach (var gate in blockCases)
        {
            Fresh(); Orbit(); gate.Block(); Orbit(rx: 1); gate.Unblock(); Orbit(rx: 1);
            Check(camera.orbitState.Pan == 0, gate.Name + " blocks spectator movement and requires neutral on return");
            Orbit(); Orbit(rx: 1);
            Check(camera.orbitState.Pan > 0, gate.Name + " recovery accepts a fresh intentional deflection");
        }
        Fresh();
        var hiddenRadial = new RadialMenuMain { isActiveAndEnabled = false };
        SceneSingleton<RadialMenuMain>.i = hiddenRadial;
        RadialMenuMain.Open = true;
        Orbit(); Orbit(rx: 1);
        Check(camera.orbitState.Pan > 0 && RadialMenuMain.Open,
            "Spectator orbit remains usable when a hidden radial child retains its native open flag");
        pan = camera.orbitState.Pan;
        hiddenRadial.isActiveAndEnabled = true;
        Orbit(rx: 1);
        Check(camera.orbitState.Pan == pan, "An active native radial still owns input and stops spectator orbit");
        hiddenRadial.isActiveAndEnabled = false;
        Orbit(rx: 1);
        Check(camera.orbitState.Pan == pan, "Hiding an active radial requires neutral before orbit resumes");
        Orbit(); Orbit(rx: 1);
        Check(camera.orbitState.Pan > pan, "Returning the stick to neutral restores orbit despite the stale hidden open flag");
        SceneSingleton<RadialMenuMain>.i = null;
        RadialMenuMain.Open = false;
        Fresh(); Orbit();
        for (var i = 0; i < 2000; i++) Orbit(ly: -1, ry: -1);
        Check(camera.orbitState.Distance == 10 && camera.orbitState.Tilt == -89, "Native orbit accumulators retain safe zoom and pitch limits");
        for (var i = 0; i < 4000; i++) Orbit(ly: 1, ry: 1);
        Check(camera.orbitState.Distance == 0 && camera.orbitState.Tilt == 89, "Native orbit cannot zoom through the target or pitch over the pole");
        Fresh(); Orbit(); Orbit(rx: 1); pan = camera.orbitState.Pan;
        camera.followingUnit = new(); Orbit(rx: 1);
        Check(camera.orbitState.Pan == pan, "Changing followed target while a stick is held requires neutral");
        Orbit(); Orbit(rx: 1);
        Check(camera.orbitState.Pan > pan, "New target accepts input after recentering the stick");
        pan = camera.orbitState.Pan; camera.currentState = new CameraFreeState(); Orbit(rx: 1);
        Check(camera.orbitState.Pan == pan, "External state changes rearm even with the same camera and target");
        Fresh();
        var free = new CameraFreeState(); var freeHook = Hook("FreeInputPatch");
        CameraStateManager.cameraMode = CameraMode.free;
        camera.currentState = free;
        Axis(0, 0, 0, 0); freeHook.Invoke(null, new object[] { free, camera });
        Axis(1, 1, 1, 1); freeHook.Invoke(null, new object[] { free, camera });
        Check(free.Pan > 0 && free.Tilt < 0 && !free.Track, "Free camera uses native pan and tilt accumulators");
        Check(camera.cameraVelocity.x > 0 && camera.cameraVelocity.z > 0 && camera.cameraVelocity.y == 0,
            "Free movement feeds native velocity without changing the tracking origin");
        foreach (var name in new[] { "ChaseInputPatch", "TelevisionInputPatch" })
        {
            void FreshCinematic()
            {
                Fresh();
                CameraStateManager.cameraMode = name == "ChaseInputPatch" ? CameraMode.chase : CameraMode.tv;
                camera.currentState = name == "ChaseInputPatch" ? new CameraChaseState() : new CameraTVState();
            }
            FreshCinematic(); var hook = Hook(name);
            Axis(0, 0, 1, 0);
            Check((bool)hook.Invoke(null, new object[] { camera })! && camera.Switches == 0,
                "Cinematic camera keeps its native vantage while a pre-existing stick deflection is held");
            Axis(0, 0, 0, 0); hook.Invoke(null, new object[] { camera });
            Axis(0, 0, 1, 0);
            Check(!(bool)hook.Invoke(null, new object[] { camera })! && camera.currentState == camera.orbitState &&
                camera.Switches == 1 && camera.orbitState.Updates == 1 && camera.orbitState.Pan > 0,
                "Deliberate input converts cinematic camera to native orbit and skips the old motion");
            pan = camera.orbitState.Pan; Orbit(rx: 1);
            Check(camera.orbitState.Pan > pan, "Deliberate cinematic-to-orbit transition preserves the active input gate");
            FreshCinematic(); Axis(0, 0, 0, 0); hook.Invoke(null, new object[] { camera }); Axis(1, 0, 0, 0);
            Check((bool)hook.Invoke(null, new object[] { camera })! && camera.Switches == 0,
                "An unused lateral-distance axis does not cancel the cinematic camera");
            FreshCinematic(); Axis(0, 0, 0, 0); hook.Invoke(null, new object[] { camera }); camera.followingUnit = null;
            Axis(0, 0, 1, 0);
            Check((bool)hook.Invoke(null, new object[] { camera })! && camera.Switches == 0,
                "Missing follow target cannot enter invalid native orbit");
        }
        foreach (var state in new[] { GameState.SinglePlayer, GameState.Multiplayer })
        {
            Fresh(); GameManager.gameState = state; Orbit(); Orbit(rx: 1, ly: -1);
            Check(camera.orbitState.Pan > 0 && camera.orbitState.Distance > 0,
                "Stereo spectator input works in supported gameplay without any flat-presentation service: " + state);
        }
        foreach (var excluded in new[] { "selection camera", "map cursor", "selection cursor", "maximized map" })
        {
            Fresh();
            var blockedFree = new CameraFreeState();
            camera.currentState = blockedFree;
            CameraStateManager.cameraMode = CameraMode.free;
            // Nothing followed: the free camera a spawn map or selection page sits over.
            camera.followingUnit = null;
            Axis(0, 0, 0, 0); freeHook.Invoke(null, new object[] { blockedFree, camera });
            if (excluded == "selection camera") CameraStateManager.cameraMode = CameraMode.selection;
            if (excluded == "map cursor") CursorManager.Flags = CursorFlags.Map;
            if (excluded == "selection cursor") CursorManager.Flags = CursorFlags.SelectionMenu;
            if (excluded == "maximized map") DynamicMap.mapMaximized = true;
            Axis(1, 1, 1, 1);
            freeHook.Invoke(null, new object[] { blockedFree, camera });
            SpectatorCameraInput.ApplyOrbit(camera.orbitState, camera);
            var chaseNative = (bool)Hook("ChaseInputPatch").Invoke(null, new object[] { camera })!;
            var televisionNative = (bool)Hook("TelevisionInputPatch").Invoke(null, new object[] { camera })!;
            Check(blockedFree.Pan == 0 && blockedFree.Tilt == 0 && blockedFree.Track &&
                camera.cameraVelocity.x == 0 && camera.cameraVelocity.y == 0 && camera.cameraVelocity.z == 0 &&
                camera.orbitState.Pan == 0 && camera.orbitState.Tilt == 20 && camera.orbitState.Distance == 0 &&
                camera.Switches == 0 && chaseNative && televisionNative,
                excluded + " cannot consume sticks or change native camera state through any spectator hook");
            CursorManager.Flags = CursorFlags.None;
            DynamicMap.mapMaximized = false;
        }
        // A spectator who follows nothing (joined as spectator, or closed the
        // spawn map) roams with the game's free camera.
        Fresh();
        var roam = new CameraFreeState();
        camera.currentState = roam;
        camera.followingUnit = null;
        CameraStateManager.cameraMode = CameraMode.free;
        Axis(0, 0, 0, 0); freeHook.Invoke(null, new object[] { roam, camera });
        Axis(1, 1, 1, 1); freeHook.Invoke(null, new object[] { roam, camera });
        Check(roam.Pan > 0 && roam.Tilt < 0 && !roam.Track && camera.cameraVelocity.x > 0 && camera.cameraVelocity.z > 0,
            "Free camera with nothing followed looks with the right stick and moves with the left");
        var walk = camera.cameraVelocity.z;
        Fresh();
        roam = new CameraFreeState();
        camera.currentState = roam;
        camera.followingUnit = null;
        CameraStateManager.cameraMode = CameraMode.free;
        Axis(0, 0, 0, 0); freeHook.Invoke(null, new object[] { roam, camera });
        TrackedControllers.Left.Grip = 1;
        Axis(1, 1, 1, 1); freeHook.Invoke(null, new object[] { roam, camera });
        TrackedControllers.Left.Grip = 0;
        Check(Math.Abs(camera.cameraVelocity.z - walk * 10) < .001f, "Holding a grip moves the free camera ten times faster, as Shift does natively");
        Fresh();
        camera.followingUnit = null;
        Orbit(); Orbit(rx: 1);
        Check(camera.orbitState.Pan == 0, "An orbit camera with no target still takes no stick input");
        Check(Debug.Warnings == 0, "Verified native input contract produces no fallback warnings");
        return checks;
    }
}
