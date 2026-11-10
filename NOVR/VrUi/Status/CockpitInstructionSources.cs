using System;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

// Keep the native action queue and navigation marker geometry in place. Only
// their text is presented here: timers, progress and runway state remain native.
internal sealed class CockpitInstructionSources
{
    private static readonly FieldInfo? ActionsText = AccessTools.Field(typeof(AircraftActionsReport), "actionsText");
    private static readonly FieldInfo? ActionsBackground = AccessTools.Field(typeof(AircraftActionsReport), "background");
    private static readonly FieldInfo? AirbaseText = AccessTools.Field(typeof(AirbaseOverlay), "airbaseLabel");

    private sealed class TextLeaf
    {
        internal readonly TMP_Text Source;
        private readonly HudGraphicVisibilityLease _hide;
        private TextMeshProUGUI? _copy;
        private RectTransform? _slot;
        private int _fontSize, _lines;
        private float _height;
        private string? _last;
        internal TextLeaf(TMP_Text source)
        {
            Source = source;
            _hide = new HudGraphicVisibilityLease(source, true);
        }
        internal bool HasText => Source != null && Source.isActiveAndEnabled && !string.IsNullOrWhiteSpace(Source.text);
        internal bool Taxi => Source != null && Source.text.StartsWith("Taxi to ", StringComparison.Ordinal);
        internal void Bind(RectTransform slot, Vector2 position, Vector2 size, int fontSize, int lines)
        {
            if (_slot == slot && _copy != null)
            {
                SetBounds(position, size);
                return;
            }
            if (_copy != null)
                UnityEngine.Object.Destroy(_copy.gameObject);
            _slot = slot;
            _last = null;
            var go = new GameObject("Compact aircraft instruction", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(slot, false);
            go.layer = slot.gameObject.layer;
            _copy = go.GetComponent<TextMeshProUGUI>();
            _copy.font = Source.font;
            _copy.fontSharedMaterial = Source.fontSharedMaterial;
            _copy.fontSize = fontSize;
            _copy.richText = true;
            _copy.raycastTarget = false;
            _copy.enableWordWrapping = true;
            _copy.overflowMode = TextOverflowModes.Ellipsis;
            _copy.alignment = TextAlignmentOptions.TopLeft;
            _copy.maxVisibleLines = lines;
            _fontSize = fontSize;
            _lines = lines;
            SetBounds(position, size);
        }
        private void SetBounds(Vector2 position, Vector2 size)
        {
            if (_copy == null)
                return;
            _height = size.y;
            if (_copy.rectTransform.anchoredPosition != position)
                _copy.rectTransform.anchoredPosition = position;
            if (_copy.rectTransform.sizeDelta != size)
                _copy.rectTransform.sizeDelta = size;
        }
        internal bool Refresh(bool hideNative, bool showCopy, float textScale)
        {
            if (hideNative)
                _hide.Apply();
            else
                _hide.Restore();
            var visible = showCopy && HasText;
            if (_copy != null)
            {
                var fontSize = CockpitTextPresentation.FontSize(_fontSize, textScale);
                if (_copy.fontSize != fontSize)
                    _copy.fontSize = fontSize;
                var lines = CockpitTextPresentation.Lines(_height, fontSize, _lines);
                if (_copy.maxVisibleLines != lines)
                    _copy.maxVisibleLines = lines;
                _copy.enabled = visible;
                if (visible)
                {
                    if (_last != Source.text)
                    {
                        _last = Source.text;
                        _copy.text = CockpitTextPresentation.InstructionText(_last);
                    }
                    _copy.color = Source.color;
                }
            }
            return visible && _copy != null;
        }
        internal void Release()
        {
            _hide.Release();
            if (_copy != null)
                UnityEngine.Object.Destroy(_copy.gameObject);
            _copy = null;
            _slot = null;
        }
    }

    private TextLeaf? _actions, _taxi;
    private Image? _background;
    private HudGraphicVisibilityLease? _backgroundHide;
    private float _nextFind;

    internal void Update()
    {
        if (Time.unscaledTime >= _nextFind)
        {
            _nextFind = Time.unscaledTime + .5f;
            var report = SceneSingleton<AircraftActionsReport>.i;
            SetLeaf(ref _actions, report != null ? ActionsText?.GetValue(report) as TMP_Text : null);
            var background = report != null ? ActionsBackground?.GetValue(report) as Image : null;
            if (background != _background || background == null && _backgroundHide != null)
            {
                _backgroundHide?.Release();
                _background = background;
                _backgroundHide = background == null ? null : new HudGraphicVisibilityLease(background);
            }
            var hud = SceneSingleton<FlightHud>.i;
            var airbase = hud != null ? FindAirbase(hud.transform) : null;
            SetLeaf(ref _taxi, airbase != null ? AirbaseText?.GetValue(airbase) as TMP_Text : null);
        }

        var owned = CockpitHudWidgets.TryGetContent("objectives", out var slot);
        var cockpit = owned && CockpitHudWidgets.Instance?.Available == true;
        var recovery = CockpitAlerts.InRecovery;
        var visible = cockpit && CockpitHudWidgets.IsVisible("objectives");
        var textScale = CockpitHudWidgets.GetTextScale("objectives");
        if (cockpit)
        {
            // Keep both regions within the original frame. At larger sizes the
            // taxi row grows inward and the action region shows fewer lines.
            var taxiHeight = Mathf.Max(36, CockpitTextPresentation.LineHeight(CockpitTextPresentation.FontSize(26, textScale)));
            _taxi?.Bind(slot, new Vector2(0, 75 - taxiHeight * .5f), new Vector2(408, taxiHeight), 26, 1);
            _actions?.Bind(slot, new Vector2(0, -1 - taxiHeight * .5f), new Vector2(408, 148 - taxiHeight), 23, 4);
        }
        var taxi = _taxi?.Taxi == true;
        var hasTaxi = _taxi?.Refresh(recovery || cockpit && taxi, visible && taxi, textScale) == true;
        var hasActions = _actions?.Refresh(recovery || cockpit, visible, textScale) == true;
        // This is the report's own black rectangle, not any HUD/map container.
        if (recovery || cockpit)
            _backgroundHide?.Apply();
        else
            _backgroundHide?.Restore();
        CockpitHudWidgets.SetHasContent("objectives", hasTaxi || hasActions);
    }

    private static AirbaseOverlay? FindAirbase(Transform parent)
    {
        var own = parent.GetComponent<AirbaseOverlay>();
        if (own != null)
            return own;
        for (var i = 0; i < parent.childCount; i++)
        {
            var nested = FindAirbase(parent.GetChild(i));
            if (nested != null)
                return nested;
        }
        return null;
    }
    private static void SetLeaf(ref TextLeaf? leaf, TMP_Text? source)
    {
        // Destroyed Unity objects compare equal to null; release their draw
        // callbacks instead of retaining an invalid leaf until another spawn.
        if (leaf == null && source == null || leaf != null && source != null && leaf.Source == source)
            return;
        leaf?.Release();
        leaf = source == null ? null : new TextLeaf(source);
    }
    internal void Release()
    {
        _actions?.Release();
        _actions = null;
        _taxi?.Release();
        _taxi = null;
        _backgroundHide?.Release();
        _backgroundHide = null;
        _background = null;
        _nextFind = 0;
    }
}
