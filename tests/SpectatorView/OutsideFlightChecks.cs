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

        // ---- Stick mapping (no game state) ----
        var sticks = new ExternalFlightSticks();
        ExternalFlightMotion Read(float sx = 0, float sy = 0, float ox = 0, float oy = 0, bool throttle = false, bool distance = false,
            float dt = .02f, bool stick = true, bool other = true, bool available = true) =>
            sticks.Update(available, stick, other, sx, sy, ox, oy, throttle, distance, dt);
        Check(!Read(sx: 1, sy: -1).Flying, "A thumbstick held while entering the outside view does not fly the aircraft");
        Check(!Read(sx: 1, sy: -1).Flying, "A held thumbstick cannot arm itself over time");
        Check(Read().Flying && Read().Pitch == 0 && Read().Roll == 0, "A centred stick arms flight with neutral input");
        var pull = Read(sy: -1);
        var push = Read(sy: 1);
        Check(Near(pull.Pitch, -1) && Near(push.Pitch, 1), "Pulling the thumbstick back commands nose-up (the game's negative pitch)");
        Check(Near(Read(sx: 1).Roll, 1) && Near(Read(sx: -1).Roll, -1), "Thumbstick right rolls right");
        var half = Read(sx: .6f).Roll;
        Check(half > 0 && half < .5f, "Small deflections are finer than linear, full deflection still reaches the stop");
        Check(Read(sx: .1f, sy: -.1f).Pitch == 0 && Read(sx: .1f).Roll == 0, "Thumbstick drift does not move the aircraft");
        Check(float.IsFinite(Read(sx: float.MaxValue, sy: float.MaxValue).Roll), "Malformed finite values cannot overflow");
        sticks.Reset();
        Check(!Read(ox: 1, oy: 1).Camera.HasOrbitMotion, "The camera stick also needs a fresh centre");
        Read();
        var look = Read(ox: 1);
        Check(Near(look.Camera.YawDegrees, 1.5f) && look.Camera.PitchDegrees == 0 && look.Camera.Distance == 0 &&
              !look.Rudder && look.ThrottleStep == 0, "The other thumbstick orbits the camera and commands nothing");
        Check(Read(oy: 1).Camera.PitchDegrees < 0 && Read(oy: -1).Camera.PitchDegrees > 0,
            "Camera stick up raises the vantage, matching the spectator orbit");
        var slam = Read(oy: 1, throttle: true);
        Check(slam.ThrottleStep == 0 && !slam.Rudder && !slam.Camera.HasOrbitMotion,
            "Squeezing the throttle grip with the stick deflected cannot slam the throttle");
        Read(throttle: true);
        var power = Read(oy: 1, throttle: true);
        Check(Near(power.ThrottleStep, ExternalFlightSticks.ThrottlePerSecond * .02f) && power.Rudder && power.Yaw == 0 &&
              !power.Camera.HasOrbitMotion, "With the throttle grip held the thumbstick is throttle and rudder, not camera");
        Check(Read(ox: 1, throttle: true).Yaw > 0, "Throttle-hand stick right is right rudder");
        Check(Read(oy: -1, throttle: true).ThrottleStep < 0 && Read(ox: -1, throttle: true).Yaw < 0, "Throttle down and left rudder follow the stick");
        Check(!Read(ox: 1).Camera.HasOrbitMotion, "Releasing the throttle grip with the stick deflected cannot swing the camera");
        Read();
        Read(distance: true);
        var zoom = Read(ox: 1, oy: 1, distance: true);
        Check(zoom.Camera.Distance < 0 && zoom.Camera.YawDegrees > 0 && zoom.Camera.PitchDegrees == 0 && zoom.ThrottleStep == 0,
            "With the stick-hand grip held the camera stick moves closer or farther and still orbits");
        Read(throttle: true, distance: true);
        var both = Read(oy: 1, throttle: true, distance: true);
        Check(both.ThrottleStep > 0 && both.Camera.Distance == 0, "Both grips held: flying wins over framing");
        sticks.Reset(); Read();
        Check(!Read(sx: 1, stick: false).Flying && !Read(sx: 1).Flying, "Losing the flight hand's tracking requires a fresh centre");
        Read();
        Check(Read(ox: 1, stick: false).Camera.YawDegrees > 0, "The camera hand keeps working while the flight hand is untracked");
        Read();
        Check(Read(sx: 1, other: false).Roll > 0, "The flight hand keeps working while the camera hand is untracked");
        foreach (var bad in new[] { float.NaN, float.PositiveInfinity, 0f, -1f })
        {
            sticks.Reset(); Read();
            Check(!Read(sx: 1, dt: bad).Flying && !Read(sx: 1).Flying, "An invalid frame time resets every gate");
        }
        sticks.Reset(); Read();
        Check(!Read(sx: 1, available: false).Flying && !Read(sx: 1).Flying, "A menu or map resets every gate");
        foreach (var dt in new[] { 1f / 60, 1f / 90, 1f / 144 })
        {
            sticks.Reset(); Read(throttle: true);
            var total = 0f;
            for (var i = 0; i < (int)MathF.Round(1 / dt); i++) total += Read(oy: 1, throttle: true, dt: dt).ThrottleStep;
            Check(Math.Abs(total - ExternalFlightSticks.ThrottlePerSecond) < .002f, "Throttle rate is independent of headset refresh rate");
        }
        sticks.Reset(); Read(throttle: true);
        Check(Near(Read(oy: 1, throttle: true, dt: 10).ThrottleStep, ExternalFlightSticks.ThrottlePerSecond * .05f),
            "A stalled frame cannot jump the throttle");

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
        Fresh(); ui.Messages.Clear();
        Frame(); Check(ExternalFlightInput.Sample() && ExternalFlightInput.Active, "Outside-view flight starts with motion controls on");
        Check(ui.Messages.Count == 3 && ui.Messages[0].StartsWith("Right stick flies") && ui.Messages[2].Contains("B: cockpit"),
            "Entering the outside view names the controls once in the game's feed");
        Orbit();
        Frame(rx: 1, ry: -1); Orbit();
        Check(ExternalFlightInput.Flying && Near(ExternalFlightInput.Roll, 0) is false && ExternalFlightInput.Pitch < 0 &&
              camera.orbitState.Pan == 0 && camera.orbitState.Tilt == 20,
            "While flying from outside the right stick flies and never moves the camera");
        Frame(lx: 1, ly: 1); Orbit();
        var pan = camera.orbitState.Pan;
        Check(pan > 0 && camera.orbitState.Tilt > 20 && camera.orbitState.Distance == 0 && ExternalFlightInput.Pitch == 0,
            "The left stick orbits the camera around the aircraft");
        Orbit();
        Check(camera.orbitState.Pan == pan, "Several native camera callbacks in one frame move the camera once");
        Check(ui.Messages.Count == 3, "The hint is not repeated every frame");
        Frame(); Orbit();
        TrackedControllers.Left.Grip = 1;
        Frame(); Orbit(); Frame(lx: .9f, ly: 1); Orbit();
        var took = ExternalFlightInput.TakeThrottleStep(out var step);
        Check(camera.orbitState.Pan == pan && ExternalFlightInput.Rudder && ExternalFlightInput.Yaw > 0 &&
              took && step > 0 && !ExternalFlightInput.TakeThrottleStep(out _),
            "Left grip held: the left stick is throttle and rudder, handed out once");
        // Two rendered frames pass before the next physics step asks.
        Frame(lx: .9f, ly: 1); ExternalFlightInput.Sample();
        Frame(lx: .9f, ly: 1); ExternalFlightInput.Sample();
        Check(ExternalFlightInput.TakeThrottleStep(out var two) && Near(two, step * 2), "Throttle steps accumulate across frames without a physics step");
        TrackedControllers.Left.Grip = 0;
        TrackedControllers.Right.Grip = 1;
        Frame(); Orbit(); Frame(ly: 1); Orbit();
        Check(camera.orbitState.Distance == 0 && camera.orbitState.Pan == pan, "Zoom cannot go below the native minimum distance");
        Frame(ly: -1); Orbit();
        Check(camera.orbitState.Distance > 0, "Right grip held: the left stick changes camera distance");
        TrackedControllers.Right.Grip = 0;

        Fresh(); ModConfiguration.Instance.MotionControls.LeftHanded.Value = true;
        Frame(); ExternalFlightInput.Sample(); Orbit();
        Frame(lx: 1, rx: 1); Orbit();
        Check(ExternalFlightInput.Roll > 0 && camera.orbitState.Pan > 0, "Left-handed: the left stick flies and the right stick looks");
        Check(ui.Messages.Any(m => m.StartsWith("Left stick flies")), "Left-handed hints name the mirrored hands");

        Fresh(); NativeHandOptionWheel.RightOwned = true;
        Frame(); ExternalFlightInput.Sample(); Frame(rx: 1);
        Check(ExternalFlightInput.Sample() && !ExternalFlightInput.Flying, "A hand that owns the quick wheel does not fly");
        NativeHandOptionWheel.RightOwned = false;
        Frame(rx: 1);
        Check(ExternalFlightInput.Sample() && !ExternalFlightInput.Flying, "Closing the wheel with the stick deflected needs a fresh centre");

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
            gate.Block(); Frame(rx: 1);
            Check(!ExternalFlightInput.Sample() && !ExternalFlightInput.Flying && !ExternalFlightInput.Rudder,
                "No thumbstick flight while: " + gate.Name);
            gate.Unblock(); Frame(rx: 1);
            Check(ExternalFlightInput.Sample() && !ExternalFlightInput.Flying, gate.Name + " ends with the stick held: a fresh centre is required");
            Frame(); ExternalFlightInput.Sample(); Frame(rx: 1);
            Check(ExternalFlightInput.Sample() && ExternalFlightInput.Flying && ExternalFlightInput.Roll > 0, gate.Name + " recovery flies again");
        }

        // Motion controls off (a physical HOTAS): the sticks keep their spectator roles.
        Fresh(); ModConfiguration.Instance.MotionControls.Enabled.Value = false;
        Frame(); Orbit(); Frame(rx: 1); Orbit();
        Check(!ExternalFlightInput.Active && camera.orbitState.Pan > 0, "With motion controls off the right stick still orbits the outside view");
        ModConfiguration.Instance.MotionControls.Enabled.Value = true;

        // Losing the aircraft mid-flight hands the sticks back to the spectator camera.
        Fresh(); Frame(); ExternalFlightInput.Sample(); Orbit();
        Frame(rx: 1); Orbit();
        aircraft.disabled = true;
        Frame(rx: 1); Orbit();
        Check(!ExternalFlightInput.Active && camera.orbitState.Pan == 0, "A flight deflection held through a crash does not swing the spectator camera");
        Frame(); Orbit(); Frame(rx: 1); Orbit();
        Check(camera.orbitState.Pan > 0, "After centring, the right stick orbits the wreck as a spectator");

        // The hint is limited per session and tied to visits, not frames.
        Fresh(); ui.Messages.Clear();
        for (var visit = 0; visit < 6; visit++)
        {
            CameraStateManager.cameraMode = CameraMode.cockpit; Frame(); ExternalFlightInput.Sample();
            CameraStateManager.cameraMode = CameraMode.orbit; Frame(); ExternalFlightInput.Sample();
        }
        Check(ui.Messages.Count <= 9, "The control hint appears on at most three visits per session");
        DynamicMap.mapMaximized = true; Frame(); ExternalFlightInput.Sample();
        DynamicMap.mapMaximized = false; Frame(); ExternalFlightInput.Sample();
        var lines = ExternalFlightInput.HintLines(false, "B");
        Check(lines.Length == 3 && lines.All(l => l.Length <= 44), "Hint lines stay short enough for one feed row each");

        SceneSingleton<CameraStateManager>.i = null;
        SceneSingleton<GameplayUI>.i = null;
        GameManager.LocalAircraft = null;
        return checks;
    }
}
