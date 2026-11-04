using System;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Shape changes the viewport, never its transform scale: native map terrain and
// symbols keep isotropic pixels while the window reveals a different extent.
internal static class HudWidgetShape
{
    internal static float Aspect(float value) => !Finite(value) || value <= 0 ? 1 : Math.Max(.25f, Math.Min(4, value));
    internal static Vector2 Factors(float aspect)
    {
        var width = (float)Math.Sqrt(Aspect(aspect));
        return new Vector2(width, 1 / width);
    }
    internal static Vector2 Size(Vector2 original, float aspect)
    {
        var factors = Factors(aspect);
        return new Vector2(original.x * factors.x, original.y * factors.y);
    }
    internal static void Normalize(ref float scale, ref float aspect, float fallbackScale)
    {
        aspect = Aspect(aspect);
        var factors = Factors(aspect);
        var min = HudWidgetProfiles.MinScale / Math.Min(factors.x, factors.y);
        var max = HudWidgetProfiles.MaxScale / Math.Max(factors.x, factors.y);
        scale = Math.Max(min, Math.Min(max, Finite(scale) ? scale : fallbackScale));
    }
    internal static float ResizeAxis(float initial, float start, float current, float previous)
    {
        var fallback = Finite(previous) && previous > 0 ? previous : initial;
        if (!Finite(start) || !Finite(current) || Math.Abs(start) < .025f || start * current <= 0)
            return fallback;
        return Math.Max(HudWidgetProfiles.MinScale, Math.Min(HudWidgetProfiles.MaxScale, initial * current / start));
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
