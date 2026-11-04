using NuclearOption.UIStyleSystem;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.MotionControls;

// At the unchanged 3 m depth / .001 canvas scale, the default 60-unit circle
// spans about 1.15 degrees. Appearance never changes the actual selection ray.
internal sealed class GazeRingGraphic : MaskableGraphic
{
    private GazeCueShape _shape;
    internal static GameObject Create(Transform parent)
    {
        var go = new GameObject("Look direction cue", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        // A small visual-only drop: 26.2 mm at 3 m is about half a degree.
        // Leave the native designator, selection ray and optical camera untouched.
        rect.anchoredPosition = new Vector2(0, -26.2f);
        rect.sizeDelta = Vector2.one * GazeCueSettings.Diameter;
        var ring = go.AddComponent<GazeRingGraphic>();
        ring.RefreshAppearance(); ring.raycastTarget = false;
        return go;
    }

    private void LateUpdate() => RefreshAppearance();

    private void RefreshAppearance()
    {
        var settings = ModConfiguration.Instance.LookCue;
        var size = Vector2.one * (GazeCueSettings.Diameter * settings.Scale);
        if (rectTransform.sizeDelta != size) rectTransform.sizeDelta = size;
        var shape = settings.CurrentShape;
        if (_shape != shape) { _shape = shape; SetVerticesDirty(); }
        // Match the game's live HUD theme, including theme changes. Contextual
        // acquisition brightening stays on the parent. The idle cue should
        // remain readable against both the sky and cockpit.
        var tint = ThemeManager.Active != null ? ThemeManager.Active.ColorTheme.AllClear : new Color(.2f, 1f, .35f);
        tint.a = settings.Alpha * ModConfiguration.Instance.InstrumentBrightness.Value;
        if (color != tint) color = tint;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = rectTransform.rect;
        var radius = Mathf.Min(rect.width, rect.height) * .5f;
        if (radius <= 0) return;
        var rim = new Color(0, 0, 0, color.a * .65f);
        switch (_shape)
        {
            case GazeCueShape.Dot:
                Disc(vh, rect.center, radius * .24f, rim);
                Disc(vh, rect.center, radius * .18f, color);
                break;
            case GazeCueShape.Crosshair:
                Crosshair(vh, rect.center, radius, .14f, rim);
                Crosshair(vh, rect.center, radius, .08f, color);
                break;
            case GazeCueShape.Brackets:
                Brackets(vh, rect.center, radius, .14f, rim);
                Brackets(vh, rect.center, radius, .08f, color);
                break;
            default:
                // Keep the existing default mesh, proportions and rim intact.
                Ring(vh, rect.center, radius * .83f, radius, rim);
                Ring(vh, rect.center, radius * .87f, radius * .97f, color);
                break;
        }
    }

    private static void Disc(VertexHelper vh, Vector2 center, float radius, Color tint)
    {
        const int segments = 64;
        var start = vh.currentVertCount;
        vh.AddVert(center, tint, Vector2.zero);
        for (var i = 0; i <= segments; i++)
        {
            var angle = i * Mathf.PI * 2 / segments;
            vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
            if (i > 0) vh.AddTriangle(start, start + i + 1, start + i);
        }
    }
    private static void Crosshair(VertexHelper vh, Vector2 c, float r, float width, Color tint)
    {
        var half = width * r * .5f;
        Quad(vh, c, -.88f*r, -half, -.28f*r, half, tint);
        Quad(vh, c, .28f*r, -half, .88f*r, half, tint);
        Quad(vh, c, -half, -.88f*r, half, -.28f*r, tint);
        Quad(vh, c, -half, .28f*r, half, .88f*r, tint);
    }
    private static void Brackets(VertexHelper vh, Vector2 c, float r, float width, Color tint)
    {
        var half = width * r * .5f;
        for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            {
                var cx = x * .72f * r; var cy = y * .72f * r;
                var innerX = x * .32f * r; var innerY = y * .32f * r;
                Quad(vh, c, Mathf.Min(cx, innerX) - half, cy - half, Mathf.Max(cx, innerX) + half, cy + half, tint);
                Quad(vh, c, cx - half, Mathf.Min(cy, innerY) - half, cx + half, Mathf.Max(cy, innerY) + half, tint);
            }
    }
    private static void Quad(VertexHelper vh, Vector2 c, float x0, float y0, float x1, float y1, Color tint)
    {
        var start = vh.currentVertCount;
        vh.AddVert(c + new Vector2(x0, y0), tint, Vector2.zero);
        vh.AddVert(c + new Vector2(x0, y1), tint, Vector2.zero);
        vh.AddVert(c + new Vector2(x1, y1), tint, Vector2.zero);
        vh.AddVert(c + new Vector2(x1, y0), tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
    }

    private static void Ring(VertexHelper vh, Vector2 center, float inner, float outer, Color color)
    {
        const int segments = 64;
        var start = vh.currentVertCount;
        for (var i = 0; i <= segments; i++)
        {
            var angle = i * Mathf.PI * 2 / segments;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            vh.AddVert(center + direction * inner, color, Vector2.zero);
            vh.AddVert(center + direction * outer, color, Vector2.zero);
            if (i == segments) continue;
            var index = start + i * 2;
            vh.AddTriangle(index, index + 2, index + 1);
            vh.AddTriangle(index + 1, index + 2, index + 3);
        }
    }
}
