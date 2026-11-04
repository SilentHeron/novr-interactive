using System.Reflection;
using NOVR;
using NOVR.MotionControls;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.XR;

var checks = 0;
void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
void Near(Vector3 actual, Vector3 expected, string message) => Check((actual - expected).magnitude < .00001f, message);
void Same(HudWidgetPose actual, HudWidgetPose expected, string message)
{
    Near(actual.Position, expected.Position, message + " position");
    Near(actual.Euler, expected.Euler, message + " angles");
    Check(actual.Scale == expected.Scale && actual.Aspect == expected.Aspect && actual.Opacity == expected.Opacity &&
        actual.Background == expected.Background && actual.Visible == expected.Visible, message + " appearance");
}
void TeardownOrder()
{
    var events = Effects.Events;
    var destroyed = events.FindIndex(e => e.StartsWith("destroy:"));
    Check(destroyed >= 0 && new[] { "release-status", "release-map", "release-alerts" }
        .All(name => events.IndexOf(name) >= 0 && events.IndexOf(name) < destroyed),
        "All native source owners release before the first widget root is destroyed");
    var created = events.FindIndex(e => e.StartsWith("create:"));
    Check(created < 0 || events.FindLastIndex(e => e.StartsWith("destroy:")) < created,
        "Old roots are released before replacement widgets are created");
    Check(events.IndexOf("finish-handles") >= 0 && events.IndexOf("finish-handles") < events.IndexOf("release-status"),
        "Combined preview finishes before native source teardown");
}

foreach (var hands in new[] { 1, 2 })
    foreach (var opacity in new[] { false, true })
    {
        var f = new Fixture();
        f.Manager.Begin();
        f.ChangePreview();
        f.Manager.HoldForCheck(hands, opacity);
        var saves = ModConfiguration.Instance.Config.Saves;
        f.Manager.Begin();
        Check(CockpitHudWidgets.Editing && !f.Manager.ContactsClear, "Begin is idempotent and does not replace the cancellation snapshot");
        f.Manager.Finish(false);
        Check(!CockpitHudWidgets.Editing && f.Manager.ContactsClear && !CockpitLayout.Instance!.Active,
            "Cancel releases one/two-hand and opacity contacts and the combined handle preview");
        Same(f.Manager.PoseForCheck("map"), f.Original, "Cancel restores exact displayed pose");
        Same(f.Stored.Load(Fixture.Key, 1)["map"], f.Original, "Cancel never writes HUD preview");
        Near(CockpitProfiles.SeatOffset, Fixture.Seat, "Cancel restores seat");
        Near(CockpitProfiles.StickPosition, Fixture.Stick, "Cancel restores stick");
        Check(ModConfiguration.Instance.Config.Saves == saves, "Cancel does not persist either slot");
    }

foreach (var change in new[] { "aircraft instance", "aircraft identity", "slot" })
    foreach (var hands in new[] { 1, 2 })
    {
        var f = new Fixture();
        f.Manager.Begin();
        f.ChangePreview();
        f.Manager.HoldForCheck(hands, opacity: true);
        var appearance = f.Manager.AppearanceForCheck("map");
        Effects.Events.Clear();
        if (change == "aircraft instance")
            GameManager.Current = new Aircraft();
        else if (change == "aircraft identity")
            GameManager.Current!.definition!.jsonKey = "mod/other-aircraft";
        else
            f.Profiles.SelectSlot(2);
        f.Profiles.Refresh();
        f.Manager.RefreshForCheck();
        Check(!CockpitHudWidgets.Editing && f.Manager.ContactsClear && !CockpitLayout.Instance!.Active, change + " cancels preview");
        Check(appearance.Disposed && !appearance.SliderHeld, change + " releases the old opacity editor");
        Check(f.Manager.WidgetCount == HudWidgetProfiles.Ids.Length, change + " creates all widgets for new context");
        Check(f.Manager.ProfileSlot == CockpitProfiles.ActiveSlot && CockpitHudWidgets.CurrentAircraft == GameManager.Current,
            change + " selects matching aircraft/slot");
        Same(f.Stored.Load(Fixture.Key, 1)["map"], f.Original, change + " preserves source HUD slot");
        Same(f.Stored.Load(Fixture.Key, 2)["map"], f.Second, change + " preserves destination HUD slot");
        TeardownOrder();
    }

