using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.SpecialBehavior;

// Borrow only the game's live leaf widgets. WeaponStatus, CombatHUD and
// ChargeIndicator/StatusDisplay retain their events, visibility and animations.
[DefaultExecutionOrder(110)]
internal sealed class CockpitStatusSources : MonoBehaviour
{
    private sealed class Source
    {
        internal readonly string Id, Name;
        internal Lease? Lease;
        internal bool Failed;
        internal Source(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    internal sealed class Lease
    {
        internal readonly RectTransform Widget, Slot;
        internal readonly Transform Parent;
        internal readonly int Index;
        internal bool Attached;
        private readonly Vector3 _position, _scale;
        private readonly Quaternion _rotation;
        private readonly Vector2 _anchorMin, _anchorMax, _pivot, _size;
        private readonly Image? _background;
        private readonly bool _backgroundEnabled;
        private HudCanvasGroupLease? _visibility;
        private NativeWeaponSafetyIndicator? _safety;
        private readonly bool _compactWeapon;
        private readonly bool _damage;
        private readonly AuxiliaryGaugeBounds? _auxiliaryBounds;
        private readonly BorrowedLayers _layers = new();
        private readonly List<Transform> _nodes = new();
        private readonly List<Graphic> _graphics = new();
        private readonly Vector3[] _corners = new Vector3[4];
        private Bounds _bounds;

        internal Lease(RectTransform widget, RectTransform slot, bool compactWeapon, bool damage = false, bool preserveRootImage = false, HUDApp? auxiliary = null)
        {
            Widget = widget;
            Slot = slot;
            Parent = widget.parent;
            _compactWeapon = compactWeapon;
            _damage = damage;
            _auxiliaryBounds = auxiliary != null ? new AuxiliaryGaugeBounds(auxiliary, widget) : null;
            Index = widget.GetSiblingIndex();
            _position = widget.localPosition;
            _scale = widget.localScale;
            _rotation = widget.localRotation;
            _anchorMin = widget.anchorMin;
            _anchorMax = widget.anchorMax;
            _pivot = widget.pivot;
            _size = widget.sizeDelta;
            // StatusDisplay's root image is its aircraft silhouette, including
            // native damage fading. It is content, never a removable backplate.
            _background = damage || preserveRootImage ? null : widget.GetComponent<Image>();
            _backgroundEnabled = _background != null && _background.enabled;
        }

        internal void Attach()
        {
            var widget = Widget;
            var dimensions = widget.rect.size;
            if (_background != null)
                _background.enabled = false;
            _visibility = new HudCanvasGroupLease(widget.gameObject);
            // Do not activate the source: the capacitor deliberately remains
            // dormant until the native power-supply event makes it relevant.
            widget.SetParent(Slot, false);
            widget.anchorMin = widget.anchorMax = new Vector2(.5f, .5f);
            widget.sizeDelta = dimensions;
            widget.localRotation = Quaternion.identity;
            if (_compactWeapon)
            {
                // Exact native safety overlay only. Weapon art, ammunition,
                // reload progress and other descendant images remain intact.
                var safety = widget.Find("weaponDisabled")?.GetComponent<Image>();
                if (safety != null)
                    _safety = new NativeWeaponSafetyIndicator(safety, widget);
            }
            Refresh();
            Attached = true;
        }

        internal bool Valid => Widget != null && Slot != null && Parent != null && Widget.parent == Slot;

        internal void Refresh()
        {
            if (!Valid)
                return;
            _nodes.Clear();
            Widget.GetComponentsInChildren(true, _nodes);
            _layers.Apply(_nodes, Slot.gameObject.layer);
            _graphics.Clear();
            Widget.GetComponentsInChildren(true, _graphics);
            // Include dormant native rectangles too. Unity's convenience bounds
            // query omits inactive descendants, which would briefly misplace a
            // capacitor the first time its charge event makes it visible.
            _bounds = new Bounds(Widget.rect.center, Widget.rect.size);
            if (_auxiliaryBounds != null)
                _bounds = _auxiliaryBounds.Measure(_graphics);
            else if (_compactWeapon)
            {
                // A disabled backing rectangle must not define the readout's
                // fit. Use its actual text/icon rectangles, including dormant
                // reload indicators so changing weapon state does not jump.
                var started = false;
                foreach (var graphic in _graphics)
                {
                    if (graphic == null || graphic == _background || graphic == _safety?.Source)
                        continue;
                    graphic.rectTransform.GetWorldCorners(_corners);
                    foreach (var corner in _corners)
                    {
                        var local = Widget.InverseTransformPoint(corner);
                        if (!started)
                        {
                            _bounds = new Bounds(local, Vector3.zero);
                            started = true;
                        }
                        else
                            _bounds.Encapsulate(local);
                    }
                }
            }
            else
                foreach (var node in _nodes)
                    if (node is RectTransform rect)
                    {
                        rect.GetWorldCorners(_corners);
                        foreach (var corner in _corners)
                            _bounds.Encapsulate(Widget.InverseTransformPoint(corner));
                    }
            _nodes.Clear();
            Fit();
        }

        internal void Fit()
        {
            _visibility?.Apply(Parent != null && Parent.gameObject.activeInHierarchy ? 1 : 0);
            _safety?.Refresh();
            if (_background != null)
                _background.enabled = false;
            if (!Valid || _bounds.size.x < 1 || _bounds.size.y < 1)
                return;
            var factor = Mathf.Min(Slot.rect.width / _bounds.size.x, Slot.rect.height / _bounds.size.y);
            if (factor <= 0 || float.IsNaN(factor) || float.IsInfinity(factor))
                return;
            var scale = Vector3.one * factor;
            var position = -_bounds.center * factor;
            if (Widget.localScale != scale)
                Widget.localScale = scale;
            if (Widget.localPosition != position)
                Widget.localPosition = position;
        }

        internal bool HasContent
        {
            get
            {
                if (!Valid || !Parent.gameObject.activeInHierarchy || !Widget.gameObject.activeSelf)
                    return false;
                foreach (var graphic in _graphics)
                {
                    if (graphic == null || !graphic.enabled || graphic == _background || graphic == _safety?.Source)
                        continue;
                    if (_damage && graphic.color.a <= .001f)
                        continue;
                    if (graphic is TMP_Text tmp && string.IsNullOrWhiteSpace(tmp.text) ||
                        graphic is Text text && string.IsNullOrWhiteSpace(text.text))
                        continue;
                    var active = true;
                    for (var node = graphic.transform; node != null && node != Slot; node = node.parent)
                        if (!node.gameObject.activeSelf)
                        {
                            active = false;
                            break;
                        }
                    if (active)
                        return true;
                }
                return false;
            }
        }

        internal void Restore()
        {
            _layers.Restore();
            _nodes.Clear();
            _graphics.Clear();
            _safety?.Release();
            _safety = null;
            _visibility?.Release();
            _visibility = null;
            if (_background != null)
                _background.enabled = _backgroundEnabled;
            if (Widget == null || Widget.parent != Slot)
                return;
            if (Parent == null)
            {
                // Its original scene owner has died. Never retain a live native
                // component pointing at the previous aircraft in the VR rig.
                Widget.gameObject.SetActive(false);
                Destroy(Widget.gameObject);
                return;
            }
            Widget.SetParent(Parent, false);
            Widget.anchorMin = _anchorMin;
            Widget.anchorMax = _anchorMax;
            Widget.pivot = _pivot;
            Widget.sizeDelta = _size;
            Widget.localPosition = _position;
            Widget.localRotation = _rotation;
            Widget.localScale = _scale;
            Widget.SetSiblingIndex(Index);
            // Preserve the source's current activeSelf: native visibility may
            // legitimately change while a widget is moved or hidden.
        }
    }

    private static CockpitStatusSources? _instance;
    private readonly Source[] _sources =
    {
        new("weapons", "weaponPanel"), new("countermeasures", "countermeasuresBackground"), new("power", "PowerPanel"),
        new("damage", "StatusDisplay")
    };
    private FlightHud? _hud;
    private Aircraft? _aircraft;
    private float _nextFind;
    private readonly List<Lease> _restore = new();
    private readonly CockpitInstructionSources _instructions = new();
    private readonly CockpitAuxiliarySources _auxiliary = new();

    private void Awake() => _instance = this;

    internal static bool Owns(Transform source)
    {
        if (_instance == null || source == null)
            return false;
        if (_instance._auxiliary.Owns(source))
            return true;
        foreach (var item in _instance._sources)
            if (item.Lease?.Valid == true && (source == item.Lease.Widget || source.IsChildOf(item.Lease.Widget)))
                return true;
        return false;
    }

    internal static bool EmptiedStatusPanel(Transform panel)
    {
        if (_instance == null || panel == null)
            return false;
        // Hide only the old common container once all three known leaves have
        // safely moved. Never blanket-hide flight instruments or ThreatList.
        foreach (var source in _instance._sources)
        {
            // Aircraft damage lives in a separate native anchor. Its absence
            // must not resurrect the emptied weapons/CM/capacitor backplate.
            if (source.Id == "damage")
                continue;
            if (source.Lease?.Valid != true ||
                source.Lease.Parent != panel && !source.Lease.Parent.IsChildOf(panel))
                return false;
        }
        return true;
    }

    private void LateUpdate()
    {
        // Reports can outlive their aircraft for a frame. Keep the text leases
        // alive through recovery so old overhead instructions never reappear.
        _instructions.Update();
        GameManager.GetLocalAircraft(out var aircraft);
        var hud = SceneSingleton<FlightHud>.i;
        if (_aircraft != aircraft || _hud != hud)
        {
            Release();
            _aircraft = aircraft;
            _hud = hud;
        }
        if (!XRSettings.enabled || aircraft == null || hud == null || CockpitHudWidgets.CurrentAircraft != aircraft)
        {
            Release();
            return;
        }
        var discover = Time.unscaledTime >= _nextFind;
        if (discover)
            _nextFind = Time.unscaledTime + .5f;
        var markers = discover ? HudHierarchy.MarkerLayer : null;
        // Capture every sibling's original index before moving any of them.
        foreach (var source in _sources)
        {
            if (!CockpitHudWidgets.TryGetContent(source.Id, out var content))
            {
                source.Lease?.Restore();
                source.Lease = null;
                CockpitHudWidgets.SetHasContent(source.Id, false);
                continue;
            }
            if (source.Lease != null && (!source.Lease.Valid || source.Lease.Slot != content))
            {
                source.Lease.Restore();
                source.Lease = null;
            }
            if (discover && source.Lease == null && !source.Failed)
            {
                var widget = source.Id == "damage" ? FindDamage(hud.statusAnchor) : Find(hud.transform, source.Name, markers);
                if (widget != null)
                    source.Lease = new Lease(widget, content, source.Id == "weapons", source.Id == "damage");
            }
        }
        foreach (var source in _sources)
        {
            if (source.Lease is { Attached: false } lease)
            {
                try
                {
                    lease.Attach();
                }
                catch (Exception error)
                {
                    lease.Restore();
                    source.Lease = null;
                    source.Failed = true;
                    Debug.LogWarning("[NOVR] Keeping native " + source.Name + " after widget attachment failed: " + error.Message);
                }
            }
            if (discover)
                source.Lease?.Refresh();
            source.Lease?.Fit();
            CockpitHudWidgets.SetHasContent(source.Id, source.Lease?.HasContent == true);
        }
        _auxiliary.Update(hud, discover);
    }

    // Reading a name copies it out of the engine, so a search through the
    // marker layer allocated one string per unit marker. No status panel is
    // ever in there.
    private static RectTransform? Find(Transform parent, string name, Transform? skip = null)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (ReferenceEquals(child, skip))
                continue;
            if (child.name == name && child is RectTransform rect)
                return rect;
            var nested = Find(child, name);
            if (nested != null)
                return nested;
        }
        return null;
    }

