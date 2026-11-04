using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.SpecialBehavior;

// Borrow the actual minimized map, including its native clipping, zoom, icons
// and sensor updates. The full-screen map always takes its original hierarchy.
[DefaultExecutionOrder(115)]
internal sealed class CockpitMapWidget : MonoBehaviour
{
    private sealed class Lease
    {
        internal readonly DynamicMap Map;
        internal readonly RectTransform Root, Slot;
        internal CockpitMapNavigation? Navigation;
        private readonly Transform? _parent;
        private readonly int _index;
        private readonly Vector2 _anchorsMin, _anchorsMax, _pivot, _size;
        private readonly Vector3 _position, _scale;
        private readonly Quaternion _rotation;
        private readonly Image _terrain;
        private Sprite? _lastTerrainSprite;
        private readonly Material _material;
        private Color _nativeColor, _appliedColor;
        private bool _applied;
        private readonly Image? _forwardReference;
        private readonly bool _forwardEnabled;
        private readonly RectTransform? _background;
        private readonly Vector2 _backgroundSize, _backgroundBorder;
        private readonly float _nativeHeight;
        private readonly Dictionary<GameObject, int> _layers = new();
        private readonly List<Transform> _nodes = new();
        private readonly List<GameObject> _gone = new();

        internal Lease(DynamicMap map, RectTransform slot, Image terrain)
        {
            Map = map; Root = (RectTransform)map.transform; Slot = slot;
            _parent = Root.parent; _index = Root.GetSiblingIndex();
            _anchorsMin = Root.anchorMin; _anchorsMax = Root.anchorMax;
            _pivot = Root.pivot; _size = Root.sizeDelta;
            _position = Root.localPosition; _scale = Root.localScale; _rotation = Root.localRotation;
            _terrain = terrain; _lastTerrainSprite = terrain.sprite;
            _material = terrain.material; _nativeColor = terrain.color;
            _nativeHeight = Root.rect.height;
            _background = map.mapBackground != null ? map.mapBackground.transform as RectTransform : null;
            _backgroundSize = _background != null ? _background.sizeDelta : Vector2.zero;
            _backgroundBorder = _background != null ? _background.rect.size - Root.rect.size : Vector2.zero;
            // This arrow belongs to the old HUD anchor, outside DynamicMap.
            var forward = _parent != null ? _parent.Find("forwardRef") : null;
            _forwardReference = forward != null ? forward.GetComponent<Image>() : null;
            _forwardEnabled = _forwardReference != null && _forwardReference.enabled;
        }

        internal bool Valid => Map != null && Root != null && Slot != null && _parent != null && Root.parent == Slot;
        internal bool HasContent => Valid && Root.gameObject.activeSelf && _terrain != null && _terrain.sprite != null;

        internal void Attach()
        {
            var dimensions = Root.rect.size;
            Root.SetParent(Slot, false);
            Root.anchorMin = Root.anchorMax = new Vector2(.5f, .5f);
            Root.sizeDelta = dimensions;
            Root.localRotation = Quaternion.identity;
            if (_forwardReference != null) _forwardReference.enabled = false;
            // The native additive terrain is nearly black and Minimize disables
            // its backing plate. Alpha UI preserves the sprite's opaque black
            // ocean, so terrain relief has contrast over bright cockpit scenery.
            // Keep RectMask2D: no terrain is drawn outside the native viewport.
            _terrain.material = null!;
            RefreshLayers(); Update();
            Navigation = new CockpitMapNavigation(Map, Slot);
        }

        internal void Update()
        {
            if (!Valid) return;
            if (_nativeHeight > 0 && Slot.rect.width > 0 && Slot.rect.height > 0)
            {
                // Change the native clipping viewport, not the terrain/icon
                // aspect ratio. Every map child retains an isotropic scale.
                var dimensions = new Vector2(_nativeHeight * Slot.rect.width / Slot.rect.height, _nativeHeight);
                Root.sizeDelta = dimensions;
                if (_background != null) _background.sizeDelta = dimensions + _backgroundBorder;
                var factor = Slot.rect.height / _nativeHeight;
                Root.localScale = Vector3.one * factor;
                Root.localPosition = (Vector3)Slot.rect.center - (Vector3)Root.rect.center * factor;
            }
            if (_forwardReference != null) _forwardReference.enabled = false;
            if (_terrain == null) return;
            // A new terrain can arrive without replacing this map or aircraft.
            // Return to ownship instead of carrying a selected point from the
            // previous terrain. Keep the live sprite and lease during loading.
            var sprite = _terrain.sprite;
            if (sprite != _lastTerrainSprite)
            {
                _lastTerrainSprite = sprite;
                Navigation?.Deactivate();
            }
            // Native theme/map loading may update the color while borrowed.
            // Remember that new value, rather than multiplying our last result.
            if (!_applied || _terrain.color != _appliedColor) _nativeColor = _terrain.color;
            _appliedColor = TerrainTint(_nativeColor);
            if (_terrain.color != _appliedColor) _terrain.color = _appliedColor;
            _applied = true;
        }

