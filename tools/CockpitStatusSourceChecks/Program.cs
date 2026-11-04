using System.Reflection;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

var count = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
void Near(float a, float b, string message) => Check(Math.Abs(a - b) < .001f, message);

using (var f = new Fixture())
{
    var capacitor = f.Widgets["power"]; capacitor.SetActive(false); f.Tick();
    Check(!capacitor.activeSelf && capacitor.transform.parent == CockpitHudWidgets.Slots["power"], "Dormant capacitor is borrowed without activation");
    Check(!CockpitHudWidgets.Content["power"], "Dormant capacitor has no replacement content");
    var fitted = capacitor.transform.localPosition;
    capacitor.SetActive(true); f.Tick(.01f);
    Check(capacitor.transform.localPosition == fitted, "Native charge event does not start at old coordinates");
    Check(CockpitHudWidgets.Content["power"], "Native charge activation becomes available immediately");
    CockpitStatusSources.ReleaseAll();
    Check(capacitor.activeSelf && capacitor.transform.parent == f.Panel, "Restore preserves changed native visibility");
}
using (var f = new Fixture())
{
    var weapon = f.Widgets["weapons"];
    var nativeGroup = weapon.AddComponent<CanvasGroup>(); nativeGroup.alpha = .37f;
    f.Tick(); f.Panel.gameObject.SetActive(false); f.Tick(.01f);
    Check(weapon.activeSelf, "Parent hiding never changes native activeSelf");
    Check(nativeGroup.alpha == 0 && weapon.Components.OfType<CanvasGroup>().Count(g => !g.Dead) == 1, "Borrowed presentation hides through the single native CanvasGroup");
    Check(!CockpitHudWidgets.Content["weapons"], "Hidden native owner contributes no content");
    f.Panel.gameObject.SetActive(true); f.Tick(.01f);
    Near(nativeGroup.alpha, .37f, "Native parent restores original opacity without rebinding or a second CanvasGroup");
    CockpitStatusSources.ReleaseAll();
    Check(nativeGroup.alpha == .37f && weapon.Components.OfType<CanvasGroup>().Count(g => !g.Dead) == 1, "Release leaves existing native CanvasGroups intact");
}
using (var f = new Fixture())
{
    var weapon = f.Widgets["weapons"]; var rect = weapon.transform;
    rect.anchorMin = new Vector2(.1f, .2f); rect.anchorMax = new Vector2(.7f, .8f); rect.pivot = new Vector2(1, 1);
    rect.localPosition = new Vector3(17, -31, 2); rect.localScale = new Vector3(.6f, .7f, 1); rect.localRotation = new Quaternion(0, 0, .2f, .98f);
    var original = (rect.anchorMin, rect.anchorMax, rect.pivot, rect.localPosition, rect.localScale, rect.localRotation, rect.sizeDelta, rect.GetSiblingIndex());
    weapon.layer = 9; var child = rect.GetChild(0).gameObject; child.layer = 7;
    f.Tick();
    Check(weapon.layer == 30 && child.layer == 30, "All borrowed graphics use the widget camera layer");
    Check(CockpitStatusSources.Owns(child.transform), "Ownership includes child graphics");
    Check(CockpitStatusSources.EmptiedStatusPanel(f.Panel), "Common empty status parent is identified for all aircraft");
    var separateThreats = new GameObject("LowerLeftPanel");
    Check(!CockpitStatusSources.EmptiedStatusPanel(separateThreats.transform), "Threat fallback container is never blanket-hidden");
    CockpitStatusSources.ReleaseAll();
    Check((rect.anchorMin, rect.anchorMax, rect.pivot, rect.localPosition, rect.localScale, rect.localRotation, rect.sizeDelta, rect.GetSiblingIndex()) == original, "Every native rect field and sibling index restores");
    Check(weapon.layer == 9 && child.layer == 7, "Per-node layers restore exactly");
    Check(weapon.GetComponent<Image>()!.enabled, "Native background restores");
    Check(!CockpitStatusSources.Owns(child.transform) && !CockpitStatusSources.EmptiedStatusPanel(f.Panel), "Presentation ownership releases");
}
using (var f = new Fixture())
{
    var weapon = f.Widgets["weapons"]; f.Tick(); weapon.SetActive(false);
    CockpitStatusSources.ReleaseAll();
    Check(!weapon.activeSelf, "Release never resurrects a natively hidden weapon");
    foreach (var slot in CockpitHudWidgets.Slots.Values) Object.Destroy(slot.gameObject);
    Check(weapon != null && weapon.transform.parent == f.Panel, "Slot destruction after release preserves native hierarchy");
}
foreach (var order in new[]
{
    new[] { "countermeasures", "weapons", "power" }, // Installed native hierarchy.
    new[] { "power", "countermeasures", "weapons" },
    new[] { "weapons", "power", "countermeasures" }
})
using (var f = new Fixture())
{
    for (var i = 0; i < order.Length; i++) f.Widgets[order[i]].transform.SetSiblingIndex(i);
    f.Tick(); CockpitStatusSources.ReleaseAll();
    Check(order.Select(id => f.Widgets[id].transform).SequenceEqual(f.Panel.Children), "Borrow/restore preserves authored sibling order for " + string.Join(",", order));
}
using (var f = new Fixture())
{
    var weapon = f.Widgets["weapons"]; f.Tick();
    Object.Destroy(f.Panel.gameObject); CockpitStatusSources.ReleaseAll();
    Check(weapon == null, "Lost native owner cannot orphan a component into the persistent rig");
}
using (var f = new Fixture())
{
    var weapon = f.Widgets["weapons"]; weapon.FailNextCanvasGroup = true;
    f.Tick(); f.Tick();
    Check(weapon.transform.parent == f.Panel && weapon.GetComponent<Image>()!.enabled, "Failed binding rolls back to native presentation");
    Check(Debug.Warnings == 1, "Failure reports once rather than retrying every frame");
    Check(!CockpitStatusSources.EmptiedStatusPanel(f.Panel), "Partial replacement keeps native fallback visible");
    Check(CockpitStatusSources.Owns(f.Widgets["countermeasures"].transform), "One failed source does not block independent widgets");
}
using (var f = new Fixture())
{
    var weapon = f.Widgets["weapons"]; weapon.transform.sizeDelta = new Vector2(240, 100);
    var image = new GameObject("weaponImage"); image.transform.SetParent(weapon.transform, false);
    image.AddComponent<Image>(); image.transform.sizeDelta = new Vector2(240, 120); image.SetActive(false);
    CockpitHudWidgets.Slots["weapons"].sizeDelta = new Vector2(420, 170); f.Tick();
    Near(weapon.transform.localScale.y, 170f / 120, "Fit includes inactive overflowing native geometry");
    f.Tick(); Near(weapon.transform.localScale.y, 170f / 120, "Repeated discovery cannot accumulate scale");
    var oldAircraft = GameManager.Aircraft;
    GameManager.Aircraft = new Aircraft(); f.Tick();
    Check(weapon.transform.parent == f.Panel && !CockpitStatusSources.Owns(weapon.transform), "Aircraft mismatch releases rather than borrowing into wrong layout");
    GameManager.Aircraft = oldAircraft;
}
using (var f = new Fixture())
{
    var weapon = f.Widgets["weapons"]; weapon.transform.sizeDelta = new Vector2(240, 100);
    var image = new GameObject("weaponImage"); image.transform.SetParent(weapon.transform, false);
    image.AddComponent<Image>(); image.transform.sizeDelta = new Vector2(240, 120);
    var safety = new GameObject("weaponDisabled"); safety.transform.SetParent(weapon.transform, false);
    safety.transform.sizeDelta = new Vector2(1200, 900); // This backing must not determine the fit.
    var safetyImage = safety.AddComponent<Image>(); safetyImage.color = new Color(.689f, .689f, .689f, .8f);
    var safetyGroup = safety.AddComponent<CanvasGroup>(); safetyGroup.alpha = .63f;
    CockpitHudWidgets.Slots["weapons"].sizeDelta = new Vector2(420, 170); f.Tick();
    var label = weapon.transform.Find("NOVR weapon safety")!.GetComponent<Text>();
    Check(label.enabled && label.text == "SAFE", "Safety-on is represented in the borrowed weapon widget");
    Check(safetyImage.enabled && safetyGroup.alpha == 0, "Only grey safety overlay is suppressed; native safety enabled state remains intact");
    Near(weapon.transform.localScale.y, 170f / 120, "Oversized hidden backing does not shrink the weapon readout");
    Check(image.GetComponent<Image>().enabled && weapon.transform.GetChild(0).GetComponent<TMP_Text>().enabled, "Weapon icon and original readout remain enabled");
    safetyImage.enabled = false; f.Tick(.01f);
    Check(!label.enabled && !safetyImage.enabled, "Native safety release updates the compact label immediately");
    safetyImage.enabled = true; f.Tick(.01f); Check(label.enabled, "Native safety can engage again without rebinding");
    CockpitStatusSources.ReleaseAll();
    Near(safetyGroup.alpha, .63f, "Release restores native safety overlay opacity");
    Check(!safetyGroup.Dead && safetyImage.enabled && label == null, "Release preserves native safety state and removes only temporary label");
    Check(weapon.transform.parent == f.Panel && safety.transform.parent == weapon.transform, "Weapon and safety hierarchy restore after compact presentation");
}
using (var f = new Fixture())
{
    var damage = f.AddDamage("StatusDisplay_SmallFighter1(Clone)");
    var rect = damage.transform; rect.sizeDelta = new Vector2(270, 270); rect.pivot = new Vector2(1, 0);
    rect.localPosition = new Vector3(4, 7, 0); rect.localScale = new Vector3(.00045f, .00045f, .00045f);
    var originalParent = rect.parent; var originalPosition = rect.localPosition; var originalScale = rect.localScale;
    var silhouette = damage.GetComponent<Image>();
    var native = damage.GetComponent<StatusDisplay>();
    var part = rect.GetChild(0).GetComponent<Image>();
    part.color = new Color(1, .2f, 0, .8f);
    f.Tick();
    Check(rect.parent == CockpitHudWidgets.Slots["damage"], "Damage lease attaches to movable widget");
    Check(silhouette.enabled && silhouette.color.a == 1 && native.enabled, "Native aircraft silhouette and damage logic remain enabled");
    Check(part.enabled && part.color == new Color(1, .2f, 0, .8f), "Native part damage colors survive reparenting unchanged");
    Near(rect.localScale.x, 300f / 270, "Damage source fits its own aspect and ignores old 0.00045 docking scale");
    Check(CockpitHudWidgets.Content["damage"] && CockpitStatusSources.Owns(part.transform), "Damage graphics contribute content and belong to movable widget");
    Check(CockpitStatusSources.EmptiedStatusPanel(f.Panel) && !CockpitStatusSources.EmptiedStatusPanel(originalParent),
        "Separate damage anchor does not resurrect old status backplate or hide unrelated panels");
    silhouette.color = new Color(1, 1, 1, 0); part.color = new Color(1, .2f, 0, 0); native.enabled = false;
    f.Tick(.01f);
    Check(!CockpitHudWidgets.Content["damage"] && !native.enabled, "Native healthy-aircraft fade can leave only edit placeholder without forced activation");
    part.color = new Color(.7f, 0, .25f, 1); native.enabled = true; f.Tick(.01f);
    Check(CockpitHudWidgets.Content["damage"] && part.color == new Color(.7f, 0, .25f, 1), "Later native damage/detachment state becomes visible without rebinding");
    originalParent.gameObject.SetActive(false); f.Tick(.01f);
    Check(!CockpitHudWidgets.Content["damage"] && damage.activeSelf && damage.GetComponent<CanvasGroup>().alpha == 0,
        "Native HUD/map owner hiding suppresses borrowed damage without changing its active state");
    originalParent.gameObject.SetActive(true); f.Tick(.01f);
    Check(CockpitHudWidgets.Content["damage"], "Leaving native HUD hiding restores current damage immediately");
    CockpitStatusSources.ReleaseAll();
    Check(rect.parent == originalParent && rect.localPosition == originalPosition && rect.localScale == originalScale && rect.pivot == new Vector2(1, 0),
        "Damage returns to native anchor with its exact original transform");
    Check(silhouette.enabled && silhouette.color.a == 0 && part.color == new Color(.7f, 0, .25f, 1) && native.enabled,
        "Restoration preserves live native damage/fade state");
}
using (var f = new Fixture())
{
    // Aircraft mods choose their own prefab names; component identity at the
    // live HUD anchor is the contract, not one hard-coded vanilla name.
    var damage = f.AddDamage("Custom aircraft health diagram"); damage.transform.sizeDelta = new Vector2(128, 256); f.Tick();
    Near(damage.transform.localScale.x, 300f / 256, "Tall helicopter damage diagram keeps its aspect ratio");
    Check(damage.transform.parent == CockpitHudWidgets.Slots["damage"], "Damage discovery supports arbitrary aircraft prefab names");
    var owner = damage.transform.parent;
    Object.Destroy(damage); f.Tick(.01f);
    Check(!CockpitHudWidgets.Content["damage"], "Natively destroyed damage component clears moved content safely");
    var next = f.AddDamage("Next aircraft diagram"); f.Tick();
    Check(next.transform.parent == CockpitHudWidgets.Slots["damage"] && CockpitHudWidgets.Content["damage"], "Replacement native damage instance is discovered after destruction");
}
using (var f = new Fixture())
{
    var damage = f.AddDamage("StatusDisplay_Fighter1(Clone)");
    var parent = damage.transform.parent; damage.FailNextCanvasGroup = true; f.Tick();
    Check(damage.transform.parent == parent && damage.GetComponent<Image>().enabled && damage.GetComponent<StatusDisplay>().enabled,
        "Failed damage attachment leaves native diagram and behavior available");
    Check(CockpitStatusSources.EmptiedStatusPanel(f.Panel), "Damage failure does not block independent weapons/CM/power replacement");
}
using (var f = new Fixture())
{
    var hud = SceneSingleton<FlightHud>.i!;
    var reportRoot = new GameObject("aircraftActions"); reportRoot.transform.SetParent(hud.transform, false);
    var report = reportRoot.AddComponent<AircraftActionsReport>(); SceneSingleton<AircraftActionsReport>.i = report;
    var background = reportRoot.AddComponent<Image>(); background.color = new Color(0, 0, 0, .8f);
    var labelObject = new GameObject("ActionReportText"); labelObject.transform.SetParent(reportRoot.transform, false);
    var reportText = labelObject.AddComponent<TextMeshProUGUI>(); reportText.color = new Color(1, 1, 1, 1);
    report.SetFields(reportText, background); report.NativeReport("Cleared to taxi to Runway 21", true);
    var navRoot = new GameObject("AirbaseOverlay"); navRoot.transform.SetParent(hud.transform, false);
    var overlay = navRoot.AddComponent<AirbaseOverlay>();
    var marker = navRoot.AddComponent<Image>();
    var taxiObject = new GameObject("AirbaseLabel"); taxiObject.transform.SetParent(navRoot.transform, false);
    var taxi = taxiObject.AddComponent<TextMeshProUGUI>(); taxi.color = new Color(.1f, 1, .2f, 1);
    overlay.SetLabel(taxi); overlay.NativeLabel("Taxi to Runway 21 187m");
    var slot = new GameObject("Instructions content") { layer = 30 }.transform; slot.sizeDelta = new Vector2(420, 150);
    CockpitHudWidgets.Slots.Add("objectives", slot); f.Tick();
    var copies = slot.Children.Select(x => x.GetComponent<TextMeshProUGUI>()).ToArray();
    var taxiCopy = copies.Single(x => x.fontSize == 22); var reportCopy = copies.Single(x => x.fontSize == 19);
    Check(taxiCopy.text == taxi.text && reportCopy.text == reportText.text && CockpitHudWidgets.Content["objectives"],
        "Instructions mirrors both current taxi guidance and separate native action feed");
    Check(taxi.canvasRenderer.cull && reportText.canvasRenderer.cull && background.canvasRenderer.cull,
        "Original instruction text and its own backing are draw-culled despite additive shaders");
    Check(marker.enabled && !marker.canvasRenderer.cull && taxi.transform.parent == navRoot.transform,
        "Airbase marker geometry and original world hierarchy are untouched");
    Check(taxi.enabled && reportText.enabled && background.enabled && reportText.text == "Cleared to taxi to Runway 21",
        "Native text, enabled state and report queue inputs remain intact");
    Check(taxiCopy.color == taxi.color && reportCopy.rectTransform.sizeDelta == new Vector2(408, 102) && reportCopy.maxVisibleLines == 4,
        "Copies preserve native color with fixed readable widget layout");
    overlay.NativeLabel("Taxi to Runway 21 95m"); report.NativeReport("Rearmed\nCleared for departure", true); f.Tick(.01f);
    Check(taxiCopy.text.EndsWith("95m") && reportCopy.text == "Rearmed\nCleared for departure",
        "Native progress and timed message replacements update without rediscovery");
    report.NativeReport("", false); f.Tick(.01f);
    Check(!reportCopy.enabled && !background.enabled && taxiCopy.enabled, "Native message expiry removes only report content");
    overlay.NativeLabel("Stonefield Airbase 2km"); f.Tick(.01f);
    Check(!taxiCopy.enabled && !taxi.canvasRenderer.cull && !CockpitHudWidgets.Content["objectives"],
        "Non-taxi airbase names stay in world HUD instead of entering Instructions");
    overlay.NativeLabel("Taxi to Runway 21 30m"); report.NativeReport("Clearance update", true);
    CockpitHudWidgets.Visible = false; f.Tick(.01f);
    Check(!taxiCopy.enabled && !reportCopy.enabled && taxi.canvasRenderer.cull && reportText.canvasRenderer.cull,
        "Eye-hidden Instructions does not resurrect overhead originals");
    CockpitHudWidgets.Visible = true; CockpitHudWidgets.Instance!.Available = false; f.Tick(.01f);
    Check(!taxi.canvasRenderer.cull && !reportText.canvasRenderer.cull && !background.canvasRenderer.cull && !taxiCopy.enabled,
        "Full map or menu restores native presentation and hides compact copies");
    // Recovery is selected later than the source adapter. Its explicit refresh
    // must suppress outside-HUD text in that same frame, without another tick.
    CockpitAlerts.InRecovery = true; CockpitHudWidgets.Slots.Remove("objectives");
    GameManager.Aircraft = null; CockpitStatusSources.RefreshInstructions();
    Check(taxi.canvasRenderer.cull && reportText.canvasRenderer.cull && background.canvasRenderer.cull && !reportCopy.enabled,
        "Crash recovery suppresses overhead instructions even after cockpit widgets release");
    // TMP can create extra geometry after the hide begins. A native layout
    // rebuild may also clear culling before the final canvas draw.
    var extra = new GameObject("TMP SubMeshUI"); extra.transform.SetParent(reportText.transform, false);
    var extraGraphic = extra.AddComponent<Image>(); reportText.canvasRenderer.cull = false; Canvas.Render();
    Check(reportText.canvasRenderer.cull && extraGraphic.canvasRenderer.cull,
        "Before-render suppression covers later TMP submeshes and layout cull resets");
    CockpitAlerts.InRecovery = false; f.Tick(.01f);
    Check(!taxi.canvasRenderer.cull && !reportText.canvasRenderer.cull && !extraGraphic.canvasRenderer.cull,
        "Leaving VR recovery restores only the native draw states that were borrowed");
    Object.Destroy(reportRoot); SceneSingleton<AircraftActionsReport>.i = null; f.Tick(); Canvas.Render();
    Check(!reportCopy.enabled, "Natively destroyed report removes copied instructions and releases dead draw leases");
}
Console.WriteLine($"{count} source-linked cockpit widget lifecycle checks passed.");