    private static RectTransform? FindDamage(Transform parent)
    {
        if (parent == null)
            return null;
        // These are instantiated per aircraft with different prefab names.
        // Search only the live HUD anchor, never aircraft assets or previews.
        if (parent.GetComponent<StatusDisplay>() != null && parent is RectTransform rect)
            return rect;
        for (var i = 0; i < parent.childCount; i++)
        {
            var found = FindDamage(parent.GetChild(i));
            if (found != null)
                return found;
        }
        return null;
    }

    internal static void ReleaseAll() => _instance?.Release();
    // Recovery state is decided after this adapter's LateUpdate; the alert
    // owner calls this before rendering so no old overhead feed flashes back.
    internal static void RefreshInstructions() => _instance?._instructions.Update();
    private void Release()
    {
        _auxiliary.Release();
        _restore.Clear();
        foreach (var source in _sources)
            if (source.Lease != null)
                _restore.Add(source.Lease);
        _restore.Sort((a, b) => a.Index.CompareTo(b.Index));
        foreach (var lease in _restore)
            lease.Restore();
        _restore.Clear();
        foreach (var source in _sources)
        {
            source.Lease = null;
            source.Failed = false;
            CockpitHudWidgets.SetHasContent(source.Id, false);
        }
        _aircraft = null;
        _hud = null;
        _nextFind = 0;
    }
    private void OnDisable()
    {
        Release();
        _instructions.Release();
    }
    private void OnDestroy()
    {
        Release();
        _instructions.Release();
        if (_instance == this)
            _instance = null;
    }
}
