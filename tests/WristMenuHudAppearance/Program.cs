using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;

var checks = 0;
var manager = new CockpitHudWidgets();
CockpitHudWidgets.Instance = manager;
var menu = new WristMenu();
menu.Build();
menu.Appearance();
Check(menu.ButtonsHavePageParents, "Each extracted page builder attaches its buttons to its explicit page");
Check(menu.ScalarPage("Backplate opacity") == "HUD WIDGET" && menu.ScalarPage("Pitch") == "WIDGET ANGLE",
    "Appearance and angle sliders retain their own page after all later pages have been built");
Check(menu.Backplate.minValue == 0 && menu.Backplate.maxValue == 100 && !menu.Backplate.wholeNumbers,
    "Backplate slider spans clear through solid without rounding saved values during initialization");
Check(manager.Begins == 0 && !CockpitHudWidgets.Editing, "Appearance does not open placement guides");

for (var i = 0; i < HudWidgetProfiles.Ids.Length; i++)
{
    var id = HudWidgetProfiles.Ids[i];
    menu.Appearance();
    menu.Click("WIDGET APPEARANCE", id);
    menu.Refresh();
    var writes = manager.AppearanceSaves;
    Check(menu.Backplate.value == 0 && manager.AppearanceSaves == writes, "Selection only reads saved backplate: " + id);
    menu.Backplate.value = (i + 1) * 10;
    Check(Near(manager.GetPose(id).Background, (i + 1) * .1f), "Slider changes selected backplate: " + id);
    Check(manager.GetPose(id).Opacity == 1 && manager.GetPose(id).Scale == 1 && manager.GetPose(id).Visible,
        "Slider leaves readouts, size and visibility unchanged: " + id);
}
for (var i = 0; i < HudWidgetProfiles.Ids.Length; i++)
{
    var id = HudWidgetProfiles.Ids[i];
    var writes = manager.AppearanceSaves;
    menu.Appearance();
    menu.Click("WIDGET APPEARANCE", id);
    menu.Refresh();
    Check(Near(menu.Backplate.value, (i + 1) * 10) && manager.AppearanceSaves == writes,
        "Each widget retains its own value without selection writes: " + id);
}
menu.Appearance();
menu.Click("WIDGET APPEARANCE", "weapons");
menu.Refresh();
menu.Backplate.value = 100;
Check(manager.GetPose("weapons").Background == 1 && menu.BackplateLabel.text == "100%", "Solid endpoint and readout");
menu.Backplate.value = 0;
Check(manager.GetPose("weapons").Background == 0 && manager.GetPose("weapons").Opacity == 1, "Clear backplate leaves labels fully visible");
menu.Backplate.value = 67;
menu.Scalar("Readout opacity").value = .9f;
menu.Refresh();
Check(Near(manager.GetPose("weapons").Opacity, .9f) && Near(manager.GetPose("weapons").Background, .67f), "Readout controls leave backplate unchanged");
var exact = manager.GetPose("weapons");
exact.Background = .333333f;
manager.SetAppearance("weapons", exact);
var saves = manager.AppearanceSaves;
menu.Refresh();
Check(Near(menu.Backplate.value, exact.Background * 100) && manager.GetPose("weapons").Background == exact.Background && manager.AppearanceSaves == saves,
    "Opening an existing fractional value never quantizes or saves it");
manager.Available = false;
menu.Refresh();
menu.Backplate.value = 89;
Check(!menu.Backplate.interactable && manager.AppearanceSaves == saves, "Unavailable cockpit disables and rejects changes");
manager.Available = true;
manager.Begin();
menu.Refresh();
menu.Backplate.value = 45;
Check(CockpitHudWidgets.Editing && manager.Begins == 1 && Near(manager.GetPose("weapons").Background, .45f), "Editing reuses the existing preview without starting another");
Check(!menu.Buttons.Keys.Any(k => k.text == "Less background" || k.text == "More background"), "Slider replaces the old increment buttons");

