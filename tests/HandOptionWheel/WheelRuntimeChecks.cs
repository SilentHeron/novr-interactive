using System.Reflection;
using NOVR;
using NOVR.MotionControls;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

internal static class WheelRuntimeChecks
{
    internal static readonly List<string> Warnings = new();

    internal static void Run(Action<bool, string> check)
    {
        foreach (var state in new[] { "unspawned", "dead", "ejected", "detached", "vehicle", "missile", "aircraft", "map selection" })
        foreach (var left in new[] { true, false })
        {
            Reset();
            GameManager.Aircraft = state == "unspawned" || state == "vehicle" || state == "missile" || state == "map selection" ? null : new();
            if (state == "dead") GameManager.Aircraft!.disabled = true;
            if (state == "ejected") GameManager.Aircraft!.Ejected = true;
            if (state == "detached") GameManager.Aircraft!.cockpit!.Detached = true;
            CameraStateManager.cameraMode = state == "vehicle" ? CameraMode.orbit : state == "missile" ? CameraMode.tv :
                state == "aircraft" ? CameraMode.chase : CameraMode.cockpit;
            if (state == "map selection")
            {
                CursorManager.Flags = CursorFlags.Map | CursorFlags.SelectionMenu | CursorFlags.CameraControlUI;
                DynamicMap.mapMaximized = true;
                Time.timeScale = 0;
                GameplayUI.AllowPauseKeybind = false;
            }
            // These stale flight reservations must not strand a dead player.
            GameManager.flightControlsEnabled = false;
            MotionFlightController.Instance = new() { StickHeld = true, ThrottleHeld = true };
            CockpitMapInteraction.Reserves = HelmetNightVision.Owns = true;
            using var wheel = new Wheel();
            wheel.Open(left);
            check(NativeHandOptionWheel.IsOpen && NativeHandOptionWheel.OwnsHand(left) && !NativeHandOptionWheel.OwnsHand(!left),
                "Standalone wheel opens and consumes only its own hand: " + state);
            check(wheel.Lease == null && SceneSingleton<RadialMenuMain>.i == null && wheel.Menus != null,
                "Standalone presentation needs neither native wheel nor aircraft: " + state);
            var mapLabel = DynamicMap.mapMaximized ? "CLOSE MAP" : "OPEN MAP";
            check(wheel.Menus!.GetComponentsInChildren<Text>(true).Select(text => text.text).Order().SequenceEqual(new[] { "GAME MENU", "VR MENU", mapLabel }.Order()),
                "Outside flight there are only Game Menu, VR Menu and the current map action.");
            check(HandMenuWheelGraphic.MenuAt(-1) == NativeWheelMenuAction.None && HandMenuWheelGraphic.MenuAt(3) == NativeWheelMenuAction.None &&
                  HandMenuWheelGraphic.MenuAt(2) == NativeWheelMenuAction.Map,
                "Three-item indices cannot expose a flight, weapon or eject action.");
            var original = wheel.View!.transform.position;
            var sample = TrackedControllers.Read(left);
            sample.Position = new Vector3(.4f, .5f, .6f);
            Set(left, sample);
            wheel.Late();
            var first = wheel.View.transform.position;
            sample.Position += new Vector3(.2f, 0, 0);
            Set(left, sample);
            wheel.Late();
            check(first != original && Math.Abs(wheel.View.transform.position.x - first.x - .2f) < .00001f &&
                  wheel.View.transform.position.y == first.y && wheel.View.transform.position.z == first.z &&
                  wheel.View.GetComponent<Canvas>().worldCamera == APIBus.CockpitHudCamera,
                "Standalone presentation follows the same tracked hand and HUD camera.");
            wheel.Select(left, 1, trigger: 1);
            check(!NativeHandOptionWheel.IsOpen && GameMenuChecks.Events.Last() == "VR menu" &&
                  NativeHandOptionWheel.OwnsHand(left) && !GameplayUI.GameIsPaused,
                "Returning the stick to centre opens VR Menu even if trigger is held; closing frame remains consumed.");
            wheel.Tick();
            check(!NativeHandOptionWheel.OwnsHand(left), "Standalone closing-frame ownership expires on the next frame.");
            wheel.Open(left);
            wheel.Select(left, 0);
            check(GameplayUI.GameIsPaused && GameMenuChecks.Events.Last() == "native pause",
                "The other standalone sector invokes the actual native Game Menu entry point.");
            check(Warnings.Count == 0, "Standalone opening and handoff complete without failure warnings.");
        }

        foreach (var left in new[] { true, false })
        foreach (var frame in new[] { false, true })
        {
            Reset();
            CameraStateManager.cameraMode = CameraMode.orbit;
            var target = new Unit();
            SceneSingleton<CameraStateManager>.i!.followingUnit = target;
            var sample = TrackedControllers.Read(left);
            sample.NativeFrame = frame;
            Set(left, sample);
            using var wheel = new Wheel();
            ControllerActions.OnToggleMap = () => check(!NativeHandOptionWheel.IsOpen && NativeHandOptionWheel.OwnsHand(left),
                "Map callback runs after ownership is released, while closing-frame input remains consumed.");
            wheel.Open(left);
            check(wheel.Menus!.GetComponentsInChildren<Text>(true).Any(text => text.text == "OPEN MAP"),
                "Closed spectator map offers OPEN MAP on either controller family and hand.");
            GameMenuChecks.Events.Clear();
            wheel.Select(left, 2);
            check(DynamicMap.mapMaximized && !NativeHandOptionWheel.IsOpen &&
                  string.Join(",", GameMenuChecks.Events) == "reset grips,reset buttons,open map",
                "Map selection releases flight input and opens the native map exactly once.");
            check(SceneSingleton<CameraStateManager>.i.followingUnit == target,
                "Opening the map leaves the spectated target unchanged.");
            wheel.Open(left);
            check(wheel.Menus!.GetComponentsInChildren<Text>(true).Any(text => text.text == "CLOSE MAP"),
                "An already maximized map keeps wheel access and offers CLOSE MAP.");
            GameMenuChecks.Events.Clear();
            wheel.Select(left, 2);
            check(!DynamicMap.mapMaximized && !NativeHandOptionWheel.IsOpen && GameMenuChecks.Events.Last() == "close map" &&
                  SceneSingleton<CameraStateManager>.i.followingUnit == target,
                "The same gesture closes the map without selecting a different spectator target.");
            wheel.Tick();
            check(!NativeHandOptionWheel.OwnsHand(left), "Map commit ownership expires after its closing frame.");
        }

        foreach (var blocked in new[] { "native lock", "selection lock", "context changed during release" })
        {
            Reset();
            using var wheel = new Wheel();
            wheel.Open(true);
            if (blocked == "native lock" || blocked == "selection lock") DynamicMap.AllowedToOpen = false;
            if (blocked == "selection lock") CursorManager.Flags = CursorFlags.SelectionMenu;
            if (blocked == "context changed during release") ControllerActions.MapBlocked = true;
            wheel.Tick();
            if (blocked != "context changed during release")
                check(wheel.Menus!.GetComponentsInChildren<Text>(true).Any(text => text.text == "OPEN MAP\nUnavailable"),
                    "The map choice refreshes to unavailable when the native selection lock appears.");
            wheel.Select(true, 2);
            check(!NativeHandOptionWheel.IsOpen && !DynamicMap.mapMaximized &&
                  string.Join(",", GameMenuChecks.Events) == "reset grips,reset buttons",
                "Map commit closes safely and honors the rechecked access policy: " + blocked);
        }

        Reset();
        using (var wheel = new Wheel())
        {
            wheel.Open(true);
            DynamicMap.mapMaximized = true;
            CursorManager.Flags = CursorFlags.Map;
            wheel.Tick();
            check(wheel.Menus!.GetComponentsInChildren<Text>(true).Any(text => text.text == "CLOSE MAP"),
                "A map opened externally refreshes the visible label without reopening the wheel.");
            wheel.Select(true, 2);
            check(!DynamicMap.mapMaximized && GameMenuChecks.Events.Last() == "close map",
                "Commit acts on current map state after an external map transition.");
        }

        foreach (var inactive in new[] { "HUD parent", "radial component" })
        foreach (var left in new[] { true, false })
        {
            Reset();
            CameraStateManager.cameraMode = CameraMode.free;
            DynamicMap.mapMaximized = true;
            CursorManager.Flags = CursorFlags.Map;
            var legacy = new LeaseChecks.Fixture();
            var hud = new GameObject("Unspawned flight HUD");
            legacy.Parent.transform.SetParent(hud.transform, false);
            legacy.Menu.SetActive(true);
            SceneSingleton<RadialMenuMain>.i = legacy.Native;
            RadialMenuMain.InUse = true;
            if (inactive == "HUD parent") hud.SetActive(false);
            else legacy.Native.enabled = false;
            check(legacy.Menu.activeSelf && RadialMenuMain.IsInUse() && !legacy.Native.isActiveAndEnabled &&
                  !NativeRadialMenuState.IsBlockingInput,
                "An inactive " + inactive + " cannot block input through its stale active radial child.");
            check(GameManager.Aircraft == null && WristMenuGameplayContext.Available(CursorFlags.None) && ControllerActions.MapAccessAvailable,
                "Before first spawn, the hidden native wheel leaves VR Menu and map access available.");
            using var wheel = new Wheel();
            wheel.Open(left);
            check(NativeHandOptionWheel.IsOpen && wheel.Lease == null,
                "Either standalone hand wheel opens while an inactive native radial still reports in-use.");
            wheel.Select(left, 2);
            check(!DynamicMap.mapMaximized && !NativeHandOptionWheel.IsOpen && GameMenuChecks.Events.Last() == "close map",
                "The standalone map action closes the spectator map despite the stale hidden radial state.");
            wheel.Open(left);
            wheel.Select(left, 1);
            check(GameMenuChecks.Events.Last() == "VR menu" && !NativeHandOptionWheel.IsOpen,
                "The standalone VR menu handoff is also reachable before first spawn.");
            check(legacy.Menu.activeSelf && legacy.Native.Opens == 0 && legacy.Native.Closes == 0,
                "Ignoring the inactive radial does not borrow, hide or otherwise mutate native aircraft UI.");
            hud.SetActive(true);
            legacy.Native.enabled = true;
            check(NativeRadialMenuState.IsBlockingInput && !WristMenuGameplayContext.Available(CursorFlags.None) && !ControllerActions.MapAccessAvailable,
                "Reactivating the same in-use native radial restores its input ownership.");
            wheel.Open(left);
            check(!NativeHandOptionWheel.IsOpen,
                "An active enabled legacy radial still blocks the standalone hand wheel.");
        }

        foreach (var blocked in new[] { "editing", "layout", "VR menu", "game menu", "chat", "dialogue", "loading", "not playing", "mapper", "no camera", "no tracking", "native wheel" })
        {
            Reset();
            if (blocked == "editing") CockpitHudWidgets.Editing = true;
            if (blocked == "layout") CockpitLayout.Active = true;
            if (blocked == "VR menu") WristMenu.IsOpen = true;
            if (blocked == "game menu") GameplayUI.GameIsPaused = true;
            if (blocked == "chat") CursorManager.Flags = CursorFlags.Chat;
            if (blocked == "dialogue") CursorManager.Flags = CursorFlags.Dialogue;
            if (blocked == "loading") CursorManager.Flags = CursorFlags.Loading;
            if (blocked == "not playing") GameManager.gameState = GameState.Menu;
            if (blocked == "mapper") GameManager.controlMapper = new() { isOpen = true };
            if (blocked == "no camera") APIBus.CockpitHudCamera = null;
            if (blocked == "no tracking") TrackedControllers.Available = false;
            if (blocked == "native wheel")
            {
                SceneSingleton<RadialMenuMain>.i = new GameObject("Desktop wheel").AddComponent<RadialMenuMain>();
                RadialMenuMain.InUse = true;
            }
            using var wheel = new Wheel();
            wheel.Open(true);
            check(!NativeHandOptionWheel.IsOpen && !NativeHandOptionWheel.OwnsHand(true) && GameMenuChecks.Events.Count == 0,
                "Standalone wheel does not steal input from " + blocked);
        }

        Reset();
        using (var wheel = new Wheel())
        {
            wheel.Open(true);
            wheel.Axis(true, new Vector2(0, -1));
            var sample = TrackedControllers.Left;
            sample.StickClick = true;
            sample.Axis = Vector2.zero;
            Set(true, sample);
            wheel.Tick();
            check(!NativeHandOptionWheel.IsOpen && GameMenuChecks.Events.Count == 0 && NativeHandOptionWheel.OwnsHand(true),
                "Click cancellation beats centre-return selection and consumes the closing frame.");
            wheel.Tick();
            check(!NativeHandOptionWheel.IsOpen, "Holding the cancel click cannot reopen standalone menus.");
            wheel.Open(false);
            wheel.Axis(false, new Vector2(0, 1));
            TrackedControllers.Right.Valid = false;
            wheel.Tick();
            check(!NativeHandOptionWheel.IsOpen && GameMenuChecks.Events.Count == 0,
                "Tracking loss cancels a highlighted standalone option without a menu callback.");
        }

        Reset();
        var native = new LeaseChecks.Fixture();
        SceneSingleton<RadialMenuMain>.i = native.Native;
        GameManager.Aircraft = new();
        using (var wheel = new Wheel())
        {
            wheel.Open(true);
            check(NativeHandOptionWheel.IsOpen && wheel.Lease != null && native.Native.Opens == 1 && NativeHandOptionWheel.OwnsNative(native.Native),
                "A live cockpit still opens and owns the existing native full-flight wheel.");
            wheel.Axis(true, new Vector2(0, 1));
            GameManager.Aircraft.Ejected = true;
            wheel.Tick();
            check(!NativeHandOptionWheel.IsOpen && native.Native.Closes == 1 && native.MainAction.Activations == 0,
                "Leaving flight cancels the borrowed native selection before any aircraft action can fire.");
            wheel.Open(true);
            check(NativeHandOptionWheel.IsOpen && wheel.Lease == null && native.Native.Opens == 1,
                "A fresh click after ejection opens only standalone menus without reopening native flight actions.");
            GameManager.Aircraft = new();
            wheel.Tick();
            check(!NativeHandOptionWheel.IsOpen, "Returning to flight cancels standalone gesture state rather than carrying a menu selection across context.");
        }

        var graphicObject = new GameObject("Standalone sectors", typeof(HandMenuWheelGraphic));
        var graphic = graphicObject.GetComponent<HandMenuWheelGraphic>();
        graphic.Initialize();
        var mesh = new VertexHelper();
        graphic.Populate(mesh);
        check(mesh.Vertices.Count == 198 && mesh.Triangles.Count == 192 &&
              mesh.Triangles.All(t => t.a >= 0 && t.b < mesh.Vertices.Count && t.c < mesh.Vertices.Count),
            "The standalone wheel builds three bounded sector meshes without native sprites.");
        check(mesh.Vertices.All(v => float.IsFinite(v.position.x) && float.IsFinite(v.position.y) &&
              v.position.x * v.position.x + v.position.y * v.position.y <= 144.01f * 144.01f),
            "Standalone geometry fits its declared 300-pixel hand-wheel bounds.");
        graphic.Hover(1);
        graphic.Populate(mesh);
        check(mesh.Vertices.Take(66).All(v => v.color == mesh.Vertices[0].color) &&
              mesh.Vertices.Skip(66).Take(66).All(v => v.color == mesh.Vertices[66].color) &&
              mesh.Vertices.Skip(132).All(v => v.color == mesh.Vertices[0].color) && mesh.Vertices[0].color != mesh.Vertices[66].color,
            "Hover highlights only the selected standalone sector.");
        var labels = graphic.GetComponentsInChildren<Text>(true);
        for (var index = 0; index < labels.Length; index++)
        {
            var rect = labels[index].rectTransform;
            check(HandOptionWheelGesture.Sector(rect.anchoredPosition.x, rect.anchoredPosition.y, HandMenuWheelGraphic.Count) == index &&
                  Math.Abs(rect.anchoredPosition.x) + rect.sizeDelta.x * .5f < 150 && Math.Abs(rect.anchoredPosition.y) + rect.sizeDelta.y * .5f < 150,
                "Each three-way label fits the wheel and matches its selectable sector.");
        }
        graphic.RefreshMapState(false, false);
        check(!graphic.Hover(2) && labels[2].text == "OPEN MAP\nUnavailable" && labels[2].color != Color.white,
            "A native-locked map choice is visibly disabled and cannot receive hover feedback.");
        graphic.RefreshMapState(false, true);
        check(graphic.Hover(2) && labels[2].text == "OPEN MAP" && labels[2].color == Color.white,
            "Removing the native lock restores map label and hover feedback.");
    }

