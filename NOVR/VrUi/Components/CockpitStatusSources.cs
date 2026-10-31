using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.SpecialBehavior;

// Borrow only the game's live leaf widgets. WeaponStatus, CombatHUD and
// ChargeIndicator retain their own events, visibility, reloads and theme updates.
[DefaultExecutionOrder(110)]
internal sealed class CockpitStatusSources : MonoBehaviour
{
    private sealed class Source
    {
        internal readonly string Id, Name;
        internal Lease? Lease;
        internal bool Failed;
        internal Source(string id, string name) { Id = id; Name = name; }
    }

    private sealed class Lease
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
        private CanvasGroup? _visibility;
        private readonly Dictionary<GameObject, int> _layers = new();
        private readonly List<Transform> _nodes = new();
        private readonly List<Graphic> _graphics = new();
        private readonly List<GameObject> _gone = new();
        private readonly Vector3[] _corners = new Vector3[4];
        private Bounds _bounds;

        internal Lease(RectTransform widget, RectTransform slot)
        {
            Widget = widget; Slot = slot; Parent = widget.parent;
            Index = widget.GetSiblingIndex(); _position = widget.localPosition;
            _scale = widget.localScale; _rotation = widget.localRotation;
            _anchorMin = widget.anchorMin; _anchorMax = widget.anchorMax;
            _pivot = widget.pivot; _size = widget.sizeDelta;
            _background = widget.GetComponent<Image>();
            _backgroundEnabled = _background != null && _background.enabled;
        }

        internal void Attach()
        {
            var widget = Widget; var dimensions = widget.rect.size;
            if (_background != null) _background.enabled = false;
            _visibility = widget.gameObject.AddComponent<CanvasGroup>();
            _visibility.interactable = false; _visibility.blocksRaycasts = false;
            // Do not activate the source: the capacitor deliberately remains
            // dormant until the native power-supply event makes it relevant.
            widget.SetParent(Slot, false);
            widget.anchorMin = widget.anchorMax = new Vector2(.5f, .5f);
            widget.sizeDelta = dimensions;
            widget.localRotation = Quaternion.identity;
            Refresh();
            Attached = true;
        }

        internal bool Valid => Widget != null && Slot != null && Parent != null && Widget.parent == Slot;

        internal void Refresh()
        {
            if (!Valid) return;
            _gone.Clear();
            foreach (var pair in _layers) if (pair.Key == null) _gone.Add(pair.Key!);
            foreach (var key in _gone) _layers.Remove(key);
            _gone.Clear(); _nodes.Clear();
            Widget.GetComponentsInChildren(true, _nodes);
            foreach (var node in _nodes)
            {
                if (node == null) continue;
                var go = node.gameObject;
                if (!_layers.ContainsKey(go)) _layers.Add(go, go.layer);
                if (go.layer != Slot.gameObject.layer) go.layer = Slot.gameObject.layer;
            }
            // Include dormant native rectangles too. Unity's convenience bounds
            // query omits inactive descendants, which would briefly misplace a
            // capacitor the first time its charge event makes it visible.
            _bounds = new Bounds(Widget.rect.center, Widget.rect.size);
            foreach (var node in _nodes)
                if (node is RectTransform rect)
                {
                    rect.GetWorldCorners(_corners);
                    foreach (var corner in _corners) _bounds.Encapsulate(Widget.InverseTransformPoint(corner));
                }
            _nodes.Clear(); _graphics.Clear();
            Widget.GetComponentsInChildren(true, _graphics);
            Fit();
        }

        internal void Fit()
        {
            if (_visibility != null) _visibility.alpha = Parent != null && Parent.gameObject.activeInHierarchy ? 1 : 0;
            if (_background != null) _background.enabled = false;
            if (!Valid || _bounds.size.x < 1 || _bounds.size.y < 1) return;
            var factor = Mathf.Min(Slot.rect.width / _bounds.size.x, Slot.rect.height / _bounds.size.y);
            if (factor <= 0 || float.IsNaN(factor) || float.IsInfinity(factor)) return;
            var scale = Vector3.one * factor;
            var position = -_bounds.center * factor;
            if (Widget.localScale != scale) Widget.localScale = scale;
            if (Widget.localPosition != position) Widget.localPosition = position;
        }

        internal bool HasContent
        {
            get
            {
                if (!Valid || !Parent.gameObject.activeInHierarchy || !Widget.gameObject.activeSelf) return false;
                foreach (var graphic in _graphics)
                {
                    if (graphic == null || !graphic.enabled || graphic == _background) continue;
                    if (graphic is TMP_Text tmp && string.IsNullOrWhiteSpace(tmp.text) ||
                        graphic is Text text && string.IsNullOrWhiteSpace(text.text)) continue;
                    var active = true;
                    for (var node = graphic.transform; node != null && node != Slot; node = node.parent)
                        if (!node.gameObject.activeSelf) { active = false; break; }
                    if (active) return true;
                }
                return false;
            }
        }

