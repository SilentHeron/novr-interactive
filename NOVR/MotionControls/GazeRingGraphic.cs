using NuclearOption.UIStyleSystem;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.MotionControls;

// Both gaze views use the same angular cue. At their 3 m stereo depth and
// 0.001 canvas scale, 82 units spans about 1.6 degrees. Keep the center completely
// empty, with a dark rim for contrast against bright skies.
internal sealed class GazeRingGraphic : MaskableGraphic
{
    internal static GameObject Create(Transform parent)
    {
        var go = new GameObject("Hollow gaze circle", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        // A small visual-only drop: 26.2 mm at 3 m is about half a degree.
        // Leave the native designator, selection ray and optical camera untouched.
        rect.anchoredPosition = new Vector2(0, -26.2f);
        rect.sizeDelta = Vector2.one * 82;
        var ring = go.AddComponent<GazeRingGraphic>();
        ring.RefreshColor(); ring.raycastTarget = false;
        return go;
    }

    private void LateUpdate() => RefreshColor();

    private void RefreshColor()
    {
        // Match the game's live HUD theme, including theme changes. Contextual
        // acquisition brightening stays on the parent. The idle cue should
        // remain readable against both the sky and cockpit.
        var tint = ThemeManager.Active != null ? ThemeManager.Active.ColorTheme.AllClear : new Color(.2f, 1f, .35f);
        tint.a = .78f * ModConfiguration.Instance.InstrumentBrightness.Value;
        if (color != tint) color = tint;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var rect = rectTransform.rect;
        var radius = Mathf.Min(rect.width, rect.height) * .5f;
        if (radius <= 0) return;
        Ring(vh, rect.center, radius * .83f, radius, new Color(0, 0, 0, color.a * .65f));
        Ring(vh, rect.center, radius * .87f, radius * .97f, color);
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
