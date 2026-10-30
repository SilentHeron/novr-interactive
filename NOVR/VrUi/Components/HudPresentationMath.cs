using System;

namespace NOVR.VrUi.SpecialBehavior;

internal static class HudPresentationMath
{
    internal static float ScaleCoordinate(float coordinate, float anchor, float scale) => anchor + (coordinate - anchor) * scale;
    internal static float Project(float coordinate, float depth, float distance, float magnification) =>
        coordinate / Math.Max(.1f, depth) * distance * 1000 * magnification;
    internal static float Symbol(float vertexProjection, float anchorProjection, float magnification) =>
        anchorProjection + (vertexProjection - anchorProjection) / Math.Max(1, magnification);
    internal static float WidgetFit(float width, float height, float userScale) =>
        Math.Min(180 * userScale / .5f / Math.Max(1, width), 350 * userScale / .5f / Math.Max(1, height));
    internal static float PanelVertex(float vertex, float partCenter, float aspect, bool stretch) =>
        stretch ? vertex * aspect : vertex + partCenter * (aspect - 1);
}
