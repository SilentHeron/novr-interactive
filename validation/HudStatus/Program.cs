using System.Reflection;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

var assertions = 0;
void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
void Near(float value, float expected, string message) => Check(MathF.Abs(value - expected) < .00001f, message);
void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null);
Image Backplate(GameObject panel) => panel.GetComponentsInChildren<Image>(true).Single(i => i.gameObject.name == "NOVR status backplate");

var hud = new GameObject("Flight HUD"); hud.AddComponent<FlightHud>();
var panel = new GameObject("TopRightPanel", typeof(RectTransform)); panel.transform.SetParent(hud.transform, false);
var group = panel.AddComponent<CanvasGroup>(); group.alpha = .42f; group.interactable = false;
HudReadability.Add(panel.transform, false);
var readability = panel.GetComponent<HudReadability>()!;
Invoke(readability, "LateUpdate");
Near(group.alpha, .42f, "Native opacity survives ordinary readability update");
Check(panel.Components.OfType<CanvasGroup>().Count() == 1, "Existing CanvasGroup is reused");
var presentation = hud.AddComponent<Fs20HudPresentation>();
CockpitStatusSources.Empty.Add(panel.transform); Invoke(presentation, "LateUpdate");
Near(group.alpha, 0, "Emptied status source is hidden through the existing group");
Check(!Backplate(panel).enabled, "Emptied source backplate is hidden");
Invoke(readability, "LateUpdate"); Near(group.alpha, 0, "Next readability update cannot reveal emptied source");
Invoke(presentation, "OnDisable"); Near(group.alpha, .42f, "Releasing suppression restores original alpha rather than one");
Check(panel.Components.OfType<CanvasGroup>().Count() == 1, "Presentation never creates a second group");
Invoke(readability, "OnDisable");
Check(group.blocksRaycasts && !group.interactable, "Disabling readability restores native input settings");
Near(group.alpha, .42f, "Disabling restores native opacity");
readability.enabled = false; readability.SetWidgetSourcesMoved(false);
Near(group.alpha, .42f, "Parent release after child disable preserves native opacity");
Invoke(readability, "OnDestroy"); Check(!group.Destroyed, "Borrowed native CanvasGroup is not destroyed");

var lower = new GameObject("LowerLeftPanel", typeof(RectTransform));
var lowerGroup = lower.AddComponent<CanvasGroup>(); lowerGroup.alpha = .6f;
var threats = new GameObject("ThreatList"); threats.transform.SetParent(lower.transform, false); threats.AddComponent<ThreatList>();
var text = new GameObject("GridAircraft", typeof(RectTransform), typeof(Text)); text.transform.SetParent(lower.transform, false);
HudReadability.Add(lower.transform, true); var lowerReadability = lower.GetComponent<HudReadability>()!;
CockpitHudWidgets.MapOwned = true; Invoke(lowerReadability, "LateUpdate");
Check(!Backplate(lower).enabled, "Moved map and no incoming missiles leave no lower-left grey square");
Check(text.GetComponent<Text>()!.enabled, "Lower-left text is not blanket-hidden");
Near(lowerGroup.alpha, .09f, "Secondary declutter multiplies original native alpha");
var incoming = new GameObject("Incoming missile"); incoming.transform.SetParent(threats.transform, false);
Invoke(lowerReadability, "LateUpdate");
Check(Backplate(lower).enabled, "Unreplaced native missile fallback retains optional contrast");
Near(lowerGroup.alpha, .6f, "Incoming native missile restores original alpha");
CockpitAlerts.Active = true; Invoke(lowerReadability, "LateUpdate");
Check(!Backplate(lower).enabled, "Compact missile replacement suppresses old backing");
CockpitAlerts.Active = false; incoming.transform.SetParent(null, false);
var map = new GameObject("DynamicMap"); map.AddComponent<DynamicMap>(); map.transform.SetParent(lower.transform, false);
Invoke(lowerReadability, "LateUpdate"); Check(Backplate(lower).enabled, "Unmoved native map retains contrast");
Invoke(lowerReadability, "OnDisable"); Near(lowerGroup.alpha, .6f, "Secondary disable restores original alpha");

var owner = new GameObject("New source"); var lease = new HudCanvasGroupLease(owner);
Check(owner.GetComponent<CanvasGroup>() != null, "Lease creates group when needed");
lease.Apply(0); Near(owner.GetComponent<CanvasGroup>()!.alpha, 0, "New source hides");
lease.Release(); Check(owner.GetComponent<CanvasGroup>() == null, "Only lease-owned group is removed");

var weapon = new GameObject("weaponPanel", typeof(RectTransform));
var safety = new GameObject("weaponDisabled", typeof(RectTransform), typeof(Image)); safety.transform.SetParent(weapon.transform, false);
var safetyImage = safety.GetComponent<Image>()!; safetyImage.color = new Color(.689f, .689f, .689f, .8f);
var safetyGroup = safety.AddComponent<CanvasGroup>(); safetyGroup.alpha = .63f;
var indicator = new NativeWeaponSafetyIndicator(safetyImage, (RectTransform)weapon.transform);
var label = weapon.GetComponentsInChildren<Text>(true).Single();
Check(label.enabled && label.text == "SAFE", "Native safety-on is visible as compact SAFE text");
Near(safetyGroup.alpha, 0, "Only the native grey safety image is hidden");
Check(safetyImage.enabled, "Native safety enabled state is preserved");
Near(safetyImage.color.a, .8f, "Native safety color is not overwritten");
Check(label.rectTransform.sizeDelta.x == 68 && label.rectTransform.sizeDelta.y == 24, "Safety label is compact, not a full-panel grey overlay");
safetyImage.enabled = false; indicator.Refresh();
Check(!label.enabled && !safetyImage.enabled, "Native safety-off removes SAFE without altering game state");
safetyImage.enabled = true; indicator.Refresh(); Check(label.enabled, "Safety-on becomes visible again");
safety.activeSelf = false; indicator.Refresh(); Check(!label.enabled, "Inactive native safety suppresses the label");
safety.activeSelf = true; indicator.Refresh(); indicator.Release();
Near(safetyGroup.alpha, .63f, "Releasing widget restores exact native safety opacity");
Check(!safetyGroup.Destroyed && safetyImage.enabled, "Release preserves native group and current safety state");
Check(label.gameObject.Destroyed, "Temporary safety label is removed on restore");

Console.WriteLine($"HUD status checks passed: {assertions} assertions (source-linked group ownership, legacy backplates and weapon safety).");
