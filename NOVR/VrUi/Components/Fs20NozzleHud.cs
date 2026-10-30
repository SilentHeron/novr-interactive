using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

// Borrow the fitted HUD gauge, not the cockpit gauge or a copy of its logic.
// The native manager continues refreshing actual angle and commanded input.
internal sealed class Fs20NozzleHud
{
    private static readonly AccessTools.FieldRef<HUDAppManager, HUDApp[]> Apps = AccessTools.FieldRefAccess<HUDAppManager, HUDApp[]>("apps");
    private static readonly AccessTools.FieldRef<HUDAppManager, Aircraft> AircraftOf = AccessTools.FieldRefAccess<HUDAppManager, Aircraft>("aircraft");
    private static readonly AccessTools.FieldRef<NozzleGauge, INozzleGauge> SystemOf = AccessTools.FieldRefAccess<NozzleGauge, INozzleGauge>("system");
    private static readonly AccessTools.FieldRef<NozzleGauge, bool> Fade = AccessTools.FieldRefAccess<NozzleGauge, bool>("fade");
    private static readonly AccessTools.FieldRef<NozzleGauge, Image[]> Images = AccessTools.FieldRefAccess<NozzleGauge, Image[]>("fadeImages");
    private static readonly AccessTools.FieldRef<NozzleGauge, float[]> ImageAlphas = AccessTools.FieldRefAccess<NozzleGauge, float[]>("imageAlphas");
    private static readonly AccessTools.FieldRef<NozzleGauge, TextMeshProUGUI> Label = AccessTools.FieldRefAccess<NozzleGauge, TextMeshProUGUI>("label");
    private static readonly AccessTools.FieldRef<NozzleGauge, Transform> NozzlePivot = AccessTools.FieldRefAccess<NozzleGauge, Transform>("nozzleIcon");
    private static readonly AccessTools.FieldRef<NozzleGauge, Transform> InputPivot = AccessTools.FieldRefAccess<NozzleGauge, Transform>("inputPivot");
    private static readonly AccessTools.FieldRef<NozzleGauge, float> InputRange = AccessTools.FieldRefAccess<NozzleGauge, float>("inputRange");

    private readonly struct Layout
    {
        private readonly Vector2 _min, _max, _pivot, _size;
        private readonly Vector3 _position, _scale;
        private readonly Quaternion _rotation;
        internal Layout(RectTransform source)
        {
            _min = source.anchorMin; _max = source.anchorMax; _pivot = source.pivot;
            _size = source.sizeDelta; _position = source.anchoredPosition3D;
            _scale = source.localScale; _rotation = source.localRotation;
        }
        internal void Restore(RectTransform source)
        {
            source.anchorMin = _min; source.anchorMax = _max; source.pivot = _pivot;
            source.sizeDelta = _size; source.anchoredPosition3D = _position;
            source.localRotation = _rotation; source.localScale = _scale;
        }
    }

    private readonly struct GraphicState
    {
        internal readonly Graphic Graphic;
        private readonly bool _enabled;
        private readonly float _alpha;
        internal readonly float VisibleAlpha;
        internal GraphicState(Graphic graphic, float visibleAlpha)
        { Graphic = graphic; _enabled = graphic.enabled; _alpha = graphic.color.a; VisibleAlpha = visibleAlpha; }
        internal void Show()
        {
            if (Graphic == null) return;
            Graphic.enabled = true;
            var color = Graphic.color; color.a = VisibleAlpha; Graphic.color = color;
        }
        internal void Restore()
        {
            if (Graphic == null) return;
            Graphic.enabled = _enabled;
            var color = Graphic.color; color.a = _alpha; Graphic.color = color;
        }
    }

    private readonly List<GraphicState> _graphics = new();
    private NozzleGauge? _source;
    private HUDAppManager? _manager;
    private RectTransform? _anchor;
    private Transform? _parent;
    private CanvasGroup? _group;
    private Layout _layout;
    private int _sibling;
    private bool _fade;
    private Vector3 _baseScale;
    private readonly Vector3[] _corners = new Vector3[4];
    internal NozzleGauge? Source => _source;

    internal void Update(bool active, FlightHud? hud)
    {
        var manager = SceneSingleton<HUDAppManager>.i;
        NozzleGauge? candidate = null;
        if (active && hud != null && manager != null && manager.isActiveAndEnabled &&
            manager.transform.IsChildOf(hud.transform) && GameManager.GetLocalAircraft(out var aircraft) &&
            AircraftOf(manager) == aircraft)
        {
            var apps = Apps(manager);
            if (apps != null)
                foreach (var app in apps)
                    if (app is NozzleGauge gauge && gauge != null && gauge.gameObject.activeSelf &&
                        gauge.transform is RectTransform && SystemOf(gauge) != null && ImageAlphas(gauge) != null &&
                        ((gauge == _source && _anchor != null && gauge.transform.parent == _anchor) ||
                         gauge.transform.IsChildOf(manager.transform)))
                    { candidate = gauge; break; }
        }
        if (candidate != _source || manager != _manager ||
            (_source != null && (_anchor == null || _source.transform.parent != _anchor))) Restore();
        if (candidate == null || hud == null) { Restore(); return; }
        if (_source == null) Borrow(candidate, manager!, hud.GetHUDCenter());
        if (_source == null || _anchor == null || _group == null) return;

        // A stable right-side readout, independent of the hidden extras group.
        // Keep the existing HUD scale and brightness settings effective.
        var config = ModConfiguration.Instance;
        var scale = config.FocusedFlightHud.Value ? config.FlightHudScale.Value : 1f;
        _anchor.anchoredPosition3D = new Vector3(485, -118, 0) * scale;
        _source.transform.localScale = _baseScale * scale;
        _group.alpha = config.InstrumentBrightness.Value;
        Fade(_source) = false;
        foreach (var graphic in _graphics) graphic.Show();
    }

