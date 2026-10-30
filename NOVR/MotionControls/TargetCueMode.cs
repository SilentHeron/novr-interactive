namespace NOVR.MotionControls;

internal enum TargetCueMode { Context, Always, Off }

internal static class TargetCueVisibility
{
    internal static bool Show(TargetCueMode mode, bool cockpit, bool candidate, bool selecting)
        => cockpit && mode != TargetCueMode.Off && (mode == TargetCueMode.Always || candidate || selecting);
}
