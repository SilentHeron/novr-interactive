using System;

namespace NOVR.MotionControls;

// Enclose both arm segments plus a world-space clothing margin. Transforming
// that margin with the absolute matrix row remains conservative for rotation,
// nonuniform scale and mirrored rigs, without skinning mesh vertices each frame.
internal static class SleeveBoundsMath
{
    internal static bool Axis(float a, float b, float c, float rowX, float rowY, float rowZ,
        float radius, out float center, out float size)
    {
        center = size = 0;
        if (!Finite(a) || !Finite(b) || !Finite(c) || !Finite(rowX) || !Finite(rowY) ||
            !Finite(rowZ) || !Finite(radius) || radius <= 0) return false;
        var margin = radius * (Math.Abs(rowX) + Math.Abs(rowY) + Math.Abs(rowZ));
        var min = Math.Min(a, Math.Min(b, c));
        var max = Math.Max(a, Math.Max(b, c));
        center = min * .5f + max * .5f;
        size = max - min + margin * 2;
        return Finite(center) && Finite(size) && size > 0;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
