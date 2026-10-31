using System.Reflection;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

var count = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); count++; }
void Near(float a, float b, string message) => Check(Math.Abs(a - b) < .001f, message);
using (var f = new Fixture())
{
    var root = f.Map.gameObject.transform;
    root.localPosition = new(40, 50, 2); root.localScale = new(2, 2, 2);
    var texture = f.Terrain.sprite;
    f.Tick();
    Check(root.parent == f.Slot && CockpitMapWidget.Owns(f.Terrain.transform), "Borrow the live map, including all children");
    Check(f.Terrain.material == null && f.Terrain.sprite == texture, "Terrain uses alpha UI without replacing or copying the native map sprite");
    Check(f.Map.gameObject.activeSelf && CockpitHudWidgets.Content["map"], "Live map is visible");
    Near(root.localScale.x, 1, "Viewport fits square slot without cumulative native scale");
    Check(!f.Forward.enabled, "Old anchor arrow hidden only while its map is borrowed");
    var tint = f.Terrain.color; f.Tick(); f.Tick();
    Check(tint == f.Terrain.color, "Terrain contrast is stable across frames");
    CockpitMapWidget.ReleaseAll();
    Check(root.parent == f.Parent && root.localPosition == new Vector3(40,50,2) && root.localScale == new Vector3(2,2,2), "Restore original hierarchy and transform");
    Check(f.Terrain.material == f.Material && f.Terrain.color == f.NativeColor, "Restore original material and native tint");
    Check(f.Forward.enabled && !CockpitHudWidgets.Content["map"], "Restore authored arrow and relinquish content");
}
using (var f = new Fixture())
{
    f.Slot.sizeDelta = new(300, 260); f.Tick();
    Near(f.Map.transform.localScale.x, 260f/300, "Non-square slot preserves map aspect");
    Near(f.Map.transform.localScale.y, 260f/300, "Y scale matches X");
    f.Terrain.color = new Color(.2f,.8f,.6f,.7f); var changed = f.Terrain.color;
    f.Tick(); f.Tick(); CockpitMapWidget.ReleaseAll();
    Check(f.Terrain.color == changed, "Restoration preserves a native theme change during ownership");
}
using (var f = new Fixture())
{
    f.Tick(); var icon = new GameObject("late native icon") {layer = 5}; icon.transform.SetParent(f.Terrain.transform, false);
    f.Tick(1); Check(icon.layer == f.Slot.gameObject.layer, "Adopt icons created by the live native map");
    icon.layer = 6; f.Tick(2); Check(icon.layer == f.Slot.gameObject.layer, "Repair later native layer reset");
    CockpitMapWidget.ReleaseAll(); Check(icon.layer == 5 && f.Terrain.gameObject.layer == 3, "Restore first native layers of existing and late children");
}
using (var f = new Fixture())
{
    f.Tick();
    typeof(CockpitMapMaximizePatch).GetMethod("Prefix", BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);
    Check(f.Map.transform.parent == f.Parent && f.Terrain.material == f.Material, "Maximize prefix releases map before native reparenting");
    DynamicMap.mapMaximized = true; var large = new GameObject("native full map"); f.Map.transform.SetParent(large.transform,false); f.Tick();
    Check(f.Map.transform.parent == large.transform, "Never steal the maximized map");
    DynamicMap.mapMaximized = false; f.Map.transform.SetParent(f.Parent,false); f.Tick();
    Check(f.Map.transform.parent == f.Slot, "Native minimized map docks again after full map closes");
}
using (var f = new Fixture())
{
    f.Tick(); f.Map.gameObject.SetActive(false); CockpitMapWidget.ReleaseAll();
    Check(!f.Map.gameObject.activeSelf, "Native visibility is never forced on during release");
    f.Tick(); Check(f.Map.transform.parent == f.Parent, "Do not steal an inactive native map");
    f.Map.gameObject.SetActive(true); f.Tick(); UnityEngine.XR.XRSettings.enabled = false; f.Tick();
    Check(f.Map.transform.parent == f.Parent && f.Terrain.material == f.Material, "Disabling VR releases native map and material");
}
using (var f = new Fixture())
{
    f.Tick(); GameManager.Aircraft = new Aircraft(); f.Tick();
    Check(f.Map.transform.parent == f.Parent, "Aircraft mismatch restores map before new slot owner is ready");
}
using (var f = new Fixture())
{
    f.Tick(); Object.Destroy(f.Parent.gameObject); CockpitMapWidget.ReleaseAll();
    Check(f.Map == null, "Destroyed scene owner cannot orphan native singleton inside widget rig");
}
Console.WriteLine($"{count} source-linked map widget lifecycle checks passed.");

sealed class Fixture : IDisposable
{
    internal readonly RectTransform Parent, Slot;
    internal readonly DynamicMap Map;
    internal readonly Image Terrain, Forward;
    internal readonly Material Material = new();
    internal readonly Color NativeColor = new(0,.3f,0,.6f);
    private readonly CockpitMapWidget _adapter;
    internal Fixture()
    {
        CockpitMapWidget.ReleaseAll(); CockpitHudWidgets.Slots.Clear(); CockpitHudWidgets.Content.Clear();
        Time.unscaledTime = 0; UnityEngine.XR.XRSettings.enabled = true; DynamicMap.mapMaximized = false;
        GameManager.Aircraft = CockpitHudWidgets.CurrentAircraft = new Aircraft();
        Parent = new GameObject("HUDMapAnchor").transform;
        var arrow = new GameObject("forwardRef"); arrow.transform.SetParent(Parent,false); Forward = arrow.AddComponent<Image>();
        var go = new GameObject("MapCanvas"); go.transform.SetParent(Parent,false); go.transform.sizeDelta = new(300,300);
        Map = go.AddComponent<DynamicMap>(); SceneSingleton<DynamicMap>.i = Map;
        var image = new GameObject("mapImage") {layer=3}; image.transform.SetParent(go.transform,false);
        Terrain = image.AddComponent<Image>(); Terrain.material = Material; Terrain.color = NativeColor; Terrain.sprite = new Sprite(); Map.mapImage = image;
        Slot = new GameObject("slot") {layer=30}.transform; Slot.sizeDelta = new(300,300); CockpitHudWidgets.Slots["map"] = Slot;
        _adapter = new GameObject("adapter").AddComponent<CockpitMapWidget>();
    }
    internal void Tick(float time = 0)
    {
        Time.unscaledTime = time;
        typeof(CockpitMapWidget).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(_adapter,null);
    }
    public void Dispose()
    {
        CockpitMapWidget.ReleaseAll(); SceneSingleton<DynamicMap>.i = null;
        typeof(CockpitMapWidget).GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(_adapter,null);
        Object.Destroy(_adapter.gameObject); Object.Destroy(Parent.gameObject); Object.Destroy(Slot.gameObject);
    }
}
