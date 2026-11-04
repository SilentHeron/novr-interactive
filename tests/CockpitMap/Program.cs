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
    f.Tick();
    var root = (RectTransform)f.Map.transform;
    var background = (RectTransform)f.Map.mapBackground.transform;
    (int, int, int, int, int) Writes() =>
        (root.SizeWrites, root.ScaleWrites, root.PositionWrites, background.SizeWrites, f.Forward.EnabledWrites);
    var writes = Writes();
    f.Tick(); f.Tick();
    Check(Writes() == writes, "Unchanged map viewport performs no layout or arrow visibility setter calls");

    f.Slot.sizeDelta = new(480, 180); f.Slot.pivot = new(.25f, .7f);
    f.Tick();
    Check(root.sizeDelta == new Vector2(800, 300) && background.sizeDelta == new Vector2(820, 320),
        "Rectangular resize changes native clipping and backing dimensions in the same frame");
    Check(root.localScale == Vector3.one * .6f, "Rectangular resize retains isotropic map scale");
    Check((root.localPosition - (Vector3)f.Slot.rect.center).sqrMagnitude < .00001f,
        "Viewport position follows the current slot pivot in the resize frame");
    Check(root.SizeWrites == writes.Item1 + 1 && root.ScaleWrites == writes.Item2 + 1 &&
        root.PositionWrites == writes.Item3 + 1 && background.SizeWrites == writes.Item4 + 1,
        "Resizing submits each changed viewport property once");
    writes = Writes(); f.Tick();
    Check(Writes() == writes, "Resized viewport returns to zero setter calls on the next stable frame");

    root.sizeDelta = new(20, 30); background.sizeDelta = new(60, 70);
    root.localScale = new(2, 3, 4); root.localPosition = new(9, -4, 7); f.Forward.enabled = true;
    f.Tick();
    Check(root.sizeDelta == new Vector2(800, 300) && background.sizeDelta == new Vector2(820, 320) &&
        root.localScale == Vector3.one * .6f && (root.localPosition - (Vector3)f.Slot.rect.center).sqrMagnitude < .00001f,
        "Native writes are repaired immediately even when the slot dimensions have not changed");
    Check(!f.Forward.enabled, "Native arrow reactivation is repaired in the same frame");
    writes = Writes();
    var nativeTint = new Color(.1f, .3f, .5f, .8f);
    var sprite = new Sprite(); f.Terrain.sprite = sprite; f.Terrain.color = nativeTint;
    f.Tick();
    Check(f.Terrain.sprite == sprite && f.Terrain.color == CockpitMapWidget.TerrainTint(nativeTint),
        "An unchanged viewport still processes new native terrain and tint immediately");
    Check(Writes() == writes, "Terrain refresh does not resubmit unchanged viewport layout");
}
using (var f = new Fixture())
{
    f.Slot.sizeDelta = new(300, 260); f.Tick();
    Near(f.Map.transform.localScale.x, 260f/300, "Non-square slot preserves map aspect");
    Near(f.Map.transform.localScale.y, 260f/300, "Y scale matches X");
    Near(((RectTransform)f.Map.transform).rect.width * f.Map.transform.localScale.x, 300, "Rectangular viewport fills width without stretching map");
    Near(f.Map.mapBackground.transform.localScale.x, f.Map.mapBackground.transform.localScale.y, "Native background uses uniform scale");
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
using (var f = new Fixture())
{
    f.Tick();
    var navigation = CockpitMapWidget.Navigation!;
    navigation.Activate(); navigation.Pan(new(1, .5f), .05f);
    var aircraft = GameManager.Aircraft!;
    aircraft.transform.position = new(350, 20, -170);
    aircraft.transform.rotation = Quaternion.Euler(0, 65, 0);
    var terrain = new Sprite();
    var nativeTint = new Color(.2f, .35f, .6f, .7f);
    f.Terrain.sprite = terrain; f.Terrain.color = nativeTint;
    f.Terrain.rectTransform.sizeDelta = new(1200, 800);
    f.Tick();
    Check(!navigation.Selected, "Loading different terrain in the same lease cancels the previous map selection");
    Check(CockpitMapWidget.Navigation == navigation, "Terrain change keeps the existing map lease and navigation");
    var ownship = aircraft.GlobalPosition().AsVector3() * f.Map.mapDisplayFactor;
    var ownshipInPanel = f.Slot.InverseTransformPoint(f.Terrain.transform.TransformPoint(new(ownship.x, ownship.z, 0)));
    Check((ownshipInPanel - (Vector3)f.Slot.rect.center).sqrMagnitude < .00001f, "New terrain returns to current ownship instead of the old panned coordinate");
    Check(f.Terrain.sprite == terrain && f.Terrain.rectTransform.sizeDelta == new Vector2(1200, 800), "Native terrain sprite and dimensions are not replaced by refresh");
    navigation.Activate(); f.Tick(); f.Tick();
    Check(navigation.Selected, "An unchanged terrain sprite does not repeatedly cancel a fresh selection");
    CockpitMapWidget.ReleaseAll();
    Check(f.Terrain.sprite == terrain && f.Terrain.color == nativeTint && f.Terrain.material == f.Material,
        "Release retains the new native terrain and tint while restoring its material");
    Check(f.Terrain.rectTransform.sizeDelta == new Vector2(1200, 800), "Release retains new terrain dimensions");
}
using (var f = new Fixture())
{
    f.Tick(); var navigation = CockpitMapWidget.Navigation!; navigation.Activate();
    f.Terrain.sprite = null; f.Tick();
    Check(!CockpitMapWidget.Available && !navigation.Selected, "Missing terrain safely releases selection and availability");
    var scans = f.Map.transform.ChildrenQueries;
    f.Tick(); f.Tick();
    Check(CockpitMapWidget.Navigation == navigation && f.Map.transform.ChildrenQueries == scans,
        "Repeated missing-sprite frames do not reborrow the map or rescan its hierarchy");
    var replacement = new Sprite(); f.Terrain.sprite = replacement; f.Tick();
    Check(CockpitMapWidget.Available && !navigation.Selected, "A new sprite after loading restores availability without stale selection");
    Check(navigation.Activate(), "New terrain can be selected again");
    f.Tick(); Check(navigation.Selected, "Recovered terrain selection stays active on subsequent frames");
    CockpitMapWidget.ReleaseAll(); Check(f.Terrain.sprite == replacement, "Null-to-new terrain survives lease release");
}
using (var f = new Fixture())
{
    f.Tick();
    Check(f.Map.transform.ChildrenQueries == 1, "Attach performs exactly one initial hierarchy scan");
    var icon = new GameObject("late icon") { layer = 5 }; icon.transform.SetParent(f.Terrain.transform, false);
    f.Tick(.49f);
    Check(f.Map.transform.ChildrenQueries == 1 && icon.layer == 5, "Initial scan is not repeated before the refresh interval");
    f.Tick(.5f);
    Check(f.Map.transform.ChildrenQueries == 2 && icon.layer == f.Slot.gameObject.layer, "Scheduled refresh still adopts newly created icons");
    f.Tick(.75f); Check(f.Map.transform.ChildrenQueries == 2, "Stable frames do not perform extra hierarchy scans");
    icon.layer = 6; f.Tick(1f);
    Check(f.Map.transform.ChildrenQueries == 3 && icon.layer == f.Slot.gameObject.layer, "Next scheduled refresh still repairs native layer resets");
}
count += NavigationChecks.Run();
count += InteractionChecks.Run();
Console.WriteLine($"{count} source-linked map widget lifecycle, navigation and touch checks passed.");

sealed class Fixture : IDisposable
{
    internal readonly RectTransform Parent, Slot;
    internal readonly DynamicMap Map;
    internal readonly Image Terrain, Forward;
    internal readonly CombatHUD Hud;
    internal readonly TargetListSelector Selector;
    internal readonly Material Material = new();
    internal readonly Color NativeColor = new(0,.3f,0,.6f);
    private readonly CockpitMapWidget _adapter;
    internal Fixture()
    {
        CockpitMapWidget.ReleaseAll(); CockpitHudWidgets.Slots.Clear(); CockpitHudWidgets.Content.Clear();
        CockpitHudWidgets.Editing = false; CockpitHudWidgets.Visible = true;
        GlobalPositions.Origin = Vector3.zero;
        Time.unscaledTime = 0; UnityEngine.XR.XRSettings.enabled = true; DynamicMap.mapMaximized = false;
        GameManager.Aircraft = CockpitHudWidgets.CurrentAircraft = new Aircraft();
        Hud = new CombatHUD { aircraft = GameManager.Aircraft }; Selector = new TargetListSelector();
        SceneSingleton<CombatHUD>.i = Hud; SceneSingleton<TargetListSelector>.i = Selector;
        Parent = new GameObject("HUDMapAnchor").transform;
        var arrow = new GameObject("forwardRef"); arrow.transform.SetParent(Parent,false); Forward = arrow.AddComponent<Image>();
        var go = new GameObject("MapCanvas"); go.transform.SetParent(Parent,false); go.transform.sizeDelta = new(300,300);
        Map = go.AddComponent<DynamicMap>(); SceneSingleton<DynamicMap>.i = Map;
        var background = new GameObject("mapBackground"); background.transform.SetParent(go.transform,false); background.transform.sizeDelta = new(320,320);
        Map.mapBackground = background.AddComponent<Image>();
        Map.mapScaleCenter = new GameObject("zoom center").transform; Map.mapScaleCenter.SetParent(go.transform,false); Map.mapScaleCenter.localScale = Vector3.one * 2;
        Map.mapScaleProxy = new GameObject("zoom proxy").transform; Map.mapScaleProxy.SetParent(background.transform,false);
        var image = new GameObject("mapImage") {layer=3}; image.transform.SetParent(background.transform,false); image.transform.sizeDelta = new(2000,2000); image.transform.localScale = Vector3.one * 2;
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
        SceneSingleton<CombatHUD>.i = null; SceneSingleton<TargetListSelector>.i = null;
        SceneSingleton<CameraStateManager>.i = null; NOVR.APIBus.MainCamera = null;
        typeof(CockpitMapWidget).GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(_adapter,null);
        Object.Destroy(_adapter.gameObject); Object.Destroy(Parent.gameObject); Object.Destroy(Slot.gameObject);
    }
}