    private void Borrow(NozzleGauge source, HUDAppManager manager, Transform center)
    {
        var rect = (RectTransform)source.transform;
        if (Mathf.Max(rect.rect.width, rect.rect.height) < 1 || center == null) return;
        _source = source; _manager = manager; _parent = rect.parent;
        _sibling = rect.GetSiblingIndex(); _layout = new Layout(rect); _fade = Fade(source);
        var images = Images(source); var alphas = ImageAlphas(source);
        if (images != null)
            for (var i = 0; i < images.Length; i++)
                if (images[i] != null) _graphics.Add(new GraphicState(images[i], i < alphas.Length ? alphas[i] : images[i].color.a));
        var label = Label(source);
        if (label != null) _graphics.Add(new GraphicState(label, 1f));
        // The authored 100x100 root does not contain the label or the rotating
        // artwork. Keep its native scale and cap the complete sweep once, so
        // changing nozzle angle cannot make the gauge grow or shrink.
        var artwork = ArtworkSize(source);
        _baseScale = rect.localScale * Mathf.Min(1f, 120f / Mathf.Max(1f, artwork));

        var go = new GameObject("NOVR FS-20 nozzle HUD", typeof(RectTransform), typeof(CanvasGroup));
        go.layer = source.gameObject.layer;
        _anchor = (RectTransform)go.transform; _anchor.SetParent(center, false);
        _anchor.anchorMin = _anchor.anchorMax = _anchor.pivot = new Vector2(.5f, .5f);
        _anchor.sizeDelta = Vector2.zero;
        _group = go.GetComponent<CanvasGroup>();
        _group.blocksRaycasts = false; _group.interactable = false; _group.ignoreParentGroups = true;
        var size = rect.rect.size;
        rect.SetParent(_anchor, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition3D = Vector3.zero; rect.localRotation = Quaternion.identity;
    }

    private float ArtworkSize(NozzleGauge source)
    {
        var rect = (RectTransform)source.transform;
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var nozzle = NozzlePivot(source); var input = InputPivot(source);
        void Include(Vector3 local)
        {
            var point = Vector3.Scale(local, rect.localScale);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        foreach (var state in _graphics)
        {
            var graphic = state.Graphic;
            if (graphic == null) continue;
            graphic.rectTransform.GetWorldCorners(_corners);
            var pivot = nozzle != null && graphic.transform.IsChildOf(nozzle) ? nozzle :
                input != null && graphic.transform.IsChildOf(input) ? input : null;
            foreach (var corner in _corners)
            {
                if (pivot == null || pivot.parent == null) { Include(rect.InverseTransformPoint(corner)); continue; }
                var local = Vector3.Scale(pivot.InverseTransformPoint(corner), pivot.localScale);
                var range = pivot == nozzle ? -90f : InputRange(source);
                // Include the complete fitted FS-20 tilt range. The small margin
                // covers the extrema between samples without changing live pose.
                for (var i = 0; i <= 18; i++)
                    Include(rect.InverseTransformPoint(pivot.parent.TransformPoint(pivot.localPosition + Quaternion.Euler(0, 0, range * i / 18f) * local)));
            }
        }
        return float.IsInfinity(min.x) ? 1f : Mathf.Max(max.x - min.x, max.y - min.y) * 1.01f;
    }

    internal void Restore()
    {
        if (_source != null)
        {
            Fade(_source) = _fade;
            foreach (var graphic in _graphics) graphic.Restore();
            if (_source.transform.parent == _anchor)
            {
                if (_parent != null)
                {
                    var rect = (RectTransform)_source.transform;
                    rect.SetParent(_parent, false); _layout.Restore(rect); rect.SetSiblingIndex(_sibling);
                }
                else
                {
                    // A destroyed aircraft extras root no longer owns this widget.
                    _source.gameObject.SetActive(false);
                    Object.Destroy(_source.gameObject);
                }
            }
        }
        if (_group != null) { _group.alpha = 0; _group.enabled = false; }
        if (_anchor != null) Object.Destroy(_anchor.gameObject);
        _graphics.Clear(); _source = null; _manager = null; _parent = null; _anchor = null; _group = null;
    }
}
