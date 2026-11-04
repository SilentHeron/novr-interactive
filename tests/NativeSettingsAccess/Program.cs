using NOVR.VrUi.Native;
using UnityEngine;

var checks = 0;
void Check(bool value, string description)
{
    if (!value)
        throw new InvalidOperationException(description + ": " + string.Join(", ", NativeCalls.Events));
    checks++;
}
void Order(params string[] expected) => Check(NativeCalls.Events.SequenceEqual(expected), "Native access operation order");

NativeCalls.Reset();
SceneSingleton<HUDOptions>.i = new HUDOptions();
var notifications = 0;
PlayerSettings.OnApplyOptions = () => notifications++;
NativePlayerSettingsAccess.SaveAndApply();
Order("save", "apply-player", "apply-hud");
Check(notifications == 1, "APPLY/BACK sends one native options event rather than resetting camera/HUD state twice");
NativeCalls.Reset();
NativePlayerSettingsAccess.SaveAndApply();
Order("save", "apply-player");
Check(NativeCalls.Warnings.Count == 0, "Absent HUD singleton is allowed in menu scenes");

foreach (var apply in new[] { false, true })
{
    NativeCalls.Reset();
    var assigned = false;
    NativePlayerSettingsAccess.SetPlayerBool("ChatEnabled", true, value =>
    {
        assigned = value;
        NativeCalls.Events.Add("assign");
    }, apply);
    Check(assigned && Equals(PlayerPrefs.Values["ChatEnabled"], 1), "Native bool assignment and persisted integer agree");
    Order(apply ? new[] { "assign", "write-int:ChatEnabled", "apply-player" } : new[] { "assign", "write-int:ChatEnabled" });
    NativeCalls.Reset();
    NativePlayerSettingsAccess.SetPlayerBool("ChatEnabled", false, _ => { }, apply);
    Check(Equals(PlayerPrefs.Values["ChatEnabled"], 0), "False remains integer zero");

    NativeCalls.Reset();
    var assignedFloat = 0f;
    NativePlayerSettingsAccess.SetPlayerFloat("DefaultFoV", 53.25f, value =>
    {
        assignedFloat = value;
        NativeCalls.Events.Add("assign");
    }, apply);
    Check(assignedFloat == 53.25f && Equals(PlayerPrefs.Values["DefaultFoV"], 53.25f), "Float wrapper preserves exact value and key");
    Order(apply ? new[] { "assign", "write-float:DefaultFoV", "apply-player" } : new[] { "assign", "write-float:DefaultFoV" });

    NativeCalls.Reset();
    var assignedInt = 0;
    NativePlayerSettingsAccess.SetPlayerInt("ChatTtsSpeed", -3, value =>
    {
        assignedInt = value;
        NativeCalls.Events.Add("assign");
    }, apply);
    Check(assignedInt == -3 && Equals(PlayerPrefs.Values["ChatTtsSpeed"], -3), "Integer wrapper preserves signed value and key");
    Order(apply ? new[] { "assign", "write-int:ChatTtsSpeed", "apply-player" } : new[] { "assign", "write-int:ChatTtsSpeed" });
}

foreach (var hudPresent in new[] { false, true })
{
    NativeCalls.Reset();
    if (hudPresent)
        SceneSingleton<HUDOptions>.i = new HUDOptions();
    NativePlayerSettingsAccess.SetHudBool("HUDWeapons", true, _ => NativeCalls.Events.Add("assign"));
    Order(hudPresent ? new[] { "assign", "write-int:HUDWeapons", "apply-player", "apply-hud" } : new[] { "assign", "write-int:HUDWeapons", "apply-player" });
    Check(Equals(PlayerPrefs.Values["HUDWeapons"], 1), "HUD toggle persists before notifying player and optional HUD");
    NativeCalls.Events.Clear();
    NativePlayerSettingsAccess.SetHudFloat("HMDWidth", 1234.5f, _ => NativeCalls.Events.Add("assign"));
    Order(hudPresent ? new[] { "assign", "write-float:HMDWidth", "apply-player", "apply-hud" } : new[] { "assign", "write-float:HMDWidth", "apply-player" });
    Check(Equals(PlayerPrefs.Values["HMDWidth"], 1234.5f), "HUD float retains exact native preference value");
}