sealed class Fixture : IDisposable
{
    internal readonly Dictionary<string, GameObject> Widgets = new();
    internal readonly RectTransform Panel;
    private readonly CockpitStatusSources _adapter;
    internal Fixture()
    {
        CockpitStatusSources.ReleaseAll(); CockpitHudWidgets.Slots.Clear(); CockpitHudWidgets.Content.Clear();
        Debug.Warnings = 0; UnityEngine.XR.XRSettings.enabled = true;
        CockpitHudWidgets.Instance = new CockpitHudWidgets(); CockpitHudWidgets.Visible = true;
        CockpitAlerts.InRecovery = false; SceneSingleton<AircraftActionsReport>.i = null;
        GameManager.Aircraft = CockpitHudWidgets.CurrentAircraft = new Aircraft();
        var hud = new GameObject("HUDCanvas"); SceneSingleton<FlightHud>.i = hud.AddComponent<FlightHud>();
        Panel = new GameObject("TopRightPanel").transform; Panel.SetParent(hud.transform, false);
        foreach (var (id, name) in new[] { ("weapons", "weaponPanel"), ("countermeasures", "countermeasuresBackground"), ("power", "PowerPanel") })
        {
            var widget = new GameObject(name); widget.transform.SetParent(Panel, false); widget.AddComponent<Image>();
            var label = new GameObject("Label"); label.transform.SetParent(widget.transform, false); label.transform.sizeDelta = new Vector2(70, 20);
            label.AddComponent<TMP_Text>().text = "Native readout"; Widgets.Add(id, widget);
            var slot = new GameObject(id + " content") { layer = 30 }.transform; slot.sizeDelta = new Vector2(300, 150);
            CockpitHudWidgets.Slots.Add(id, slot);
        }
        _adapter = new GameObject("adapter").AddComponent<CockpitStatusSources>();
    }
    internal void Tick(float delta = 1)
    {
        Time.unscaledTime += delta;
        typeof(CockpitStatusSources).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_adapter, null);
    }
    internal GameObject AddDamage(string name)
    {
        var hud = SceneSingleton<FlightHud>.i!;
        if (hud.statusAnchor == null)
        {
            hud.statusAnchor = new GameObject("LowerRightPanel").transform;
            hud.statusAnchor.SetParent(hud.transform, false);
        }
        var diagram = new GameObject(name); diagram.transform.SetParent(hud.statusAnchor, false);
        diagram.AddComponent<StatusDisplay>(); diagram.AddComponent<Image>().color = new Color(1, 1, 1, 1);
        var part = new GameObject("Wing"); part.transform.SetParent(diagram.transform, false);
        part.transform.sizeDelta = new Vector2(60, 60); part.AddComponent<Image>().color = new Color(1, 0, 0, 0);
        if (!CockpitHudWidgets.Slots.ContainsKey("damage"))
        {
            var slot = new GameObject("damage content") { layer = 30 }.transform; slot.sizeDelta = new Vector2(300, 300);
            CockpitHudWidgets.Slots.Add("damage", slot);
        }
        return diagram;
    }
    public void Dispose() => typeof(CockpitStatusSources).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_adapter, null);
}
