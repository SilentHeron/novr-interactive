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
    Check(weapon.Components.OfType<CanvasGroup>().Where(g => !g.Dead).Any(g => g != nativeGroup && g.alpha == 0), "Borrowed presentation follows native parent hiding");
    Check(!CockpitHudWidgets.Content["weapons"], "Hidden native owner contributes no content");
    f.Panel.gameObject.SetActive(true); f.Tick(.01f);
    Check(weapon.Components.OfType<CanvasGroup>().Where(g => !g.Dead).Any(g => g != nativeGroup && g.alpha == 1), "Native parent can restore presentation without rebinding");
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
    image.transform.sizeDelta = new Vector2(240, 120); image.SetActive(false);
    CockpitHudWidgets.Slots["weapons"].sizeDelta = new Vector2(420, 170); f.Tick();
    Near(weapon.transform.localScale.y, 170f / 120, "Fit includes inactive overflowing native geometry");
    f.Tick(); Near(weapon.transform.localScale.y, 170f / 120, "Repeated discovery cannot accumulate scale");
    var oldAircraft = GameManager.Aircraft;
    GameManager.Aircraft = new Aircraft(); f.Tick();
    Check(weapon.transform.parent == f.Panel && !CockpitStatusSources.Owns(weapon.transform), "Aircraft mismatch releases rather than borrowing into wrong layout");
    GameManager.Aircraft = oldAircraft;
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
    public void Dispose() => typeof(CockpitStatusSources).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_adapter, null);
}
