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
            // Native pilot death also disables its aircraft on the server.
            if (state == "dead") { GameManager.Aircraft!.disabled = true; GameManager.Aircraft.pilots[0]!.dead = true; }
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
            // A seated, living pilot keeps native Eject even without a flight
            // wheel: detached cockpit, or watching their own aircraft externally.
            var canEject = state is "detached" or "aircraft";
            var expected = canEject ? new[] { "GAME MENU", "VR MENU", mapLabel, "EJECT" } : new[] { "GAME MENU", "VR MENU", mapLabel };
            check(wheel.Menus!.GetComponentsInChildren<Text>(true).Where(text => text.gameObject.activeSelf).Select(text => text.text).Order().SequenceEqual(expected.Order()),
                "Outside flight there are Game Menu, VR Menu, the current map action and Eject only for a seated living pilot: " + state);
            check(wheel.Menus!.MenuAt(-1) == NativeWheelMenuAction.None && wheel.Menus!.MenuAt(4) == NativeWheelMenuAction.None &&
                  wheel.Menus!.MenuAt(2) == NativeWheelMenuAction.Map &&
                  wheel.Menus!.MenuAt(3) == (canEject ? NativeWheelMenuAction.Eject : NativeWheelMenuAction.None),
                "Standalone indices never expose flight or weapon actions; Eject only when native ejection is possible: " + state);
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

        WreckEjectChecks(check);
        CameraChoiceChecks(check);
        Reset();

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
            check(HandOptionWheelGesture.Sector(rect.anchoredPosition.x, rect.anchoredPosition.y, graphic.Count) == index &&
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

    // A crash can leave the cockpit intact but detached, or the airframe
    // destroyed around a living pilot. Native respawn stays locked until the
    // pilot ejects, so the standalone wheel must offer the native ejection.
    private static void WreckEjectChecks(Action<bool, string> check)
    {
        foreach (var wreck in new[] { "detached", "destroyed airframe", "native wheel missing" })
        foreach (var left in new[] { true, false })
        {
            Reset();
            GameManager.Aircraft = new();
            if (wreck == "detached") GameManager.Aircraft.cockpit!.Detached = true;
            if (wreck == "destroyed airframe") GameManager.Aircraft.disabled = true;
            // "native wheel missing": a usable cockpit whose native HUD wheel is
            // inactive must not leave the stick click doing nothing at all.
            using var wheel = new Wheel();
            wheel.Open(left);
            check(NativeHandOptionWheel.IsOpen && wheel.Lease == null && wheel.Menus!.EjectAvailable && wheel.Menus!.Count == 4,
                "A wreck still opens a standalone wheel with Eject: " + wreck);
            wheel.Select(left, HandMenuWheelGraphic.EjectIndex);
            check(NativeHandOptionWheel.IsOpen && GameManager.Aircraft.EjectionRequests == 0,
                "Selecting Eject only arms the hold confirmation: " + wreck);
            wheel.Axis(left, Vector2.zero); // the hold needs a fresh click after selection
            var sample = TrackedControllers.Read(left);
            sample.Axis = Vector2.zero; sample.Trigger = 0; sample.StickClick = true;
            Set(left, sample);
            for (var frame = 0; frame < 80 && GameManager.Aircraft.EjectionRequests == 0; frame++)
                wheel.Tick();
            check(GameManager.Aircraft.EjectionRequests == 1 && !NativeHandOptionWheel.IsOpen,
                "Holding the centred stick click calls the native ejection exactly once: " + wreck);
            for (var frame = 0; frame < 10; frame++)
                wheel.Tick();
            check(GameManager.Aircraft.EjectionRequests == 1, "A continued hold cannot eject twice: " + wreck);
        }

        Reset();
        GameManager.Aircraft = new();
        GameManager.Aircraft.cockpit!.Detached = true;
        using (var wheel = new Wheel())
        {
            wheel.Open(true);
            wheel.Select(true, HandMenuWheelGraphic.EjectIndex);
            wheel.Axis(true, new Vector2(.9f, 0));
            check(!NativeHandOptionWheel.IsOpen && GameManager.Aircraft.EjectionRequests == 0,
                "Tilting the stick cancels a wreck ejection without ejecting.");
            wheel.Open(true);
            wheel.Select(true, HandMenuWheelGraphic.EjectIndex);
            GameManager.Aircraft.pilots[0]!.dead = true;
            wheel.Axis(true, Vector2.zero);
            var sample = TrackedControllers.Read(true);
            sample.Axis = Vector2.zero; sample.StickClick = true;
            Set(true, sample);
            for (var frame = 0; frame < 80; frame++)
                wheel.Tick();
            check(GameManager.Aircraft.EjectionRequests == 0, "A pilot who dies during the hold is never ejected.");
        }
        foreach (var gone in new[] { "ejected", "dead", "no pilots", "no aircraft" })
        {
            Reset();
            GameManager.Aircraft = gone == "no aircraft" ? null : new();
            if (GameManager.Aircraft != null)
            {
                GameManager.Aircraft.cockpit!.Detached = true;
                if (gone == "ejected") GameManager.Aircraft.Ejected = true;
                if (gone == "dead") GameManager.Aircraft.pilots[0]!.dead = true;
                if (gone == "no pilots") GameManager.Aircraft.pilots = System.Array.Empty<Pilot?>();
            }
            check(!NativeHandOptionWheel.CanEjectFromWreck(GameManager.Aircraft), "No wreck Eject without a seated living pilot: " + gone);
        }
    }

    // The quick wheel is where the camera is changed: out of the cockpit from
    // the flight wheel, back in or into the free camera from the menu wheel.
    private static void CameraChoiceChecks(Action<bool, string> check)
    {
        string[] Labels(Wheel wheel) => wheel.Menus!.GetComponentsInChildren<Text>(true)
            .Where(text => text.gameObject.activeSelf).Select(text => text.text.Replace('\n', ' ')).ToArray();
        foreach (var left in new[] { true, false })
        {
            // Flying from outside: a healthy aircraft, so Eject and Cockpit view both show.
            Reset();
            GameManager.Aircraft = new();
            CameraStateManager.cameraMode = CameraMode.orbit;
            NOVR.VrCamera.PilotView.CanGoCockpit = true;
            using (var wheel = new Wheel())
            {
                wheel.Open(left);
                check(wheel.Lease == null && wheel.Menus!.Count == 5 && wheel.Menus.EjectAvailable &&
                      wheel.Menus.MenuAt(HandMenuWheelGraphic.EjectIndex) == NativeWheelMenuAction.Eject &&
                      wheel.Menus.ViewIndex == 4 && wheel.Menus.MenuAt(4) == NativeWheelMenuAction.CockpitView &&
                      wheel.Menus.MenuAt(5) == NativeWheelMenuAction.None,
                    "Flying from outside, the menu wheel adds Cockpit view after Eject, which keeps its index");
                check(Labels(wheel).Order().SequenceEqual(new[] { "GAME MENU", "VR MENU", "OPEN MAP", "EJECT", "COCKPIT VIEW" }.Order()),
                    "The cockpit choice is labelled in plain words");
                foreach (var label in wheel.Menus!.GetComponentsInChildren<Text>(true).Where(text => text.gameObject.activeSelf))
                {
                    var rect = label.rectTransform;
                    var index = HandOptionWheelGesture.Sector(rect.anchoredPosition.x, rect.anchoredPosition.y, wheel.Menus.Count);
                    var expected = label.text.StartsWith("GAME") ? 0 : label.text.StartsWith("VR") ? 1 : label.text.Contains("MAP") ? 2 :
                        label.text == "EJECT" ? 3 : 4;
                    check(index == expected, "Every label of the five-way wheel sits on its own selectable sector");
                }
                wheel.Select(left, 4);
                check(!NativeHandOptionWheel.IsOpen && NOVR.VrCamera.PilotView.Calls.SequenceEqual(new[] { "cockpit" }) &&
                      GameManager.Aircraft!.EjectionRequests == 0 && !GameplayUI.GameIsPaused,
                    "Selecting Cockpit view closes the wheel and asks only for the cockpit camera");
            }

            // Spectating someone else's unit: no aircraft of our own.
            Reset();
            CameraStateManager.cameraMode = CameraMode.orbit;
            NOVR.VrCamera.PilotView.CanReleaseTarget = true;
            using (var wheel = new Wheel())
            {
                wheel.Open(left);
                check(wheel.Menus!.Count == 4 && !wheel.Menus.EjectAvailable && wheel.Menus.ViewIndex == 3 &&
                      wheel.Menus.MenuAt(3) == NativeWheelMenuAction.FreeCamera && wheel.Menus.MenuAt(4) == NativeWheelMenuAction.None &&
                      Labels(wheel).Contains("FREE CAMERA") && !Labels(wheel).Contains("EJECT"),
                    "Following a unit as a spectator, the fourth sector is Free camera, never Eject");
                var mesh = new VertexHelper();
                wheel.Menus.Populate(mesh);
                check(mesh.Vertices.Count == 4 * 66 && mesh.Vertices[3 * 66].color == mesh.Vertices[0].color,
                    "A camera sector in the fourth place is drawn like the other menu sectors, not in Eject red");
                wheel.Select(left, 3);
                check(!NativeHandOptionWheel.IsOpen && NOVR.VrCamera.PilotView.Calls.SequenceEqual(new[] { "free" }),
                    "Selecting Free camera releases the followed unit and nothing else");
                // The same wheel, reopened where no camera choice applies.
                NOVR.VrCamera.PilotView.CanReleaseTarget = false;
                wheel.Tick();
                wheel.Open(left);
                check(wheel.Menus.Count == 3 && wheel.Menus.ViewIndex == -1 && wheel.Menus.MenuAt(3) == NativeWheelMenuAction.None &&
                      !Labels(wheel).Contains("FREE CAMERA"),
                    "The camera sector disappears again when there is nothing to offer");
            }

            // In the cockpit: the flight wheel gains Outside view beside the two menus.
            Reset();
            GameManager.Aircraft = new();
            var fixture = new LeaseChecks.Fixture();
            SceneSingleton<RadialMenuMain>.i = fixture.Native;
            NOVR.VrCamera.PilotView.CanGoExternal = true;
            using (var wheel = new Wheel())
            {
                wheel.Open(left);
                var lease = wheel.Lease;
                check(lease != null && wheel.Menus == null && lease.Count == 4 &&
                      lease.MenuAt(1) == NativeWheelMenuAction.GameMenu && lease.MenuAt(2) == NativeWheelMenuAction.VrMenu &&
                      lease.MenuAt(3) == NativeWheelMenuAction.OutsideView && lease.ActionAt(3) == null && lease.MenuAt(4) == NativeWheelMenuAction.None,
                    "In the cockpit the flight wheel offers Outside view after the game's own actions and the two menus");
                check(lease!.Hover(3) && fixture.Caption.text == "OUTSIDE VIEW" && fixture.Native.selectedAction == null,
                    "Hovering Outside view shows its caption and selects no aircraft action");
                // Fourth of four sectors: straight left on the thumbstick.
                wheel.Axis(left, new Vector2(-1, 0));
                wheel.Axis(left, Vector2.zero);
                check(!NativeHandOptionWheel.IsOpen && NOVR.VrCamera.PilotView.Calls.SequenceEqual(new[] { "outside" }) &&
                      fixture.MainAction.Activations == 0 && fixture.Main.transform.Children.Count == 2,
                    "Selecting Outside view asks only for the camera change and returns the borrowed wheel untouched");
            }
            SceneSingleton<RadialMenuMain>.i = null;
        }

        // Flying with the thumbsticks: a hard push can click the stick.
        foreach (var left in new[] { true, false })
        {
            Reset();
            GameManager.Aircraft = new();
            CameraStateManager.cameraMode = CameraMode.orbit;
            ExternalFlightInput.Active = true;
            using var wheel = new Wheel();
            var sample = TrackedControllers.Read(left);
            sample.StickClick = false; sample.Axis = new Vector2(.8f, -.5f);
            Set(left, sample); wheel.Tick();
            sample.StickClick = true;
            Set(left, sample); wheel.Tick();
            check(!NativeHandOptionWheel.IsOpen && !NativeHandOptionWheel.OwnsHand(left),
                "Flying from outside, clicking a pushed thumbstick does not open the wheel or take the hand");
            sample.StickClick = false; sample.Axis = Vector2.zero;
            Set(left, sample); wheel.Tick();
            sample.StickClick = true;
            Set(left, sample); wheel.Tick();
            check(NativeHandOptionWheel.IsOpen && NativeHandOptionWheel.OwnsHand(left),
                "A click on a centred thumbstick still opens the wheel while flying from outside");
            wheel.Axis(left, Vector2.zero);
            NativeHandOptionWheel.Cancel();
            ExternalFlightInput.Active = false;
            sample.StickClick = false; sample.Axis = new Vector2(.8f, -.5f);
            Set(left, sample); wheel.Tick();
            sample.StickClick = true;
            Set(left, sample); wheel.Tick();
            check(NativeHandOptionWheel.IsOpen, "Everywhere else a click opens the wheel whatever the stick is doing, as before");
        }

        // The choices recheck themselves: a stale sector does nothing.
        Reset();
        foreach (var action in new[] { NativeWheelMenuAction.OutsideView, NativeWheelMenuAction.CockpitView, NativeWheelMenuAction.FreeCamera })
        {
            NOVR.VrCamera.PilotView.Reset();
            check(!NativeWheelMenuActions.Open(action, () => { }) && NOVR.VrCamera.PilotView.Calls.Count == 1,
                "A camera choice that no longer applies reports failure: " + action);
            NOVR.VrCamera.PilotView.Reset();
            GameplayUI.GameIsPaused = true;
            check(!NativeWheelMenuActions.Open(action, () => { }) && NOVR.VrCamera.PilotView.Calls.Count == 0,
                "A paused game never reaches the camera: " + action);
            GameplayUI.GameIsPaused = false;
            CursorManager.Flags = CursorFlags.Chat;
            check(!NativeWheelMenuActions.Open(action, () => { }) && NOVR.VrCamera.PilotView.Calls.Count == 0,
                "A blocking cursor context never reaches the camera: " + action);
            CursorManager.Flags = CursorFlags.None;
        }

        // Entry geometry with and without the third sector.
        foreach (var count in new[] { 0, 3, 8 })
        {
            var parent = new GameObject("Main page");
            var template = new GameObject("Sector template");
            template.AddComponent<Image>();
            var objects = new List<GameObject>();
            for (var i = 0; i < count; i++)
            {
                var icon = new GameObject("Icon " + i);
                var sector = new GameObject("Sector " + i);
                icon.transform.SetParent(parent.transform, false);
                sector.transform.SetParent(parent.transform, false);
                sector.AddComponent<Image>().fillAmount = .25f;
                objects.Add(icon);
                objects.Add(sector);
            }
            using (var entry = new NativeWheelMenuEntries(parent.transform, objects, count, template, true))
            {
                check(entry.Count == 3 && parent.transform.Children.Count == count * 2 + 6 &&
                      entry.ActionAt(count + 2) == NativeWheelMenuAction.OutsideView && entry.ActionAt(count + 3) == NativeWheelMenuAction.None,
                    "With the outside view available exactly three sectors and labels are added");
                var label = parent.GetComponent<RectTransform>().Children[count * 2 + 5].localPosition;
                check(HandOptionWheelGesture.Sector(label.x, label.y, count + 3) == count + 2, "The Outside view label sits on its own sector");
                for (var i = 0; i < count; i++)
                    check(Math.Abs(objects[i * 2 + 1].GetComponent<Image>().fillAmount - 1f / (count + 3)) < .00001f,
                        "Borrowed sectors shrink to share the wheel with three additions");
            }
            check(parent.transform.Children.Count == objects.Count, "All three additions are removed on release");
            using (var entry = new NativeWheelMenuEntries(parent.transform, objects, count, template))
                check(entry.Count == 2 && entry.ActionAt(count + 2) == NativeWheelMenuAction.None,
                    "Without the outside view the wheel is exactly as before");
        }
    }

    private static void Reset()
    {
        NativeHandOptionWheel.Cancel();
        NOVR.VrCamera.PilotView.Reset();
        ExternalFlightInput.Active = false;
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
            var angle = index * 2f * MathF.PI / (Menus?.Count ?? HandMenuWheelGraphic.BaseCount);
            Axis(left, new Vector2(MathF.Sin(angle), MathF.Cos(angle)), trigger);
            Axis(left, Vector2.zero, trigger);
        }
        public void Dispose() => Invoke("OnDestroy");
    }
}
