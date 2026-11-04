using System.Reflection;
using NOVR;
using NOVR.MotionControls;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class LayoutChecks
{
    internal static void Run(Action<bool> truth)
    {
        void Check(bool value, string message)
        {
            if (!value)
                throw new InvalidOperationException(message);
            truth(value);
        }
        foreach (var enabled in new[] { false, true })
            foreach (var save in new[] { false, true })
            {
                using var f = new Fixture(enabled);
                Check(f.Layout.BeginUnified() && CockpitLayout.Unified && f.Layout.CanSaveUnified, "Unified edit remains available in either flight-control mode");
                Check(f.Mode.Value == enabled && f.Mode.Writes == 0, "Beginning layout must not change the motion-control preference");
                Check(CockpitLayout.StickPreview == f.Stick && CockpitLayout.ThrottlePreview == f.Throttle, "Begin preserves saved handle placements");
                Check(CockpitLayout.CanGrabHand(true, new(true, f.Stick, 0)) == enabled &&
                    CockpitLayout.CanGrabHand(false, new(true, f.Throttle, 0)) == enabled, "Only visible enabled handles compete with widgets for either hand");
                if (!enabled)
                {
                    f.Layout.BringWithinReach();
                    Check(CockpitLayout.StickPreview == f.Stick && CockpitLayout.ThrottlePreview == f.Throttle, "Recovering HUD reach must not relocate disabled handles");
                }
                f.Input(0);
                Check(CockpitLayout.Hovered(CockpitLayout.Element.Stick) == enabled &&
                    CockpitLayout.Hovered(CockpitLayout.Element.Throttle) == enabled, "Hover is absent for disabled handles");
                f.Input(1);
                Check(CockpitLayout.HeldByHand(true) == (enabled ? CockpitLayout.Element.Stick : CockpitLayout.Element.None) &&
                    CockpitLayout.HeldByHand(false) == (enabled ? CockpitLayout.Element.Throttle : CockpitLayout.Element.None), "Disabled handles cannot acquire either grip");
                f.Input(1, Fixture.Delta);
                var offset = enabled ? Fixture.Delta : Vector3.zero;
                Check(CockpitLayout.StickPreview == f.Stick + offset && CockpitLayout.ThrottlePreview == f.Throttle + offset,
                    "Enabled grips move the real preview; disabled grips leave it unchanged");
                Check(InteractionHaptics.Grabs == (enabled ? 2 : 0), "Invisible handles produce no grab feedback");
                f.Layout.FinishUnified(save);
                Check(!CockpitLayout.Active && !CockpitLayout.Held(CockpitLayout.Element.Stick), "Finish releases the edit session and contacts");
                Check(f.Mode.Value == enabled && f.Mode.Writes == 0, "Save/cancel must preserve the user's motion-control preference");
                var savedOffset = save && enabled ? Fixture.Delta : Vector3.zero;
                f.Profiles.Refresh();
                Check(CockpitProfiles.StickPosition == f.Stick + savedOffset && CockpitProfiles.ThrottlePosition == f.Throttle + savedOffset,
                    "Save persists enabled placement edits; cancel and disabled edits retain stored positions");
            }
        Console.WriteLine("PASS production layout begin/save/cancel preserves off/on; hidden handles never hover, grab, move or steal widget contacts");

        foreach (var enabled in new[] { false, true })
            foreach (var context in new[] { "aircraft", "no aircraft", "external camera", "detached cockpit", "handedness", "disable" })
            {
                using var f = new Fixture(enabled);
                f.Layout.BeginUnified();
                f.Input(0);
                f.Input(1);
                f.Input(1, Fixture.Delta);
                switch (context)
                {
                    case "aircraft":
                        GameManager.Current = new Aircraft { definition = new() { jsonKey = "OtherPlane" } };
                        break;
                    case "no aircraft":
                        GameManager.Current = null;
                        break;
                    case "external camera":
                        CameraStateManager.cameraMode = CameraMode.external;
                        break;
                    case "detached cockpit":
                        f.Aircraft.cockpit!.Detached = true;
                        break;
                    case "handedness":
                        ModConfiguration.Instance.MotionControls.LeftHanded.Value = true;
                        break;
                    case "disable":
                        Invoke(f.Layout, "OnDisable");
                        break;
                }
                if (context != "disable")
                    f.Tick();
                Check(!CockpitLayout.Active && CockpitLayout.HeldByHand(true) == CockpitLayout.Element.None,
                    context + " cancels the handle preview and releases ownership");
                Check(f.Mode.Value == enabled && f.Mode.Writes == 0, context + " cannot change the flight-control mode");
                GameManager.Current = f.Aircraft;
                CameraStateManager.cameraMode = CameraMode.cockpit;
                f.Aircraft.cockpit!.Detached = false;
                ModConfiguration.Instance.MotionControls.LeftHanded.Value = false;
                f.Profiles.Refresh();
                Check(CockpitProfiles.StickPosition == f.Stick && CockpitProfiles.ThrottlePosition == f.Throttle,
                    context + " must not commit discarded placement edits");
            }
        Console.WriteLine("PASS production layout context loss and component disable cancel edits without changing flight-control mode or saved positions");

        foreach (var initialMode in new[] { false, true })
            foreach (var save in new[] { false, true })
            {
                using var f = new Fixture(initialMode);
                f.Layout.BeginUnified();
                f.Input(0);
                f.Input(1);
                f.Mode.Value = !initialMode;
                Check(CockpitLayout.CanGrabHand(true, new(true, f.Stick, 1)) == !initialMode,
                    "Grip competition changes immediately with the explicit control setting");
                if (initialMode)
                    Check(!CockpitLayout.Held(CockpitLayout.Element.Stick) && !CockpitLayout.Hovered(CockpitLayout.Element.Stick),
                        "Turning controls off immediately stops exposing the old handle contact");
                f.Input(1, Fixture.Delta);
                Check(CockpitLayout.HeldByHand(true) == CockpitLayout.Element.None && CockpitLayout.StickPreview == f.Stick,
                    "Changing mode cannot turn an already squeezed grip into a new drag or continue an invisible drag");
                if (!initialMode)
                {
                    f.Input(0);
                    f.Input(1);
                    f.Input(1, Fixture.Delta);
                    Check(CockpitLayout.StickPreview == f.Stick + Fixture.Delta, "Newly enabled handles work after a fresh neutral/press cycle");
                }
                else
                {
                    f.Layout.BringWithinReach();
                    Check(CockpitLayout.StickPreview == f.Stick && CockpitLayout.ThrottlePreview == f.Throttle,
                        "Recovery cannot relocate handles after they are disabled mid-edit");
                }
                f.Layout.FinishUnified(save);
                Check(f.Mode.Value == !initialMode && f.Mode.Writes == 1, "Save/cancel must retain a deliberate mode change made during editing");
            }
        Console.WriteLine("PASS deliberate off/on changes during editing survive save/cancel and require a fresh grip");

        using (var f = new Fixture(false))
        {
            f.Layout.BeginUnified();
            Check(f.Profiles.SaveSlot(2, CockpitProfiles.SeatOffset, CockpitLayout.StickPreview!.Value, CockpitLayout.ThrottlePreview!.Value),
                "HUD save-as can preserve disabled handle positions in another aircraft slot");
            f.Layout.FinishUnified(false);
            Check(f.Profiles.SelectSlot(2) && CockpitProfiles.StickPosition == f.Stick && CockpitProfiles.ThrottlePosition == f.Throttle,
                "Saved disabled handle placements load unchanged");
            Check(!f.Mode.Value && f.Mode.Writes == 0, "Profile save-as does not enable motion controls");
        }
        foreach (var enabled in new[] { false, true })
        {
            using var f = new Fixture(enabled);
            f.Layout.Begin(); // Legacy handle-only entry, without a HUD coordinator.
            Check(CockpitLayout.Active && !CockpitLayout.Unified && f.Mode.Writes == 0, "Legacy entry also preserves the control preference");
            f.Layout.Finish(false);
            Check(f.Mode.Value == enabled && f.Mode.Writes == 0, "Legacy cancellation does not enable or restore a different mode");
        }
        Console.WriteLine("PASS disabled handle positions survive profile save-as; legacy entry preserves the same mode contract");
    }

    private static void Invoke(object instance, string method) =>
        instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, null);

    private sealed class Fixture : IDisposable
    {
        internal static readonly Vector3 Delta = new(.035f, .025f, .045f);
        internal readonly Vector3 Stick = new(.31f, -.27f, .43f), Throttle = new(-.29f, -.34f, .38f);
        internal readonly Aircraft Aircraft = new();
        internal readonly CockpitProfiles Profiles = new();
        internal readonly CockpitLayout Layout = new();
        internal BepInEx.Configuration.ConfigEntry<bool> Mode => ModConfiguration.Instance.MotionControls.Enabled;
        internal Fixture(bool enabled)
        {
            ModConfiguration.Instance = new();
            Mode.Value = enabled;
            Mode.Writes = 0;
            GameManager.Current = Aircraft;
            GameManager.flightControlsEnabled = true;
            GameManager.controlMapper = null;
            CameraStateManager.cameraMode = CameraMode.cockpit;
            WristMenu.IsOpen = DynamicMap.mapMaximized = CursorManager.GameMenu = RadialMenuMain.InUse = false;
            SceneSingleton<RadialMenuMain>.i = null;
            CockpitHudWidgets.Instance = null;
            CockpitHudWidgets.Editing = CockpitHudWidgets.LeftBusy = CockpitHudWidgets.RightBusy = false;
            TrackedControllers.Available = true;
            TrackedControllers.Left = TrackedControllers.Right = default;
            Time.timeScale = 1;
            NOVRHeadsetData.TranslationAnchor = NOVRHeadsetData.TranslationCalibrationOffset = Vector3.zero;
            NOVRHeadsetData.RotationCalibrationOffset = Quaternion.identity;
            MotionFlightController.Instance = new();
            ControllerActions.Instance = new();
            InteractionHaptics.Grabs = 0;
            Invoke(Profiles, "Awake");
            Profiles.Refresh();
            Profiles.SaveHandles(Stick, Throttle);
            Invoke(Layout, "Awake");
        }
        internal void Input(float grip, Vector3 delta = default)
        {
            TrackedControllers.Left = new(true, Stick + delta, grip);
            TrackedControllers.Right = new(true, Throttle + delta, grip);
            Tick();
        }
        internal void Tick() => Invoke(Layout, "Update");
        public void Dispose()
        {
            Invoke(Layout, "OnDestroy");
            Invoke(Profiles, "OnDestroy");
        }
    }
}
