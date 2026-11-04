namespace NOVR.MotionControls;

// Shared by the physical FS-20 controls and the explicit wrist-menu samples.
// These are base amplitudes; the user's strength setting is applied at playback.
internal static class InteractionFeedbackProfile
{
    internal static bool TryGet(InteractionCue cue, out float strength, out float seconds)
    {
        switch (cue)
        {
            case InteractionCue.Touch:
                strength = .16f;
                seconds = .025f;
                return true;
            case InteractionCue.Grab:
                strength = .35f;
                seconds = .045f;
                return true;
            case InteractionCue.Switch:
                strength = .5f;
                seconds = .065f;
                return true;
            case InteractionCue.Detent:
                strength = .65f;
                seconds = .075f;
                return true;
            default:
                strength = seconds = 0;
                return false;
        }
    }
}
