namespace NOVR.VrUi.SpecialBehavior;

internal enum CockpitFeedMode
{
    Native, Cockpit, Recovery
}

internal static class CockpitFeedPresentation
{
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
