namespace NOVR.VrUi.SpecialBehavior;

internal enum CockpitFeedMode
{
    Native, Cockpit, Recovery, Spectator
}

internal static class CockpitFeedPresentation
{
    // Menu/radial/full-map transitions change presentation, not ownership of
    // the old floating feed. Native chat is released separately by the caller.
    internal static bool SuppressNative(bool enabled, bool playing, bool hadCockpit, bool spectatorReady = false) =>
        enabled && playing && (hadCockpit || spectatorReady);

    // A view-relative presentation has no aircraft layout to inherit. Keep the
    // whole panel below-left of the sightline throughout the overlay Scale range.
    internal static (float x, float y, float z, float scale) ViewPlacement(float overlayScale)
    {
        if (float.IsNaN(overlayScale) || float.IsInfinity(overlayScale))
            overlayScale = 1;
        overlayScale = System.Math.Max(.7f, System.Math.Min(1.3f, overlayScale));
        return (-.34f, -.22f, .9f, .0006f * overlayScale);
    }

    // Losing an aircraft must not switch its notifications back to the flat
    // HUD. Expanded interfaces still own their original notification history.
    internal static CockpitFeedMode Choose(bool enabled, bool playing, bool unobstructed,
        bool cockpitReady, bool hadCockpit, bool lostCockpit, bool spectatorReady = false)
    {
        if (!enabled || !playing)
            return CockpitFeedMode.Native;
        if (hadCockpit && lostCockpit)
            return CockpitFeedMode.Recovery;
        if (!unobstructed)
            return CockpitFeedMode.Native;
        if (cockpitReady)
            return CockpitFeedMode.Cockpit;
        return spectatorReady ? CockpitFeedMode.Spectator : CockpitFeedMode.Native;
    }
}
