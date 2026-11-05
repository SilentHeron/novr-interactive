namespace NOVR.VrUi.SpecialBehavior;

internal enum CockpitFeedMode
{
    Native, Cockpit, Recovery
}

internal static class CockpitFeedPresentation
{
    // Menu/radial/full-map transitions change presentation, not ownership of
    // the old floating feed. Native chat is released separately by the caller.
    internal static bool SuppressNative(bool enabled, bool playing, bool hadCockpit) =>
        enabled && playing && hadCockpit;

    // Losing an aircraft must not switch its notifications back to the flat
    // HUD. Expanded interfaces still own their original notification history.
    internal static CockpitFeedMode Choose(bool enabled, bool playing, bool unobstructed,
        bool cockpitReady, bool hadCockpit, bool lostCockpit)
    {
        if (!enabled || !playing)
            return CockpitFeedMode.Native;
        if (hadCockpit && lostCockpit)
            return CockpitFeedMode.Recovery;
        if (!unobstructed)
            return CockpitFeedMode.Native;
        if (cockpitReady)
            return CockpitFeedMode.Cockpit;
        return CockpitFeedMode.Native;
    }
}