foreach (var cause in new[] { "readiness", "camera mode", "disable", "VR disabled", "lost cockpit" })
{
    var f = new Fixture();
    f.Manager.Begin();
    f.ChangePreview();
    f.Manager.HoldForCheck(2, opacity: true);
    Effects.Events.Clear();
    if (cause == "readiness")
        f.Manager.ReadyBoundary = false;
    if (cause == "camera mode")
        CameraStateManager.cameraMode = CameraMode.external;
    if (cause == "disable")
        f.Manager.DisableForCheck();
    else if (cause == "VR disabled" || cause == "lost cockpit")
    {
        if (cause == "VR disabled")
            XRSettings.enabled = false;
        else
            GameManager.Current!.Lost = true;
        f.Manager.RefreshForCheck();
    }
    else
        Check(!f.Manager.InputReadyForCheck(), cause + " stops editing input");
    Check(!CockpitHudWidgets.Editing && f.Manager.ContactsClear && !CockpitLayout.Instance!.Active, cause + " cancels all previews");
    Same(f.Manager.PoseForCheck("map"), f.Original, cause + " preserves displayed baseline");
    Same(f.Stored.Load(Fixture.Key, 1)["map"], f.Original, cause + " preserves persisted baseline");
    if (cause is "disable" or "VR disabled" or "lost cockpit")
    {
        Check(f.Manager.WidgetCount == 0 && CockpitHudWidgets.CurrentAircraft == null, cause + " releases widget ownership");
        TeardownOrder();
    }
    if (cause == "disable")
        Check(Application.Subscribers == 0 && f.Manager.FocusCleared, "Disable detaches before-render callback and appearance focus");
}

foreach (var change in new[] { "seat", "translation anchor", "translation calibration", "rotation calibration" })
{
    var f = new Fixture();
    f.Manager.Begin();
    f.ChangePreview();
    f.Manager.HoldForCheck(2, opacity: true);
    var preview = f.Manager.PoseForCheck("map");
    var saves = ModConfiguration.Instance.Config.Saves;
    if (change == "seat")
        f.Profiles.MoveSeat(new(.01f, 0, 0));
    if (change == "translation anchor")
        NOVRHeadsetData.TranslationAnchor = new(.1f, 0, 0);
    if (change == "translation calibration")
        NOVRHeadsetData.TranslationCalibrationOffset = new(0, .1f, 0);
    if (change == "rotation calibration")
        NOVRHeadsetData.RotationCalibrationOffset = new(0, .1f, 0, .99f);
    Check(f.Manager.InputReadyForCheck() && CockpitHudWidgets.Editing && f.Manager.ContactsClear,
        change + " releases contacts without canceling the edit session");
    Check(!f.Manager.ContinueGripForCheck(0) && !f.Manager.ContinueGripForCheck(1), change + " cannot inherit held corner grips");
    Same(f.Manager.PoseForCheck("map"), preview, change + " does not translate or rotate widget preview");
    Check(ModConfiguration.Instance.Config.Saves == saves, change + " does not save a preview");
    f.Manager.HoldForCheck(1);
    Check(f.Manager.InputReadyForCheck() && !f.Manager.ContactsClear, change + " calibration is captured once; new grip survives next frame");
    f.Manager.Finish(false);
    Same(f.Manager.PoseForCheck("map"), f.Original, change + " still cancels to original snapshot");
}

