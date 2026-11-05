using TMPro;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

internal sealed class CockpitNotificationMirror
{
    internal readonly TMP_Text Source;
    internal bool Visible
    {
        get; private set;
    }
    private readonly TextMeshProUGUI[] _copies;
    private readonly HudGraphicVisibilityLease _hide;
    private readonly Vector2 _position, _size;
    private readonly int _fontSize;
    private readonly bool _feed;
    private float _textScale = float.NaN;
    private int _rows, _lines;
    private string? _last;
    internal CockpitNotificationMirror(TMP_Text source, Transform parent, Vector2 position, Vector2 size, int fontSize, bool feed = false)
    {
        Source = source;
        _hide = new HudGraphicVisibilityLease(source, true);
        _position = position;
        _size = size;
        _fontSize = fontSize;
        _feed = feed;
        _copies = new TextMeshProUGUI[feed ? 3 : 1];
        for (var i = 0; i < _copies.Length; i++)
        {
            var go = new GameObject("Compact notification", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            var copy = go.GetComponent<TextMeshProUGUI>();
            _copies[i] = copy;
            copy.font = source.font;
            copy.fontSharedMaterial = source.fontSharedMaterial;
            copy.text = "";
            copy.fontSize = fontSize;
            copy.richText = false;
            copy.raycastTarget = false;
            copy.alignment = TextAlignmentOptions.TopLeft;
            copy.enableWordWrapping = !feed;
            copy.overflowMode = TextOverflowModes.Ellipsis;
            copy.maxVisibleLines = feed ? 1 : 2;
            var height = size.y / _copies.Length;
            copy.rectTransform.sizeDelta = new Vector2(size.x, height);
            copy.rectTransform.anchoredPosition = position + new Vector2(0, size.y * .5f - height * (i + .5f));
        }
        ApplyTextScale(1);
    }
    internal void Refresh(bool active, bool show, bool feed, float opacity = 1, bool suppressNative = false, float textScale = 1)
    {
        ApplyTextScale(textScale);
        // MessageFeed uses TMP.SetText(StringBuilder), whose text getter can return
        // stale backing strings after its first read. Use the native refresh
        // snapshot and sample other text sources only once per update.
        var text = Source != null ? NativeNotificationText.Read(Source) : "";
        var visible = active && show && Source != null && Source.isActiveAndEnabled && !string.IsNullOrWhiteSpace(text);
        if (suppressNative)
            _hide.Apply();
        else
            _hide.Restore();
        Visible = visible;
        for (var i = 0; i < _copies.Length; i++)
            _copies[i].enabled = visible && i < _rows;
        if (!visible)
            return;
        if (_last != text)
        {
            _last = text;
            var compact = CockpitAlertMath.LatestFeed(text, feed ? _rows : _lines);
            var lines = feed ? compact.Split('\n') : new[] { compact };
            for (var i = 0; i < _copies.Length; i++)
                _copies[i].text = i < lines.Length ? lines[i] : "";
        }
        var color = feed ? new Color(.72f, .82f, .84f, .85f) : Source!.color;
        color.a *= opacity;
        foreach (var copy in _copies)
            copy.color = color;
    }
    private void ApplyTextScale(float value)
    {
        value = HudWidgetProfiles.SanitizeTextScale(value);
        if (_textScale == value)
            return;
        _textScale = value;
        var fontSize = CockpitTextPresentation.FontSize(_fontSize, value);
        _rows = _feed ? CockpitTextPresentation.Lines(_size.y, fontSize, _copies.Length) : 1;
        _lines = _feed ? 1 : CockpitTextPresentation.Lines(_size.y, fontSize, 2);
        var height = _size.y / _rows;
        for (var i = 0; i < _copies.Length; i++)
        {
            var copy = _copies[i];
            copy.fontSize = fontSize;
            copy.maxVisibleLines = _lines;
            copy.rectTransform.sizeDelta = new Vector2(_size.x, height);
            copy.rectTransform.anchoredPosition = _position + new Vector2(0, _size.y * .5f - height * (i + .5f));
        }
        // A row-count change must retain the newest entries, including when the
        // native feed did not change while the user dragged the size slider.
        _last = null;
    }
    internal void Release()
    {
        _hide.Release();
        foreach (var copy in _copies)
            if (copy != null)
                UnityEngine.Object.Destroy(copy.gameObject);
    }
}
