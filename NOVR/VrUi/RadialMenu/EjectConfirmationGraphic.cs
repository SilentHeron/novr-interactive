using UnityEngine;
using UnityEngine.UI;

namespace NOVR.MotionControls;

// Kept on the hand wheel's existing canvas. The native icon is borrowed, not
// cloned or modified, and the small ring mesh changes only during confirmation.
internal sealed class EjectConfirmationGraphic : MaskableGraphic
{
    private static readonly Color Red = new(1f, .16f, .12f, 1f);
    private Image? _icon;
    private float _progress;
    internal float Progress
    {
        set
        {
            var next = Mathf.Clamp01(value);
            if (_progress == next)
                return;
            _progress = next;
            SetVerticesDirty();
        }
    }

    internal void Show(Sprite? nativeIcon)
    {
        if (_icon == null)
        {
            var go = new GameObject("Native eject symbol", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;
            _icon = go.GetComponent<Image>();
            _icon.raycastTarget = false;
            _icon.preserveAspect = true;
            _icon.color = Red;
            _icon.rectTransform.sizeDelta = new Vector2(48, 48);
        }
        _icon.sprite = nativeIcon;
        _icon.enabled = nativeIcon != null;
        _progress = 0;
        gameObject.SetActive(true);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var center = rectTransform.rect.center;
        var scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / 128f;
        if (scale <= 0)
            return;
        Arc(vh, center, 0, 63 * scale, 1, new Color(.025f, .015f, .015f, .9f));
        Arc(vh, center, 58 * scale, 61 * scale, 1, new Color(Red.r, Red.g, Red.b, .28f));
        // The guide remains visible before the hold. Its red arc fills clockwise
        // and grows outwards to meet the guide exactly when the hold completes.
        if (_progress > 0)
        {
            var radius = Mathf.Lerp(43, 61, _progress) * scale;
            Arc(vh, center, radius - 5 * scale, radius, _progress, Red);
        }
        if (_icon != null && _icon.enabled)
            return;
        // A recognizable eject glyph remains available if a mod has no icon.
        var start = vh.currentVertCount;
        vh.AddVert(center + new Vector2(-18, -2) * scale, Red, Vector2.zero);
        vh.AddVert(center + new Vector2(0, 20) * scale, Red, Vector2.zero);
        vh.AddVert(center + new Vector2(18, -2) * scale, Red, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        start = vh.currentVertCount;
        vh.AddVert(center + new Vector2(-18, -15) * scale, Red, Vector2.zero);
        vh.AddVert(center + new Vector2(-18, -8) * scale, Red, Vector2.zero);
        vh.AddVert(center + new Vector2(18, -8) * scale, Red, Vector2.zero);
        vh.AddVert(center + new Vector2(18, -15) * scale, Red, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private static void Arc(VertexHelper vh, Vector2 center, float inner, float outer, float fill, Color tint)
    {
        var segments = Mathf.Max(1, Mathf.CeilToInt(80 * fill));
        var start = vh.currentVertCount;
        for (var i = 0; i <= segments; i++)
        {
            var angle = (float)i / segments * fill * Mathf.PI * 2;
            var direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            vh.AddVert(center + direction * inner, tint, Vector2.zero);
            vh.AddVert(center + direction * outer, tint, Vector2.zero);
            if (i == segments)
                continue;
            var index = start + i * 2;
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index + 1, index + 3, index + 2);
        }
    }
}
