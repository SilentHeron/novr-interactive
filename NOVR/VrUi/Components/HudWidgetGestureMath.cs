using System;

namespace NOVR.VrUi.SpecialBehavior;

// Resize from hand separation in widget-local space. Rotating the pair must not
// change its size, and crossing hands cannot invert the display.
internal static class HudWidgetGestureMath
{
    internal static int Corner(float x, float y, float z, float halfWidth, float halfHeight)
    {
        if (!Finite(x) || !Finite(y) || !Finite(z) || !Finite(halfWidth) || !Finite(halfHeight) ||
            halfWidth <= 0 || halfHeight <= 0) return -1;
        var dx = Math.Abs(x) - halfWidth;
        var dy = Math.Abs(y) - halfHeight;
        return dx * dx + dy * dy + z * z <= .07f * .07f
            ? (x >= 0 ? 1 : 0) | (y >= 0 ? 2 : 0) : -1;
    }
    internal static bool Opposite(int first, int second) => first >= 0 && first < 4 && second >= 0 && second < 4 && (first ^ second) == 3;
    internal static float Resize(float initial, float startX, float startY, float x, float y, float previous) =>
        Resize(initial, startX, startY, 0, x, y, 0, previous);
    internal static float Resize(float initial, float startX, float startY, float startZ, float x, float y, float z, float previous)
    {
        var fallback = Finite(previous) ? Bound(previous) : Finite(initial) ? Bound(initial) : 1;
        if (!Finite(initial) || initial <= 0 || !Finite(startX) || !Finite(startY) || !Finite(startZ) ||
            !Finite(x) || !Finite(y) || !Finite(z)) return fallback;
        var start = startX * startX + startY * startY + startZ * startZ;
        var current = x * x + y * y + z * z;
        if (!Finite(start) || start < .0025f || x * startX + y * startY + z * startZ <= 0 || !Finite(current)) return fallback;
        return Bound(initial * (float)Math.Sqrt(current / start));
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static float Bound(float value) => Math.Max(.35f, Math.Min(3, value));
}
