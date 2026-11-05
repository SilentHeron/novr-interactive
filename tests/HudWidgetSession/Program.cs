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
        actual.Background == expected.Background && actual.Visible == expected.Visible && actual.TextScale == expected.TextScale, message + " appearance");
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

foreach (var save in new[] { false, true })
    foreach (var id in new[] { "feed", "objectives" })
    {
        var f = new Fixture();
        var original = f.Manager.PoseForCheck(id);
        f.Manager.Begin();
        var preview = original;
        preview.TextScale = id == "feed" ? .78f : 1.35f;
        preview.Anchor = HudWidgetAnchor.LeftWrist;
        preview.WristPosition = new(.03f, .02f, .05f);
        f.Manager.PoseFromGesture(id, preview);
        f.Manager.HoldForCheck(1, opacity: true);
        if (save)
        {
            Check(f.Manager.SaveToSlot(2), "Text-size edit saves to selected aircraft slot");
            Same(f.Manager.PoseForCheck(id), preview, "Saved text size is immediately displayed");
            var stored = f.Stored.Load(Fixture.Key, 2)[id];
            Same(stored, preview, "Saving commits text size together with appearance");
            Check(stored.Anchor == preview.Anchor && stored.WristPosition.Equals(preview.WristPosition),
                "Text-size save retains attached wrist placement");
            Same(f.Stored.Load(Fixture.Key, 1)[id], original, "Text-size save leaves source slot untouched");
        }
        else
        {
            f.Manager.Finish(false);
            Same(f.Manager.PoseForCheck(id), original, "Cancel restores text size with original pose");
            Same(f.Stored.Load(Fixture.Key, 1)[id], original, "Canceled text size never persists");
        }
        Check(!CockpitHudWidgets.Editing && f.Manager.ContactsClear, "Text-size save/cancel releases editing contacts");
        f.Manager.DisableForCheck();
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
foreach (var slot in new[] { 1, 2, 3 })
{
    var f = new Fixture();
    if (slot == 3)
        f.Stored.Save(Fixture.Key, HudWidgetProfiles.FactoryDefaults, 3);
    var section = "VR HUD Widgets " + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(Fixture.Key))).Substring(0, 32) + (slot == 1 ? "" : " Slot " + slot);
    // Old installations have no auxiliary key. Empty is its binding default,
    // representing only the newly introduced frame, never the saved layout.
    ModConfiguration.Instance.Config.Bind(section, "auxiliary", "", "").Value = "";
    f.Profiles.SelectSlot(slot);
    f.Manager.DisableForCheck();
    var manager = new CockpitHudWidgets();
    manager.RefreshForCheck();
    var oldPoses = f.Stored.Load(Fixture.Key, slot);
    Check(!manager.Available && manager.PlacementReadyForCheck("map") && !manager.PlacementReadyForCheck("auxiliary"),
        "Existing panels remain placed while only the new frame waits for a tracked head");
    manager.Begin();
    Check(!CockpitHudWidgets.Editing, "Edit cannot save an uninitialized auxiliary frame at the cockpit origin");
    var saves = ModConfiguration.Instance.Config.Saves;
    manager.InitializeForCheck(false, Vector3.zero, 1, 1);
    manager.InitializeForCheck(false, Vector3.zero, 9, 2);
    Check(ModConfiguration.Instance.Config.Saves == saves && !manager.PlacementReadyForCheck("auxiliary"),
        "Missing headset tracking never initializes or persists the new frame");
    var head = new Vector3(.2f, .4f, -.3f);
    manager.InitializeForCheck(true, head, 10, 3);
    manager.InitializeForCheck(true, head, 10.1f, 4);
    Check(!manager.Available, "New frame honors the same settled-head startup gate as a fresh layout");
    manager.InitializeForCheck(true, head, 10.2f, 5);
    Check(manager.Available && manager.PlacementReadyForCheck("auxiliary"), "Tracked placement enables the new frame and editing");
    Check(ModConfiguration.Instance.Config.Saves == saves + 1, "Migration persists only once after actual headset tracking settles");
    var auxiliary = HudWidgetDefaultPlacement.AtHead(head)["auxiliary"];
    Same(manager.PoseForCheck("auxiliary"), auxiliary, "Auxiliary is centered around the seated user");
    foreach (var id in HudWidgetProfiles.Ids.Where(id => id != "auxiliary"))
    {
        Same(manager.PoseForCheck(id), oldPoses[id], "New frame cannot move displayed saved panel " + id);
        Same(f.Stored.Load(Fixture.Key, slot)[id], oldPoses[id], "New frame cannot rewrite persisted panel " + id);
    }
    manager.InitializeForCheck(true, new Vector3(-1, 1, 1), 20, 10);
    manager.Begin();
    Check(CockpitHudWidgets.Editing, "Editor is available after new frame initialization");
    Same(manager.PoseForCheck("auxiliary"), auxiliary, "Opening edit keeps initialized frame at its saved position");
    manager.Finish(false);
    GameManager.Current = new Aircraft();
    f.Profiles.Refresh();
    manager.RefreshForCheck();
    Check(manager.Available && manager.PlacementReadyForCheck("auxiliary"), "Respawn reuses migrated slot without another head-dependent placement");
    Same(manager.PoseForCheck("auxiliary"), auxiliary, "Respawn keeps the new frame aircraft-fixed");
    Check(ModConfiguration.Instance.Config.Saves == saves + 1, "Edit/respawn cannot repeat migration saves");
    manager.DisableForCheck();
}
{
    var f = new Fixture();
    f.Stored.ResetAircraft(Fixture.Key, 3);
    f.Profiles.SelectSlot(3);
    f.Manager.RefreshForCheck();
    var saves = ModConfiguration.Instance.Config.Saves;
    var head = new Vector3(-.2f, .3f, .1f);
    f.Manager.InitializeForCheck(true, head, 30, 10);
    f.Manager.InitializeForCheck(true, head, 30.1f, 11);
    f.Manager.InitializeForCheck(true, head, 30.2f, 12);
    var expected = HudWidgetDefaultPlacement.AtHead(head);
    foreach (var id in HudWidgetProfiles.Ids)
        Same(f.Manager.PoseForCheck(id), expected[id], "Fresh unsaved slot centers every widget including auxiliary: " + id);
    Check(f.Manager.Available && !f.Stored.HasSavedLayout(Fixture.Key, 3) && ModConfiguration.Instance.Config.Saves == saves,
        "Fresh default placement remains an unsaved layout until the user saves it");
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