        internal void Restore()
        {
            foreach (var pair in _layers) if (pair.Key != null) pair.Key.layer = pair.Value;
            _layers.Clear(); _nodes.Clear(); _graphics.Clear(); _gone.Clear();
            if (_visibility != null) { _visibility.alpha = 1; Destroy(_visibility); _visibility = null; }
            if (_background != null) _background.enabled = _backgroundEnabled;
            if (Widget == null || Widget.parent != Slot) return;
            if (Parent == null)
            {
                // Its original scene owner has died. Never retain a live native
                // component pointing at the previous aircraft in the VR rig.
                Widget.gameObject.SetActive(false); Destroy(Widget.gameObject); return;
            }
            Widget.SetParent(Parent, false);
            Widget.anchorMin = _anchorMin; Widget.anchorMax = _anchorMax;
            Widget.pivot = _pivot; Widget.sizeDelta = _size;
            Widget.localPosition = _position; Widget.localRotation = _rotation; Widget.localScale = _scale;
            Widget.SetSiblingIndex(Index);
            // Preserve the source's current activeSelf: native visibility may
            // legitimately change while a widget is moved or hidden.
        }
    }

    private static CockpitStatusSources? _instance;
    private readonly Source[] _sources =
    {
        new("weapons", "weaponPanel"), new("countermeasures", "countermeasuresBackground"), new("power", "PowerPanel")
    };
    private FlightHud? _hud;
    private Aircraft? _aircraft;
    private float _nextFind;
    private readonly List<Lease> _restore = new();

    private void Awake() => _instance = this;

    internal static bool Owns(Transform source)
    {
        if (_instance == null || source == null) return false;
        foreach (var item in _instance._sources)
            if (item.Lease?.Valid == true && (source == item.Lease.Widget || source.IsChildOf(item.Lease.Widget))) return true;
        return false;
    }

    internal static bool EmptiedStatusPanel(Transform panel)
    {
        if (_instance == null || panel == null) return false;
        // Hide only the old common container once all three known leaves have
        // safely moved. Never blanket-hide flight instruments or ThreatList.
        foreach (var source in _instance._sources)
            if (source.Lease?.Valid != true ||
                source.Lease.Parent != panel && !source.Lease.Parent.IsChildOf(panel)) return false;
        return true;
    }

    private void LateUpdate()
    {
        GameManager.GetLocalAircraft(out var aircraft);
        var hud = SceneSingleton<FlightHud>.i;
        if (_aircraft != aircraft || _hud != hud) { Release(); _aircraft = aircraft; _hud = hud; }
        if (!XRSettings.enabled || aircraft == null || hud == null || CockpitHudWidgets.CurrentAircraft != aircraft)
        { Release(); return; }
        var discover = Time.unscaledTime >= _nextFind;
        if (discover) _nextFind = Time.unscaledTime + .5f;
        // Capture every sibling's original index before moving any of them.
        foreach (var source in _sources)
        {
            if (!CockpitHudWidgets.TryGetContent(source.Id, out var content))
            { source.Lease?.Restore(); source.Lease = null; CockpitHudWidgets.SetHasContent(source.Id, false); continue; }
            if (source.Lease != null && (!source.Lease.Valid || source.Lease.Slot != content))
            { source.Lease.Restore(); source.Lease = null; }
            if (discover && source.Lease == null && !source.Failed)
            {
                var widget = Find(hud.transform, source.Name);
                if (widget != null) source.Lease = new Lease(widget, content);
            }
        }
        foreach (var source in _sources)
        {
            if (source.Lease is { Attached: false } lease)
            {
                try { lease.Attach(); }
                catch (Exception error)
                {
                    lease.Restore(); source.Lease = null; source.Failed = true;
                    Debug.LogWarning("[NOVR] Keeping native " + source.Name + " after widget attachment failed: " + error.Message);
                }
            }
            if (discover) source.Lease?.Refresh();
            source.Lease?.Fit();
            CockpitHudWidgets.SetHasContent(source.Id, source.Lease?.HasContent == true);
        }
    }

    private static RectTransform? Find(Transform parent, string name)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name && child is RectTransform rect) return rect;
            var nested = Find(child, name); if (nested != null) return nested;
        }
        return null;
    }

    internal static void ReleaseAll() => _instance?.Release();
    private void Release()
    {
        _restore.Clear();
        foreach (var source in _sources) if (source.Lease != null) _restore.Add(source.Lease);
        _restore.Sort((a, b) => a.Index.CompareTo(b.Index));
        foreach (var lease in _restore) lease.Restore();
        _restore.Clear();
        foreach (var source in _sources)
        {
            source.Lease = null; source.Failed = false;
            CockpitHudWidgets.SetHasContent(source.Id, false);
        }
        _aircraft = null; _hud = null; _nextFind = 0;
    }
    private void OnDisable() => Release();
    private void OnDestroy() { Release(); if (_instance == this) _instance = null; }
}