    private static void Reset()
    {
        NativeHandOptionWheel.Cancel();
        Warnings.Clear();
        GameMenuChecks.Events.Clear();
        SceneSingleton<RadialMenuMain>.i = null;
        RadialMenuMain.InUse = false;
        GameManager.Aircraft = null;
        GameManager.gameState = GameState.SinglePlayer;
        GameManager.flightControlsEnabled = true;
        GameManager.controlMapper = null;
        CameraStateManager.cameraMode = CameraMode.cockpit;
        DynamicMap.mapMaximized = false;
        DynamicMap.AllowedToOpen = true;
        ControllerActions.MapBlocked = false;
        ControllerActions.OnToggleMap = null;
        CursorManager.Flags = CursorFlags.None;
        CockpitLayout.Active = CockpitHudWidgets.Editing = CockpitMapInteraction.Reserves = HelmetNightVision.Owns = false;
        WristMenu.IsOpen = false;
        WristMenu.Available = true;
        GameplayUI.GameIsPaused = false;
        GameplayUI.AllowPauseKeybind = true;
        SceneSingleton<GameplayUI>.i = new GameObject("Gameplay UI").AddComponent<GameplayUI>();
        APIBus.CockpitHudCamera = new GameObject("Tracked HUD").AddComponent<Camera>();
        APIBus.MainCamera = new GameObject("Scene camera").AddComponent<Camera>();
        SceneSingleton<CameraStateManager>.i = new() { followingUnit = null };
        APIBus.CockpitHudCamera.transform.SetParent(new GameObject("Tracking root").transform, false);
        MotionFlightController.Instance = new() { StickHeld = false, ThrottleHeld = false };
        ControllerActions.Instance = new();
        ModConfiguration.Instance.MotionControls.LeftHanded.Value = false;
        TrackedControllers.Available = true;
        TrackedControllers.Left = TrackedControllers.Right = new()
        {
            Valid = true, HasAxis = true, NativeFrame = false, Position = Vector3.zero,
            Rotation = Quaternion.identity, Device = new(true), Axis = Vector2.zero, Trigger = 0, StickClick = false
        };
        Time.timeScale = 1;
    }
    private static void Set(bool left, ControllerSample sample)
    {
        if (left) TrackedControllers.Left = sample;
        else TrackedControllers.Right = sample;
    }
    private sealed class Wheel : IDisposable
    {
        private readonly NativeHandOptionWheel _component;
        internal Wheel()
        {
            _component = new GameObject("Wheel coordinator", typeof(NativeHandOptionWheel)).GetComponent<NativeHandOptionWheel>();
            Invoke("Awake");
        }
        internal NativeRadialMenuLease? Lease => Field<NativeRadialMenuLease>("_lease");
        internal HandMenuWheelGraphic? Menus => Field<HandMenuWheelGraphic>("_menus");
        internal GameObject? View => Field<GameObject>("_view");
        private T? Field<T>(string name) where T : class => (T?)typeof(NativeHandOptionWheel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_component);
        private void Invoke(string method) => typeof(NativeHandOptionWheel).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_component, null);
        internal void Tick() { Time.frameCount++; Time.unscaledTime += .02f; Invoke("Update"); }
        internal void Late() => Invoke("LateUpdate");
        internal void Open(bool left)
        {
            var sample = TrackedControllers.Read(left);
            sample.StickClick = false; sample.Axis = Vector2.zero; sample.Trigger = 0;
            Set(left, sample); Tick();
            sample.StickClick = true; Set(left, sample); Tick();
        }
        internal void Axis(bool left, Vector2 axis, float trigger = 0)
        {
            var sample = TrackedControllers.Read(left);
            sample.StickClick = false; sample.Axis = axis; sample.Trigger = trigger;
            Set(left, sample); Tick();
        }
        internal void Select(bool left, int index, float trigger = 0)
        {
            var angle = index * 2f * MathF.PI / HandMenuWheelGraphic.Count;
            Axis(left, new Vector2(MathF.Sin(angle), MathF.Cos(angle)), trigger);
            Axis(left, Vector2.zero, trigger);
        }
        public void Dispose() => Invoke("OnDestroy");
    }
}
