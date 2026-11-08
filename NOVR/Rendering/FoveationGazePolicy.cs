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

// Fixation jitter of a fraction of a degree moved the protected ellipse by a
// few pixels every frame, flipping 16-pixel tiles along its edge and making the
// boundary shimmer. Hold each eye's centre until the gaze leaves a one-tile box.
internal sealed class FoveationGazeStabilizer
{
    internal const float DeadbandPixels = 16f;
    private readonly float[] _x = new float[2], _y = new float[2];
    private readonly bool[] _held = new bool[2];

    internal void Reset() => _held[0] = _held[1] = false;

    internal void Stabilize(int eye, int width, int height, ref float x, ref float y)
    {
        if (eye < 0 || eye > 1 || width <= 0 || height <= 0 || float.IsNaN(x) || float.IsNaN(y))
            return;
        if (_held[eye] && Math.Abs(x - _x[eye]) * width < DeadbandPixels && Math.Abs(y - _y[eye]) * height < DeadbandPixels)
        {
            x = _x[eye];
            y = _y[eye];
            return;
        }
        _x[eye] = x;
        _y[eye] = y;
        _held[eye] = true;
    }
}

// Eye-in-head angular speed from consecutive gaze samples. Above saccade speed
// the protected region briefly widens, so the eyes land inside full-rate
// shading even though gaze input lags their arrival.
internal sealed class GazeSaccadeDetector
{
    internal const double SaccadeDegreesPerSecond = 120, HoldSeconds = .12, MaximumGapSeconds = .1;
    private double _x, _y, _z, _time = double.NaN, _until = double.NegativeInfinity;

    internal void Reset()
    {
        _time = double.NaN;
        _until = double.NegativeInfinity;
    }

    internal bool Update(float dx, float dy, float dz, double time)
    {
        var length = Math.Sqrt((double)dx * dx + (double)dy * dy + (double)dz * dz);
        if (!(length > 1e-6) || double.IsNaN(time) || double.IsInfinity(time) || double.IsInfinity(length))
        {
            Reset();
            return false;
        }
        double x = dx / length, y = dy / length, z = dz / length;
        var elapsed = time - _time;
        if (elapsed > 1e-4 && elapsed <= MaximumGapSeconds)
        {
            var dot = Math.Max(-1, Math.Min(1, x * _x + y * _y + z * _z));
            var speed = Math.Acos(dot) * 180 / Math.PI / elapsed;
            if (speed >= SaccadeDegreesPerSecond)
                _until = time + HoldSeconds;
        }
        else if (!(elapsed >= 0 && elapsed <= 1e-4))
            _until = double.NegativeInfinity; // a stale gap carries no motion history
        if (!(elapsed >= 0 && elapsed <= 1e-4))
        {
            _x = x; _y = y; _z = z; _time = time;
        }
        return time <= _until;
    }
}
