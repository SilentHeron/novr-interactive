using NOVR;
using NOVR.MotionControls;
using NOVR.VrCamera;
using UnityEngine;
using UnityEngine.XR;

// Flying the local aircraft from the outside view: stick mapping, the view
// switch itself, and the hand-off between flight sticks and camera sticks.
internal static class OutsideFlightChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
        bool Near(float a, float b) => Math.Abs(a - b) < .0001f;

        // ---- Mapping (no game state): the game's gamepad layout ----
        var sticks = new ExternalFlightSticks();
        ExternalFlightMotion Read(float lx = 0, float ly = 0, float rx = 0, float ry = 0, float lt = 0, float rt = 0,
            float lg = 0, float rg = 0, float dt = .02f, bool left = true, bool right = true, bool available = true) =>
            sticks.Update(available, left, right, lx, ly, rx, ry, lt, rt, lg, rg, dt);
        Check(!Read(lx: 1, ly: -1).Flying, "A thumbstick held while entering the outside view does not fly the aircraft");
        Check(!Read(lx: 1, ly: -1).Flying, "A held thumbstick cannot arm itself over time");
        Check(Read().Flying && Read().Pitch == 0 && Read().Roll == 0, "A centred stick arms flight with neutral input");
        Check(Near(Read(ly: -1).Pitch, -1) && Near(Read(ly: 1).Pitch, 1), "Pulling the left thumbstick back commands nose-up (the game's negative pitch)");
        Check(Near(Read(lx: 1).Roll, 1) && Near(Read(lx: -1).Roll, -1), "Left thumbstick right rolls right");
        var half = Read(lx: .6f).Roll;
        Check(half > 0 && half < .5f, "Small deflections are finer than linear, full deflection still reaches the stop");
        Check(Read(lx: .1f, ly: -.1f).Pitch == 0 && Read(lx: .1f).Roll == 0, "Thumbstick drift does not move the aircraft");
        Check(float.IsFinite(Read(lx: float.MaxValue, ly: float.MaxValue).Roll), "Malformed finite values cannot overflow");
        var flown = Read(lx: 1, ly: 1);
        Check(!flown.Camera.HasOrbitMotion && flown.Yaw == 0, "The left thumbstick only flies: no camera and no rudder");
        sticks.Reset();
        Check(!Read(rx: 1, ry: 1).Camera.HasOrbitMotion, "The camera stick also needs a fresh centre");
        Read();
        var look = Read(rx: 1);
        Check(Near(look.Camera.YawDegrees, 1.5f) && look.Camera.PitchDegrees == 0 && look.Camera.Distance == 0 &&
              look.Roll == 0 && look.Pitch == 0 && !look.Rudder,
            "The right thumbstick orbits the camera and commands nothing");
        Check(Read(ry: 1).Camera.PitchDegrees < 0 && Read(ry: -1).Camera.PitchDegrees > 0,
            "Camera stick up raises the vantage, matching the spectator orbit");

        // Triggers are the rudder, left and right as on the gamepad layout.
        Check(Near(Read(rt: 1).Yaw, 1) && Read(rt: 1).Rudder, "Right trigger is full right rudder");
        Read();
        Check(Near(Read(lt: 1).Yaw, -1), "Left trigger is full left rudder");
        var part = Read(lt: 0, rt: .5f).Yaw;
        Check(part > 0 && part < .5f, "Rudder is proportional and fine near the start of the pull");
        Check(!Read(rt: .05f).Rudder, "A resting finger on a trigger is not rudder");
        Read();
        Check(Near(Read(lt: 1, rt: 1).Yaw, 0), "Both triggers cancel");
        sticks.Reset();
        Check(!Read(rt: 1).Rudder && !Read(rt: 1).Rudder, "A trigger already pulled when the outside view starts is not rudder");
        Check(!Read().Rudder && Read(rt: 1).Yaw > 0, "After one release the trigger is rudder");
        Check(!Read(rt: 1, right: false).Rudder && !Read(rt: 1).Rudder, "A trigger held through lost tracking needs a release too");

        // A grip held turns the camera stick's up and down into distance.
        foreach (var hand in new[] { "left", "right" })
        {
            float Lg(float v) => hand == "left" ? v : 0;
            float Rg(float v) => hand == "right" ? v : 0;
            sticks.Reset(); Read();
            Check(!Read(lg: Lg(1), rg: Rg(1), rx: 1, ry: 1).Camera.HasOrbitMotion,
                "Squeezing a grip with the camera stick deflected does not zoom: " + hand);
            Read(lg: Lg(1), rg: Rg(1));
            var zoom = Read(lg: Lg(1), rg: Rg(1), rx: 1, ry: 1);
            Check(zoom.Camera.Distance < 0 && zoom.Camera.YawDegrees > 0 && zoom.Camera.PitchDegrees == 0,
                "With a grip held the camera stick moves closer or farther and still orbits: " + hand);
            Check(Read(lg: Lg(1), rg: Rg(1), ry: -1).Camera.Distance > 0, "Stick back moves the camera away: " + hand);
            Check(!Read(rx: 1, ry: 1).Camera.HasOrbitMotion, "Releasing the grip with the stick deflected does not swing the camera: " + hand);
            Read();
            Check(Read(ry: 1).Camera.PitchDegrees < 0 && Read(ry: 1).Camera.Distance == 0, "After centring, the stick looks around again: " + hand);
            Read(); Read(lg: Lg(.45f), rg: Rg(.45f));
            Check(Read(lg: Lg(.45f), rg: Rg(.45f), ry: 1).Camera.Distance == 0, "A light hold on the controller is not the zoom grip: " + hand);
            sticks.Reset();
            Read(lg: Lg(1), rg: Rg(1)); Read(lg: Lg(1), rg: Rg(1));
            Check(Read(lg: Lg(1), rg: Rg(1), ry: 1).Camera.Distance == 0 && Read(lg: Lg(1), rg: Rg(1), ry: 1).Camera.PitchDegrees < 0,
                "A grip still squeezed from holding a virtual handle is not the zoom grip until released: " + hand);
        }
        sticks.Reset(); Read();
        var gripOnly = Read(lg: 1, rg: 1, lx: 1, ly: -1);
        Check(gripOnly.Flying && gripOnly.Roll > 0 && gripOnly.Pitch < 0 && !gripOnly.Rudder, "Grips never change what the flight stick or the rudder do");

        sticks.Reset(); Read();
        Check(!Read(lx: 1, left: false).Flying && !Read(lx: 1).Flying, "Losing the flight hand's tracking requires a fresh centre");
        Read();
        Check(Read(rx: 1, left: false).Camera.YawDegrees > 0, "The camera hand keeps working while the flight hand is untracked");
        Read();
        Check(Read(lx: 1, right: false).Roll > 0, "The flight hand keeps working while the camera hand is untracked");
        foreach (var bad in new[] { float.NaN, float.PositiveInfinity, 0f, -1f })
        {
            sticks.Reset(); Read();
            Check(!Read(lx: 1, dt: bad).Flying && !Read(lx: 1).Flying, "An invalid frame time resets every gate");
        }
        foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            sticks.Reset(); Read();
            var broken = Read(rt: bad, rg: bad, ry: 1);
            Check(!broken.Rudder && broken.Camera.Distance == 0 && !Read(rt: 1).Rudder, "A malformed trigger or grip value is ignored and needs a release");
        }
        sticks.Reset(); Read();
        Check(!Read(lx: 1, rt: 1, available: false).Flying && !Read(lx: 1, rt: 1).Flying && !Read(lx: 1, rt: 1).Rudder,
            "A menu or map resets every gate");

        // ---- View switching against native camera doubles ----
        CameraStateManager camera = null!;
        Aircraft aircraft = null!;
        var ui = new GameplayUI();
        void Fresh(bool cockpit = false)
        {
            Time.frameCount += 3;
            Time.timeScale = 1;
            camera = new CameraStateManager();
            aircraft = new Aircraft();
            camera.followingUnit = aircraft;
            camera.SwitchState(cockpit ? camera.cockpitState : camera.orbitState);
            camera.Switches = 0;
            SceneSingleton<CameraStateManager>.i = camera;
            SceneSingleton<GameplayUI>.i = ui;
            GameManager.LocalAircraft = aircraft;
            GameManager.gameState = GameState.SinglePlayer;
            GameManager.flightControlsEnabled = true;
            GameManager.controlMapper = null;
            XRSettings.enabled = true;
            DynamicMap.mapMaximized = false;
            CursorManager.Flags = CursorFlags.None;
            TrackedControllers.Available = true;
            TrackedControllers.Left = new() { Valid = true, HasAxis = true };
            TrackedControllers.Right = new() { Valid = true, HasAxis = true };
            NativeHandOptionWheel.IsOpen = NativeHandOptionWheel.LeftOwned = NativeHandOptionWheel.RightOwned = false;
            NOVR.VrUi.WristMenu.IsOpen = false;
            ModConfiguration.Instance.MotionControls.Enabled.Value = true;
            ModConfiguration.Instance.MotionControls.LeftHanded.Value = false;
            // Every real visit to the outside view starts from another view,
            // sampled each frame by the button dispatcher.
            var mode = CameraStateManager.cameraMode;
            CameraStateManager.cameraMode = CameraMode.cockpit;
            ExternalFlightInput.Sample();
            CameraStateManager.cameraMode = mode;
            Time.frameCount += 2;
        }
        void Frame(float lx = 0, float ly = 0, float rx = 0, float ry = 0)
        {
            Time.frameCount++;
            TrackedControllers.Left.Axis = new(lx, ly);
            TrackedControllers.Right.Axis = new(rx, ry);
        }

        Fresh(cockpit: true);
        Check(PilotView.TryGetPiloted(out var foundCamera, out var foundAircraft) && foundCamera == camera && foundAircraft == aircraft &&
              !PilotView.External && PilotView.CanGoExternal && !PilotView.CanGoCockpit && !PilotView.CanReleaseTarget,
            "A live pilot in the cockpit can leave for the outside view and nothing else");
        Check(PilotView.ToExternal() && CameraStateManager.cameraMode == CameraMode.orbit && camera.currentState == camera.orbitState &&
              camera.Switches == 1, "Outside view is one native state switch into the orbit camera");
        Check(PilotView.External && PilotView.CanGoCockpit && !PilotView.CanGoExternal && !PilotView.ToExternal() && camera.Switches == 1,
            "Once outside, only the way back is offered");
        Check(!PilotView.CanReleaseTarget && !PilotView.ReleaseTarget() && camera.followingUnit == aircraft,
            "The free camera is never offered while flying: the camera stays on the aircraft");
        Check(PilotView.ToCockpit() && CameraStateManager.cameraMode == CameraMode.cockpit && camera.Switches == 2,
            "Cockpit view is one native state switch back");
        Check(PilotView.Toggle() && PilotView.External && PilotView.Toggle() && CameraStateManager.cameraMode == CameraMode.cockpit,
            "Toggle goes out and comes back");
        foreach (var mode in new[] { CameraMode.chase, CameraMode.tv })
        {
            Fresh(); CameraStateManager.cameraMode = mode;
            Check(PilotView.External && PilotView.ToCockpit(), "The chase and flyby cameras also count as outside and can return: " + mode);
        }
        var blockers = new (string Name, Action Block)[]
        {
            ("VR off", () => XRSettings.enabled = false),
            ("paused", () => Time.timeScale = 0),
            ("native flight input disabled", () => GameManager.flightControlsEnabled = false),
            ("maximized map", () => DynamicMap.mapMaximized = true),
            ("game menu", () => CursorManager.Flags = CursorFlags.GameMenu),
            ("selection menu", () => CursorManager.Flags = CursorFlags.SelectionMenu),
            ("control mapping", () => GameManager.controlMapper = new() { isOpen = true }),
            ("outside gameplay", () => GameManager.gameState = GameState.Menu),
            ("no local aircraft", () => GameManager.LocalAircraft = null),
            ("destroyed aircraft", () => aircraft.disabled = true),
            ("ejected", () => aircraft.Ejected = true),
            ("detached cockpit", () => aircraft.cockpit!.Detached = true),
            ("missing cockpit", () => aircraft.cockpit = null),
            ("dead pilot", () => aircraft.pilots![0].dead = true),
            ("pilot not in player state", () => aircraft.pilots![0].currentState = new PilotState()),
            ("camera on another unit", () => camera.followingUnit = new Unit()),
            ("no camera manager", () => SceneSingleton<CameraStateManager>.i = null)
        };
        foreach (var blocker in blockers)
        {
            Fresh(cockpit: true); blocker.Block();
            Check(!PilotView.CanGoExternal && !PilotView.ToExternal() && camera.Switches == 0 && CameraStateManager.cameraMode == CameraMode.cockpit,
                "No outside view while: " + blocker.Name);
            Fresh(); blocker.Block();
            Check(!PilotView.CanGoCockpit && !PilotView.ToCockpit() && camera.Switches == 0 && CameraStateManager.cameraMode == CameraMode.orbit,
                "No forced cockpit view while: " + blocker.Name);
        }
        Fresh(); aircraft.cockpitViewPoint = null;
        Check(PilotView.External && !PilotView.CanGoCockpit && !PilotView.ToCockpit(), "An aircraft without a cockpit viewpoint keeps its outside view, as natively");

        // ---- Spectator: let go of a followed unit ----
        Fresh(); GameManager.LocalAircraft = null; camera.followingUnit = new Unit();
        Check(PilotView.CanReleaseTarget && PilotView.ReleaseTarget() && camera.followingUnit == null &&
              CameraStateManager.cameraMode == CameraMode.free, "A spectator can release the followed unit into the native free camera");
        Check(!PilotView.CanReleaseTarget && !PilotView.ReleaseTarget(), "Nothing to release once nothing is followed");
        Fresh(); aircraft.disabled = true;
        Check(PilotView.CanReleaseTarget, "A dead player's wreck can be released like any other followed unit");
        Fresh(); GameManager.LocalAircraft = null; camera.followingUnit = new Unit(); DynamicMap.mapMaximized = true;
        Check(!PilotView.CanReleaseTarget && !PilotView.ReleaseTarget() && camera.followingUnit != null, "The map keeps the followed unit");
        Fresh(); GameManager.LocalAircraft = null; camera.followingUnit = new Unit(); XRSettings.enabled = false;
        Check(!PilotView.CanReleaseTarget, "Desktop play keeps the game's own free-camera keys");

        // ---- Runtime: flight sticks, camera hand-off, hint ----
        void Orbit() => SpectatorCameraInput.ApplyOrbit(camera.orbitState, camera);
        void Step(float lx = 0, float ly = 0, float rx = 0, float ry = 0) { Frame(lx, ly, rx, ry); ExternalFlightInput.Sample(); Orbit(); }
        Fresh(); ui.Messages.Clear();
        Frame(); Check(ExternalFlightInput.Sample() && ExternalFlightInput.Active, "Outside-view flight starts with motion controls on");
        Check(ui.Messages.Count == 3 && ui.Messages[0] == "Left stick flies, right stick looks" && ui.Messages[2] == "B returns to the cockpit",
            "Entering the outside view names the controls once in the game's feed");
        Orbit();
        Step(lx: 1, ly: -1);
        Check(ExternalFlightInput.Flying && ExternalFlightInput.Roll > 0 && ExternalFlightInput.Pitch < 0 &&
              camera.orbitState.Pan == 0 && camera.orbitState.Tilt == 20,
            "While flying from outside the left stick flies and never moves the camera");
        Step(rx: 1, ry: 1);
        var pan = camera.orbitState.Pan;
        Check(pan > 0 && camera.orbitState.Tilt > 20 && camera.orbitState.Distance == 0 && ExternalFlightInput.Pitch == 0 && ExternalFlightInput.Roll == 0,
            "The right stick orbits the camera around the aircraft and does not fly it");
        Orbit();
        Check(camera.orbitState.Pan == pan, "Several native camera callbacks in one frame move the camera once");
        Check(ui.Messages.Count == 3, "The hint is not repeated every frame");
        Step();
        TrackedControllers.Right.Trigger = 1;
        Step();
        Check(ExternalFlightInput.Rudder && ExternalFlightInput.Yaw > 0 && camera.orbitState.Pan == pan, "Right trigger is right rudder");
        TrackedControllers.Right.Trigger = 0; TrackedControllers.Left.Trigger = 1;
        Step();
        Check(ExternalFlightInput.Rudder && ExternalFlightInput.Yaw < 0, "Left trigger is left rudder");
        TrackedControllers.Left.Trigger = 0;
        Step();
        Check(!ExternalFlightInput.Rudder, "Released triggers leave the game's own yaw alone");
        TrackedControllers.Right.Grip = 1;
        Step(); Step(ry: 1);
        Check(camera.orbitState.Distance == 0 && camera.orbitState.Pan == pan, "Zoom cannot go below the native minimum distance");
        Step(ry: -1);
        Check(camera.orbitState.Distance > 0, "A grip held: the right stick changes camera distance");
        TrackedControllers.Right.Grip = 0;

        // A gamepad does not mirror: cockpit handedness only moves the way back.
        Fresh(); ModConfiguration.Instance.MotionControls.LeftHanded.Value = true;
        Step(); Step(lx: 1, rx: 1);
        Check(ExternalFlightInput.Roll > 0 && camera.orbitState.Pan > 0, "Left-handed cockpit: the left stick still flies and the right stick still looks");
        Check(ui.Messages.Any(m => m == "Y returns to the cockpit"), "Left-handed hints name the mirrored cockpit button");

        Fresh(); NativeHandOptionWheel.LeftOwned = true;
        Step(); Frame(lx: 1);
        TrackedControllers.Left.Trigger = 1;
        Check(ExternalFlightInput.Sample() && !ExternalFlightInput.Flying && !ExternalFlightInput.Rudder,
            "A hand that owns the quick wheel neither flies nor steers: its trigger pages the wheel");
        NativeHandOptionWheel.LeftOwned = false;
        Frame(lx: 1);
        Check(ExternalFlightInput.Sample() && !ExternalFlightInput.Flying && !ExternalFlightInput.Rudder,
            "Closing the wheel with stick and trigger held needs a fresh centre and release");
        TrackedControllers.Left.Trigger = 0;

        var gates = new (string Name, Action Block, Action Unblock)[]
        {
            ("motion controls off", () => ModConfiguration.Instance.MotionControls.Enabled.Value = false, () => ModConfiguration.Instance.MotionControls.Enabled.Value = true),
            ("tracking lost", () => TrackedControllers.Available = false, () => TrackedControllers.Available = true),
            ("cockpit view", () => CameraStateManager.cameraMode = CameraMode.cockpit, () => CameraStateManager.cameraMode = CameraMode.orbit),
            ("free camera", () => CameraStateManager.cameraMode = CameraMode.free, () => CameraStateManager.cameraMode = CameraMode.orbit),
            ("maximized map", () => DynamicMap.mapMaximized = true, () => DynamicMap.mapMaximized = false),
            ("game menu", () => CursorManager.Flags = CursorFlags.GameMenu, () => CursorManager.Flags = CursorFlags.None),
            ("VR menu", () => NOVR.VrUi.WristMenu.IsOpen = true, () => NOVR.VrUi.WristMenu.IsOpen = false),
            ("paused", () => Time.timeScale = 0, () => Time.timeScale = 1),
            ("aircraft destroyed", () => aircraft.disabled = true, () => aircraft.disabled = false)
        };
        foreach (var gate in gates)
        {
            Fresh(); Frame(); ExternalFlightInput.Sample();
            gate.Block(); Frame(lx: 1);
            Check(!ExternalFlightInput.Sample() && !ExternalFlightInput.Flying && !ExternalFlightInput.Rudder,
                "No thumbstick flight while: " + gate.Name);
            gate.Unblock(); Frame(lx: 1);
            Check(ExternalFlightInput.Sample() && !ExternalFlightInput.Flying, gate.Name + " ends with the stick held: a fresh centre is required");
            Frame(); ExternalFlightInput.Sample(); Frame(lx: 1);
            Check(ExternalFlightInput.Sample() && ExternalFlightInput.Flying && ExternalFlightInput.Roll > 0, gate.Name + " recovery flies again");
        }

        // Motion controls off (a physical HOTAS): the sticks keep their spectator roles.
        Fresh(); ModConfiguration.Instance.MotionControls.Enabled.Value = false;
        Frame(); Orbit(); Frame(rx: 1, ly: -1); Orbit();
        Check(!ExternalFlightInput.Active && camera.orbitState.Pan > 0 && camera.orbitState.Distance > 0,
            "With motion controls off the right stick orbits and the left stick changes distance, as for a spectator");
        ModConfiguration.Instance.MotionControls.Enabled.Value = true;

        // Losing the aircraft mid-flight hands the sticks back to the spectator camera.
        Fresh(); Step(); Step(rx: 1, lx: 1);
        pan = camera.orbitState.Pan;
        aircraft.disabled = true;
        Step(rx: 1, lx: 1);
        Check(!ExternalFlightInput.Active && camera.orbitState.Pan == pan && camera.orbitState.Distance == 0,
            "Sticks held through a crash do not swing or zoom the spectator camera");
        Step(); Step(rx: 1);
        Check(camera.orbitState.Pan > pan, "After centring, the right stick orbits the wreck as a spectator");

        // The hint is limited per session and tied to visits, not frames.
        Fresh(); ui.Messages.Clear();
        for (var visit = 0; visit < 6; visit++)
        {
            CameraStateManager.cameraMode = CameraMode.cockpit; Frame(); ExternalFlightInput.Sample();
            CameraStateManager.cameraMode = CameraMode.orbit; Frame(); ExternalFlightInput.Sample();
        }
        Check(ui.Messages.Count <= 9, "The control hint appears on at most three visits per session");
        var lines = ExternalFlightInput.HintLines("B");
        Check(lines.Length == 3 && lines.All(l => l.Length <= 44), "Hint lines stay short enough for one feed row each");

        SceneSingleton<CameraStateManager>.i = null;
        SceneSingleton<GameplayUI>.i = null;
        GameManager.LocalAircraft = null;
        return checks;
    }
}
