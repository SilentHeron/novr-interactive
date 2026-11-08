using System;

namespace NOVR.Rendering;

internal enum FixedFoveationLevel
{
    Off = 0,
    Mild = 1,
    Balanced = 2,
    Strong = 3,
    Extreme = 4,
    Maximum = 5
}

// Eligibility is deliberately narrower than graphics API support. A successful
// native VRS probe is still required before the runtime can activate a level.
internal static class FixedFoveationPolicy
{
    internal const int NvidiaVendorId = 0x10DE;

    internal static FixedFoveationLevel Normalize(int level) =>
        level >= (int)FixedFoveationLevel.Off && level <= (int)FixedFoveationLevel.Maximum
            ? (FixedFoveationLevel)level
            : FixedFoveationLevel.Off;

    internal static string? UnavailableReason(bool windows, bool proton, bool d3d11, int vendorId)
    {
        if (proton)
            return "Unavailable on Linux/Proton.";
        if (!windows)
            return "Unavailable: native Windows is required.";
        if (!d3d11)
            return "Unavailable: Direct3D11 is required.";
        if (vendorId != NvidiaVendorId)
            return "Unavailable: NVIDIA VRS hardware is required.";
        return null;
    }

    internal static FixedFoveationLevel EffectiveLevel(FixedFoveationLevel requested, bool available, bool xrActive) =>
        available && xrActive ? Normalize((int)requested) : FixedFoveationLevel.Off;

    // The two test levels deliberately protect much less of the image. Never
    // leave that small region at a stale gaze or fixed centre after tracking loss.
    internal static FixedFoveationLevel WithGazeFallback(FixedFoveationLevel level, bool eyeTracking, bool tracked) =>
        eyeTracking && !tracked && level > FixedFoveationLevel.Strong ? FixedFoveationLevel.Strong : level;

    // Eye tracking moves the sharp region, but gaze input lags the eye and its
    // boundary sweeps over the scene during saccades. Protect a margin around
    // the tracked centre instead of reusing the fixed-centre radii unchanged,
    // and a wider one while the eyes are moving fast.
    internal const float GazeMargin = .12f, SaccadeMargin = .15f;
    // Preset radii are tuned for an eye whose projection scale (cot of the
    // half-FOV) is about .85, i.e. roughly 100 degrees per eye. Scale them by
    // each eye's actual projection so a preset protects the same angle on
    // every headset and on each eye's asymmetric frustum.
    internal const float ReferenceProjectionScale = .85f, MinimumAngularScale = .7f, MaximumAngularScale = 1.3f;

    internal static float AngularScale(float projectionX, float projectionY)
    {
        if (!(projectionX > 0) || !(projectionY > 0) || float.IsInfinity(projectionX) || float.IsInfinity(projectionY))
            return 1;
        var scale = (float)Math.Sqrt((double)projectionX * projectionY) / ReferenceProjectionScale;
        return float.IsNaN(scale) ? 1 : Math.Max(MinimumAngularScale, Math.Min(MaximumAngularScale, scale));
    }

    internal static void Radii(FixedFoveationLevel level, bool trackedGaze, bool saccade, float angularScale,
        out float inner, out float outer)
    {
        Radii(level, out inner, out outer);
        var scale = float.IsNaN(angularScale) || angularScale <= 0 ? 1 :
            Math.Max(MinimumAngularScale, Math.Min(MaximumAngularScale, angularScale));
        inner *= scale;
        outer *= scale;
        if (trackedGaze && level != FixedFoveationLevel.Off)
        {
            var margin = GazeMargin + (saccade ? SaccadeMargin : 0);
            inner += margin;
            outer += margin;
        }
        // The native backend accepts inner .15-1.25 and outer > inner up to 1.5.
        inner = Math.Max(.15f, Math.Min(1.1f, inner));
        outer = Math.Min(1.4f, Math.Max(inner + .05f, outer));
    }

    internal static void Radii(FixedFoveationLevel level, out float inner, out float outer)
    {
        switch (level)
        {
            case FixedFoveationLevel.Mild: inner = .70f; outer = .95f; break;
            case FixedFoveationLevel.Balanced: inner = .55f; outer = .85f; break;
            case FixedFoveationLevel.Strong: inner = .40f; outer = .75f; break;
            case FixedFoveationLevel.Extreme: inner = .30f; outer = .60f; break;
            case FixedFoveationLevel.Maximum: inner = .18f; outer = .40f; break;
            default: inner = .70f; outer = .95f; break; // Off diagnostic packet.
        }
    }
}
