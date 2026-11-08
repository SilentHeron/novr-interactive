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
