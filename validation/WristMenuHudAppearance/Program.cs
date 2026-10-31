using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;

var checks = 0;
var manager = new CockpitHudWidgets(); CockpitHudWidgets.Instance = manager;
var menu = new WristMenu(); menu.Build(); menu.Appearance();
Check(menu.Backplate.minValue == 0 && menu.Backplate.maxValue == 100 && menu.Backplate.wholeNumbers,
    "Slider covers clear through solid in one-percent steps");
Check(manager.Begins == 0 && !CockpitHudWidgets.Editing, "Appearance does not open placement guides");

for (var i = 0; i < HudWidgetProfiles.Ids.Length; i++)
{
    var id = HudWidgetProfiles.Ids[i];
    menu.Appearance(); menu.Click("WIDGET APPEARANCE", id); menu.Refresh();
    var writes = manager.AppearanceSaves;
    Check(menu.Backplate.value == 0 && manager.AppearanceSaves == writes, "Selection only reads saved backplate: " + id);
    menu.Backplate.value = (i + 1) * 10;
    Check(Near(manager.GetPose(id).Background, (i + 1) * .1f), "Slider changes selected backplate: " + id);
    Check(manager.GetPose(id).Opacity == 1 && manager.GetPose(id).Scale == 1 && manager.GetPose(id).Visible,
        "Slider leaves readouts, size and visibility unchanged: " + id);
}
for (var i = 0; i < HudWidgetProfiles.Ids.Length; i++)
{
    var id = HudWidgetProfiles.Ids[i]; var writes = manager.AppearanceSaves;
    menu.Appearance(); menu.Click("WIDGET APPEARANCE", id); menu.Refresh();
    Check(menu.Backplate.value == (i + 1) * 10 && manager.AppearanceSaves == writes,
        "Each widget retains its own value without selection writes: " + id);
}
menu.Appearance(); menu.Click("WIDGET APPEARANCE", "weapons"); menu.Refresh();
menu.Backplate.value = 100;
Check(manager.GetPose("weapons").Background == 1 && menu.BackplateLabel.text == "Backplate opacity 100%", "Solid endpoint and readout");
menu.Backplate.value = 0;
Check(manager.GetPose("weapons").Background == 0 && manager.GetPose("weapons").Opacity == 1, "Clear backplate leaves labels fully visible");
menu.Backplate.value = 67;
menu.Click("HUD WIDGET", "Fainter readouts"); menu.Refresh();
Check(Near(manager.GetPose("weapons").Opacity, .9f) && Near(manager.GetPose("weapons").Background, .67f), "Readout controls leave backplate unchanged");
var exact = manager.GetPose("weapons"); exact.Background = .333333f; manager.SetAppearance("weapons", exact);
var saves = manager.AppearanceSaves; menu.Refresh();
Check(menu.Backplate.value == 33 && manager.GetPose("weapons").Background == exact.Background && manager.AppearanceSaves == saves,
    "Opening an existing fractional value never quantizes or saves it");
manager.Available = false; menu.Refresh(); menu.Backplate.value = 89;
Check(!menu.Backplate.interactable && manager.AppearanceSaves == saves, "Unavailable cockpit disables and rejects changes");
manager.Available = true; manager.Begin(); menu.Refresh(); menu.Backplate.value = 45;
Check(CockpitHudWidgets.Editing && manager.Begins == 1 && Near(manager.GetPose("weapons").Background, .45f), "Editing reuses the existing preview without starting another");
Check(!menu.Buttons.Keys.Any(k => k.text == "Less background" || k.text == "More background"), "Slider replaces the old increment buttons");
Console.WriteLine($"HUD appearance UI checks passed: {checks} assertions (source-linked callbacks; no Unity rendering).");
void Check(bool result, string label) { if (!result) throw new Exception(label); checks++; }
bool Near(float a, float b) => Math.Abs(a - b) < .00001f;