        internal void RefreshLayers()
        {
            if (!Valid) return;
            _gone.Clear();
            foreach (var pair in _layers) if (pair.Key == null) _gone.Add(pair.Key!);
            foreach (var go in _gone) _layers.Remove(go);
            _gone.Clear(); _nodes.Clear();
            Root.GetComponentsInChildren(true, _nodes);
            foreach (var node in _nodes)
            {
                if (node == null) continue;
                var go = node.gameObject;
                if (!_layers.ContainsKey(go)) _layers.Add(go, go.layer);
                if (go.layer != Slot.gameObject.layer) go.layer = Slot.gameObject.layer;
            }
            _nodes.Clear();
        }

        internal void Restore()
        {
            Navigation?.Dispose(); Navigation = null;
            foreach (var pair in _layers) if (pair.Key != null) pair.Key.layer = pair.Value;
            _layers.Clear(); _nodes.Clear(); _gone.Clear();
            if (_terrain != null)
            {
                _terrain.material = _material;
                if (_applied && _terrain.color == _appliedColor) _terrain.color = _nativeColor;
            }
            if (_forwardReference != null) _forwardReference.enabled = _forwardEnabled;
            if (Root == null || Root.parent != Slot) return;
            if (_background != null) _background.sizeDelta = _backgroundSize;
            if (_parent == null)
            {
                Root.gameObject.SetActive(false); Destroy(Root.gameObject); return;
            }
            Root.SetParent(_parent, false); Root.SetSiblingIndex(_index);
            Root.anchorMin = _anchorsMin; Root.anchorMax = _anchorsMax;
            Root.pivot = _pivot; Root.sizeDelta = _size;
            Root.localPosition = _position; Root.localScale = _scale; Root.localRotation = _rotation;
            // Never override activeSelf: native map/game state owns visibility.
        }
    }

    private static CockpitMapWidget? _instance;
    private Lease? _lease;
    private DynamicMap? _map;
    private Aircraft? _aircraft;
    private float _nextLayers;
    private bool _failed;

    private void Awake() => _instance = this;
    internal static CockpitMapNavigation? Navigation => _instance?._lease?.Valid == true ? _instance._lease.Navigation : null;
    internal static RectTransform? Viewport => _instance?._lease?.Valid == true ? _instance._lease.Slot : null;
    internal static bool Available => _instance?._lease?.HasContent == true &&
        CockpitHudWidgets.IsVisible("map") && !CockpitHudWidgets.Editing && Navigation?.Valid == true;
    internal static void ReleaseAll() { if (_instance != null) _instance.Release(); }
    internal static bool Owns(Transform source) => _instance != null && _instance._lease?.Valid == true &&
        source != null && (source == _instance._lease.Root || source.IsChildOf(_instance._lease.Root));

    internal static Color TerrainTint(Color native)
    {
        // Retain theme hue, but lift the low-contrast terrain independently of
        // bright tactical icons. The source sprite still supplies all relief.
        var tint = Color.Lerp(native, Color.white, .35f);
        return new Color(Mathf.Clamp01(tint.r * 2), Mathf.Clamp01(tint.g * 2), Mathf.Clamp01(tint.b * 2), 1);
    }

    private void LateUpdate()
    {
        var map = SceneSingleton<DynamicMap>.i;
        GameManager.GetLocalAircraft(out var aircraft);
        if (_map != map || _aircraft != aircraft)
        { Release(); _map = map; _aircraft = aircraft; _failed = false; }
        if (!XRSettings.enabled || map == null || aircraft == null || DynamicMap.mapMaximized ||
            CockpitHudWidgets.CurrentAircraft != aircraft || !CockpitHudWidgets.TryGetContent("map", out var content))
        { Release(); return; }
        if (_lease != null && (!_lease.Valid || _lease.Slot != content)) Release();
        if (_lease == null && !_failed && map.gameObject.activeSelf && map.mapImage != null)
        {
            var terrain = map.mapImage.GetComponent<Image>();
            if (terrain == null || map.transform.parent == null) return;
            _lease = new Lease(map, content, terrain);
            try { _lease.Attach(); _nextLayers = Time.unscaledTime + .5f; }
            catch (Exception e)
            {
                Release(); _failed = true;
                Debug.LogWarning("[NOVR] Cockpit map unavailable; retaining native map: " + e.Message);
            }
        }
        if (_lease == null) return;
        if (Time.unscaledTime >= _nextLayers)
        { _nextLayers = Time.unscaledTime + .5f; _lease.RefreshLayers(); }
        _lease.Update();
        if (!Available) _lease.Navigation?.Deactivate();
        _lease.Navigation?.Tick();
        CockpitHudWidgets.SetHasContent("map", _lease.HasContent);
    }

    private void Release()
    {
        _lease?.Restore(); _lease = null;
        CockpitHudWidgets.SetHasContent("map", false);
    }
    private void OnDisable() => Release();
    private void OnDestroy() { Release(); if (_instance == this) _instance = null; }
}

[HarmonyPatch(typeof(DynamicMap), nameof(DynamicMap.Maximize))]
internal static class CockpitMapMaximizePatch
{
    // Restore before the native method changes parents, dimensions and state.
    [HarmonyPrefix] private static void Prefix() => CockpitMapWidget.ReleaseAll();
}