foreach (var cause in new[] { "tracking", "wrist menu", "paused", "flight input", "native wheel", "control mapper" })
{
    var f = new Fixture();
    f.Manager.Begin();
    f.ChangePreview();
    f.Manager.HoldForCheck(2, opacity: true);
    var preview = f.Manager.PoseForCheck("map");
    if (cause == "tracking")
        TrackedControllers.Available = false;
    if (cause == "wrist menu")
        WristMenu.IsOpen = true;
    if (cause == "paused")
        Time.timeScale = 0;
    if (cause == "flight input")
        GameManager.flightControlsEnabled = false;
    if (cause == "native wheel")
    {
        SceneSingleton<RadialMenuMain>.i = new();
        RadialMenuMain.InUse = true;
    }
    if (cause == "control mapper")
        GameManager.controlMapper = new()
        {
            isOpen = true
        };
    Check(!f.Manager.InputReadyForCheck() && CockpitHudWidgets.Editing && f.Manager.ContactsClear,
        cause + " suspends contacts while retaining edit session");
    Check(!f.Manager.ContinueGripForCheck(0) && !f.Manager.ContinueGripForCheck(1), cause + " requires neutral grip handoff");
    Same(f.Manager.PoseForCheck("map"), preview, cause + " retains preview unchanged");
    Check(f.Manager.WidgetCount == HudWidgetProfiles.Ids.Length, cause + " does not tear down native content slots");
}

foreach (var destination in new[] { 1, 2 })
{
    var f = new Fixture();
    f.Manager.Begin();
    f.ChangePreview();
    f.Manager.HoldForCheck(2, opacity: true);
    var preview = f.Manager.PoseForCheck("map");
    var seat = CockpitProfiles.SeatOffset;
    var stick = CockpitProfiles.StickPosition;
    var throttle = CockpitProfiles.ThrottlePosition;
    Check(!f.Manager.LoadSlot(2), "Loading a slot cannot silently discard an active preview");
    Check(f.Manager.SaveToSlot(destination), "Save accepts current preview and destination slot");
    Check(!CockpitHudWidgets.Editing && f.Manager.ContactsClear && !CockpitLayout.Instance!.Active, "Save ends combined edit and releases contacts");
    Check(f.Manager.ProfileSlot == destination && CockpitProfiles.ActiveSlot == destination, "Save loads the destination slot for both owners");
    Same(f.Manager.PoseForCheck("map"), preview, "Save redisplays committed preview");
    Same(f.Stored.Load(Fixture.Key, destination)["map"], preview, "Save writes exact HUD preview");
    Near(CockpitProfiles.SeatOffset, seat, "Save commits matching seat");
    Near(CockpitProfiles.StickPosition, stick, "Save commits matching stick");
    Near(CockpitProfiles.ThrottlePosition, throttle, "Save commits matching throttle");
    var untouched = destination == 1 ? 2 : 1;
    Same(f.Stored.Load(Fixture.Key, untouched)["map"], untouched == 1 ? f.Original : f.Second, "Save leaves other HUD slot intact");
    Check(f.Manager.LoadSlot(untouched), "Saved profile can be loaded outside editing");
    Near(CockpitProfiles.SeatOffset, untouched == 1 ? Fixture.Seat : Fixture.SecondSeat, "Save leaves other seat slot intact");
}

