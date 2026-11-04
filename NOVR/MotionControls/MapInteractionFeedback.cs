namespace NOVR.MotionControls;

// Base amplitudes still respect the user's shared haptic-strength setting.
internal static class MapInteractionFeedback
{
    internal const float TouchStrength = .34f, TouchSeconds = .045f;
    internal const float SelectStrength = .55f, SelectSeconds = .06f;
    internal const float ZoomStrength = .60f, ZoomSeconds = .065f;
    internal const float TargetStrength = .40f, TargetSeconds = .04f;
}
