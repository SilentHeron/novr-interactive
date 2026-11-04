using System;

namespace NOVR.VrUi.SpecialBehavior;

// Resize from hand separation in widget-local space. Rotating the pair must not
// change its size, and crossing hands cannot invert the display.
internal static class HudWidgetGestureMath
{
    internal static int Corner(float x, float y, float z, float halfWidth, float halfHeight)
        => Corner(x, y, z, halfWidth, halfHeight, .09f);
    private static int Corner(float x, float y, float z, float halfWidth, float halfHeight, float radius)
    {
        if (!Valid(x, y, z, halfWidth, halfHeight) || !AtEdge(x, y, halfWidth, halfHeight))
            return -1;
        var dx = Math.Abs(x) - halfWidth;
        var dy = Math.Abs(y) - halfHeight;
        return dx * dx + dy * dy + z * z <= radius * radius
            ? (x >= 0 ? 1 : 0) | (y >= 0 ? 2 : 0) : -1;
    }
    internal static int PairCorner(int firstCorner, float firstX, float firstY, float firstZ,
        float x, float y, float z, float halfWidth, float halfHeight, out int promotedFirst)
    {
        promotedFirst = -1;
        if (firstCorner < -1 || firstCorner > 3 || !Valid(firstX, firstY, firstZ, halfWidth, halfHeight) || !Valid(x, y, z, halfWidth, halfHeight) ||
            !AtEdge(firstX, firstY, halfWidth, halfHeight) || !AtEdge(x, y, halfWidth, halfHeight))
            return -1;
        var dx = x - firstX;
        var dy = y - firstY;
        var dz = z - firstZ;
        if (dx * dx + dy * dy + dz * dz < .05f * .05f)
            return -1;
        // Prefer the already-held diagonal. Enlarging this one affordance does
        // not enlarge every neighboring panel's acquisition zone.
        if (firstCorner >= 0 && NearCorner(firstCorner, firstX, firstY, firstZ, halfWidth, halfHeight, .35f) &&
            NearCorner(firstCorner ^ 3, x, y, z, halfWidth, halfHeight))
        {
            promotedFirst = firstCorner;
            return firstCorner ^ 3;
        }
        var second = Corner(x, y, z, halfWidth, halfHeight, .12f);
        if (second < 0)
            return -1;
        var first = second ^ 3;
        if (!NearCorner(first, firstX, firstY, firstZ, halfWidth, halfHeight, .35f))
            return -1;
        // A body/edge grip had no corner identity. An edge-centered grip can
        // also have an arbitrary top/bottom (or left/right) identity; choose
        // its diagonal from the deliberate second grip without moving pose.
        var changed = firstCorner ^ first;
        if (firstCorner >= 0 && (firstCorner > 3 ||
            (changed & 1) != 0 && Math.Abs(firstX) > halfWidth * .35f ||
            (changed & 2) != 0 && Math.Abs(firstY) > halfHeight * .35f))
            return -1;
        promotedFirst = first;
        return second;
    }
    private static bool NearCorner(int corner, float x, float y, float z, float halfWidth, float halfHeight, float midlineTolerance = .25f)
    {
        var sx = (corner & 1) == 0 ? -1 : 1;
        var sy = (corner & 2) == 0 ? -1 : 1;
        if (sx * x < -halfWidth * midlineTolerance || sy * y < -halfHeight * midlineTolerance)
            return false;
        var dx = x - sx * halfWidth;
        var dy = y - sy * halfHeight;
        return dx * dx + dy * dy + z * z <= .12f * .12f;
    }
    private static bool AtEdge(float x, float y, float halfWidth, float halfHeight) =>
        Math.Abs(x) >= halfWidth * .45f || Math.Abs(y) >= halfHeight * .45f;
    private static bool Valid(float x, float y, float z, float halfWidth, float halfHeight) =>
        Finite(x) && Finite(y) && Finite(z) && Finite(halfWidth) && Finite(halfHeight) && halfWidth > 0 && halfHeight > 0;
    internal static bool Opposite(int first, int second) => first >= 0 && first < 4 && second >= 0 && second < 4 && (first ^ second) == 3;
    internal static float Resize(float initial, float startX, float startY, float x, float y, float previous) =>
        Resize(initial, startX, startY, 0, x, y, 0, previous);
    internal static float Resize(float initial, float startX, float startY, float startZ, float x, float y, float z, float previous)
    {
        var fallback = Finite(previous) ? Bound(previous) : Finite(initial) ? Bound(initial) : 1;
        if (!Finite(initial) || initial <= 0 || !Finite(startX) || !Finite(startY) || !Finite(startZ) ||
            !Finite(x) || !Finite(y) || !Finite(z))
            return fallback;
        var start = startX * startX + startY * startY + startZ * startZ;
        var current = x * x + y * y + z * z;
        if (!Finite(start) || start < .0025f || x * startX + y * startY + z * startZ <= 0 || !Finite(current))
            return fallback;
        return Bound(initial * (float)Math.Sqrt(current / start));
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static float Bound(float value) => Math.Max(.35f, Math.Min(3, value));
}