{
    var f = new Fixture();
    CockpitLayout.Instance!.RejectBegin = true;
    f.Manager.Begin();
    Check(!CockpitHudWidgets.Editing && !CockpitLayout.Instance.Active, "Failed handle-preview acquisition cannot enter HUD edit mode");
    CockpitLayout.Instance.RejectBegin = false;
    f.Manager.Begin();
    f.ChangePreview();
    var saves = ModConfiguration.Instance.Config.Saves;
    Check(!f.Manager.SaveToSlot(0) && !f.Manager.SaveToSlot(4) && CockpitHudWidgets.Editing, "Invalid save slots retain preview without writing");
    CockpitLayout.Instance.Stick = new(float.NaN, 0, 0);
    Check(!f.Manager.SaveToSlot(2) && CockpitHudWidgets.Editing, "Rejected handle snapshot cannot partially save the HUD");
    Check(ModConfiguration.Instance.Config.Saves == saves, "Rejected save performs no configuration writes");
    Same(f.Stored.Load(Fixture.Key, 2)["map"], f.Second, "Rejected save preserves destination");
    f.Manager.DisableForCheck();
}
{
    var f = new Fixture();
    f.Manager.Begin();
    f.ChangePreview();
    // A configuration change can happen after the early widget Update but
    // before a later UI callback. Saving must not mix two session contexts.
    f.Profiles.SelectSlot(2);
    var saves = ModConfiguration.Instance.Config.Saves;
    Check(!f.Manager.SaveToSlot(2), "A changed active slot invalidates the old edit session before saving");
    Check(ModConfiguration.Instance.Config.Saves == saves, "Stale-session save cannot write either profile");
    f.Manager.RefreshForCheck();
    Check(!CockpitHudWidgets.Editing && f.Manager.ProfileSlot == 2, "Next context refresh cancels stale preview and displays new slot");
    Same(f.Stored.Load(Fixture.Key, 1)["map"], f.Original, "Stale save leaves source slot unchanged");
    Same(f.Stored.Load(Fixture.Key, 2)["map"], f.Second, "Stale save leaves destination slot unchanged");
    Near(CockpitProfiles.SeatOffset, Fixture.SecondSeat, "Stale save leaves new slot seat unchanged");
    f.Manager.DisableForCheck();
}
Console.WriteLine($"{checks} HUD edit-session checks passed (production coordinator/profile source; no Unity scene or file I/O).");

sealed class Fixture
{
    internal const string Key = "SmallFighter1";
    internal static readonly Vector3 Seat = new(.03f, .04f, .01f), SecondSeat = new(-.03f, .08f, .02f);
    internal static readonly Vector3 Stick = new(.26f, -.32f, .31f), Throttle = new(-.25f, -.37f, .33f);
    internal readonly CockpitProfiles Profiles;
    internal readonly HudWidgetProfiles Stored;
    internal readonly CockpitHudWidgets Manager;
    internal readonly HudWidgetPose Original, Second;
    internal Fixture()
    {
        CockpitHudWidgets.Instance?.DisableForCheck();
        Application.Clear();
        ModConfiguration.Instance = new();
        GameManager.Current = new();
        GameManager.flightControlsEnabled = true;
        GameManager.controlMapper = null;
        XRSettings.enabled = TrackedControllers.Available = true;
        WristMenu.IsOpen = RadialMenuMain.InUse = false;
        SceneSingleton<RadialMenuMain>.i = null;
        CameraStateManager.cameraMode = CameraMode.cockpit;
        Time.timeScale = 1;
        NOVRHeadsetData.TranslationAnchor = NOVRHeadsetData.TranslationCalibrationOffset = default;
        NOVRHeadsetData.RotationCalibrationOffset = Quaternion.identity;
        CockpitLayout.Instance = new();
        MotionFlightController.Instance = new();
        ControllerActions.Instance = new();
        Profiles = new();
        typeof(CockpitProfiles).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Profiles, null);
        Profiles.Refresh();
        Profiles.SaveSlot(1, Seat, Stick, Throttle);
        Profiles.SaveSlot(2, SecondSeat, Stick + new Vector3(.03f, 0, 0), Throttle + new Vector3(-.03f, 0, 0));
        Stored = new(ModConfiguration.Instance.Config);
        var first = HudWidgetProfiles.FactoryDefaults;
        Original = first["map"];
        Stored.Save(Key, first, 1);
        Second = new HudWidgetPose(new(.15f, -.25f, .5f), new(5, 10, 0), .8f, .7f, .3f, false, 1.2f);
        first["map"] = Second;
        Stored.Save(Key, first, 2);
        Manager = new();
        Manager.RefreshForCheck();
        Effects.Events.Clear();
    }
    internal void ChangePreview()
    {
        Manager.PoseFromGesture("map", new HudWidgetPose(new(.3f, -.2f, .6f), new(-5, 15, 0), .8f, .7f, .6f, false, 1.2f));
        Profiles.MoveSeat(new(.02f, .01f, .03f));
        CockpitLayout.Instance!.Stick += new Vector3(.04f, 0, 0);
        CockpitLayout.Instance.Throttle += new Vector3(-.04f, 0, 0);
    }
}