NativeCalls.Reset();
NativePlayerSettingsAccess.SetUnitSystem(1);
Check(PlayerSettings.unitSystem == PlayerSettings.UnitSystem.Imperial && Equals(PlayerPrefs.Values["UnitSystem"], 1), "Units assignment retains the native enum and preference identity");
Order("write-int:UnitSystem", "apply-player");
foreach (var value in new[] { true, false })
{
    NativeCalls.Reset();
    NativePlayerSettingsAccess.SetCinematicMode(value);
    Check(PlayerSettings.cinematicMode == value && Equals(PlayerPrefs.Values["CinematicMode"], value ? 1 : 0), "Cinematic mode retains native field and preference key");
    Order("write-int:CinematicMode");
    NativeCalls.Events.Clear();
    NativePlayerSettingsAccess.SetDebugVisuals(value);
    Check(PlayerSettings.debugVis == value && Equals(PlayerPrefs.Values["DebugVis"], value ? 1 : 0), "Debug mode retains native field and preference key");
    Order("write-int:DebugVis");
}
NativeCalls.Reset();
NativePlayerSettingsAccess.ReloadPlayerSettings();
Order("load");
NativeCalls.Events.Clear();
NativePlayerSettingsAccess.Save();
Order("save");
Check(NativeCalls.Warnings.Count == 0, "Normal reload/save introduces no other apply or warning");

NativeCalls.Reset();
PlayerSettings.FailLoad = true;
NativePlayerSettingsAccess.ReloadPlayerSettings();
Check(NativeCalls.Warnings.Count == 1 && NativeCalls.Warnings[0].Contains("reload PlayerSettings"), "Native load failure stays caught with its existing warning context");
Order("load");
NativeCalls.Reset();
PlayerSettings.FailApply = true;
NativePlayerSettingsAccess.SetPlayerInt("ChatTtsVolume", 35, _ => { }, apply: true);
Check(Equals(PlayerPrefs.Values["ChatTtsVolume"], 35) && NativeCalls.Warnings.Single().Contains("apply PlayerSettings"), "Apply failure retains assigned preference and existing warning context");
Order("write-int:ChatTtsVolume", "apply-player");
NativeCalls.Reset();
SceneSingleton<HUDOptions>.i = new HUDOptions { FailApply = true };
NativePlayerSettingsAccess.SetHudBool("Gauges", false, _ => { });
Check(Equals(PlayerPrefs.Values["Gauges"], 0) && NativeCalls.Warnings.Single().Contains("apply HUD settings"), "HUD notification failure does not undo the saved field");
Order("write-int:Gauges", "apply-player", "apply-hud");
NativeCalls.Reset();
SceneSingleton<HUDOptions>.i = new HUDOptions();
PlayerSettings.FailApply = true;
NativePlayerSettingsAccess.SaveAndApply();
Order("save", "apply-player");
Check(NativeCalls.Warnings.Count == 1, "Failed APPLY has no duplicate retry or duplicate warning");

NativeCalls.Reset();
var assignmentThrew = false;
try
{
    NativePlayerSettingsAccess.SetPlayerFloat("DefaultFoV", 60, _ => throw new InvalidOperationException("assignment failed"), apply: true);
}
catch (InvalidOperationException) { assignmentThrew = true; }
Check(assignmentThrew && PlayerPrefs.Values.Count == 0 && NativeCalls.Events.Count == 0, "Failed field assignment never persists or applies a value");
NativeCalls.Reset();
PlayerPrefs.FailSave = true;
var saveThrew = false;
try
{
    NativePlayerSettingsAccess.SaveAndApply();
}
catch (IOException) { saveThrew = true; }
Check(saveThrew, "Preference save errors retain their existing propagation behavior");
Order("save");
Console.WriteLine($"Native settings access checks passed ({checks} assertions).");