manager.Finish(false);
menu.Refresh();
var begins = manager.Begins;
Check(!menu.Enabled("HUD WIDGET", "Angle"), "Angle page is disabled outside layout preview");
menu.Click("HUD WIDGET", "Angle");
Check(menu.ActivePage == "HUD WIDGET" && manager.Begins == begins, "Angle does not silently start layout editing");
menu.ForceNudge("Pitch", 5);
menu.ForceCallback("WIDGET ANGLE", "Face forward");
Check(manager.AngleWrites == 0, "Late angle callbacks cannot mutate a saved layout outside preview");
manager.Begin();
menu.Refresh();
menu.Click("HUD WIDGET", "Angle");
Check(menu.ActivePage == "WIDGET ANGLE" && menu.Scalar("Pitch").interactable, "Angle page is available during layout editing");
foreach (var id in HudWidgetProfiles.Ids)
{
    menu.Appearance();
    menu.Click("WIDGET APPEARANCE", id);
    menu.Refresh();
    menu.Click("HUD WIDGET", "Angle");
    var original = manager.GetPose(id);
    var appearanceWrites = manager.AppearanceSaves;
    menu.Nudge("Pitch", 5);
    Check(manager.GetPose(id).Euler == new UnityEngine.Vector3(5, 0, 0), "Pitch increases by five degrees for selected " + id);
    menu.Nudge("Yaw", -5);
    Check(manager.GetPose(id).Euler == new UnityEngine.Vector3(5, -5, 0), "Yaw changes without resetting pitch for selected " + id);
    Check(menu.AngleInfo.text.Contains("Pitch 5° · Yaw -5°") && menu.AngleInfo.text.Contains("Roll stays level"), "Angle readout identifies values and locked roll: " + id);
    var angled = manager.GetPose(id);
    Check(angled.Position == original.Position && angled.Scale == original.Scale && angled.Opacity == original.Opacity &&
        angled.Background == original.Background && angled.Visible == original.Visible && manager.AppearanceSaves == appearanceWrites,
        "Angle callback changes no position or appearance fields: " + id);
    Check(HudWidgetProfiles.Ids.Where(other => other != id).All(other => manager.GetPose(other).Euler == UnityEngine.Vector3.zero),
        "Angle callback targets only selected widget: " + id);
    menu.Nudge("Pitch", -5);
    menu.Nudge("Yaw", 5);
    Check(manager.GetPose(id).Euler == UnityEngine.Vector3.zero, "Opposite steps restore original orientation: " + id);
}
menu.Nudge("Pitch", -5);
menu.Nudge("Yaw", 5);
menu.Click("WIDGET ANGLE", "Face forward");
Check(manager.GetPose(manager.SelectedId).Euler == UnityEngine.Vector3.zero, "Face forward resets pitch and yaw together");
manager.Available = false;
menu.Refresh();
var writesBeforeUnavailable = manager.AngleWrites;
Check(!menu.Scalar("Yaw").interactable && !menu.Enabled("WIDGET ANGLE", "Face forward"), "Unavailable aircraft disables all angle editing");
menu.ForceNudge("Yaw", 5);
menu.ForceCallback("WIDGET ANGLE", "Face forward");
Check(manager.AngleWrites == writesBeforeUnavailable, "Unavailable late callbacks do not edit a layout");
manager.Available = true;
menu.Refresh();
var closes = WristMenu.Closes;
begins = manager.Begins;
menu.Click("WIDGET ANGLE", "Continue moving");
Check(WristMenu.Closes == closes + 1 && manager.Begins == begins && CockpitHudWidgets.Editing, "Continue moving closes menu without replacing the active preview");
menu.Nudge("Pitch", 5);
menu.Click("LAYOUT", "Cancel");
menu.Refresh();
Check(!CockpitHudWidgets.Editing && manager.GetPose(manager.SelectedId).Euler == UnityEngine.Vector3.zero,
    "Angle controls use the same layout cancellation callback");
Check(!menu.Scalar("Pitch").interactable, "Cancelled preview disables angle controls");
manager.Begin();
menu.Refresh();
menu.Nudge("Yaw", 5);
menu.Click("LAYOUT", "Save FS-20 Revoker\nSlot 1");
Check(!CockpitHudWidgets.Editing && manager.GetPose(manager.SelectedId).Euler.y == 5, "Angle controls use the same layout save callback");
menu.Appearance();
menu.Click("WIDGET APPEARANCE", "weapons");
menu.Refresh();
Check(manager.AppearanceFocus == "weapons", "Widget detail requests temporary dimming of surrounding widgets");
menu.Appearance();
Check(manager.AppearanceFocus == null, "Leaving widget detail clears temporary focus");
checks += InlineAppearanceChecks.Run();
checks += AircraftSlotUiChecks.Run();
NavigationChecks.Run(Check);
ControlChecks.Run(Check);
SeatPageChecks.Run(Check);
Console.WriteLine($"HUD appearance UI checks passed: {checks} assertions (source-linked callbacks; no Unity rendering).");
void Check(bool result, string label) { if (!result) throw new Exception(label); checks++; }
bool Near(float a, float b) => Math.Abs(a - b) < .00001f;
