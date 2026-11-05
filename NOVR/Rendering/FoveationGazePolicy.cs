using System;

namespace NOVR.Rendering;

// Project a combined gaze direction relative to the HMD into each eye's own
// asymmetric frustum. Input uses Unity's +Z-forward tracking convention; the
// projection uses camera -Z and the rate texture uses top-left-origin Y.
internal static class FoveationGazePolicy
{
    internal static bool TryProject(float dx, float dy, float dz,
        float projectionX, float projectionY, float offsetX, float offsetY,
        out float x, out float y)
    {
        x = y = .5f;
        if (!Finite(dx) || !Finite(dy) || !Finite(dz) || dz <= .05f ||
            !Finite(projectionX) || !Finite(projectionY) || projectionX <= 0 || projectionY <= 0 ||
            !Finite(offsetX) || !Finite(offsetY)) return false;
        var projectedX = .5f * (1 - offsetX + projectionX * dx / dz);
        var projectedY = .5f * (1 + offsetY - projectionY * dy / dz);
        if (!Finite(projectedX) || !Finite(projectedY)) return false;
        // A ray can fall just outside one asymmetric eye. Protect its visible
        // edge rather than snapping back to the centre of that eye.
        x = Math.Max(0, Math.Min(1, projectedX));
        y = Math.Max(0, Math.Min(1, projectedY));
        return true;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
