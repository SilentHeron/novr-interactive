using System;

namespace NOVR.MotionControls;

internal static class PilotTorsoMath
{
    // A head turn/lean does not translate the whole shoulder girdle. Follow a
    // portion of tracked displacement, bounded independently of seat adjustment.
    internal static ArmPoseMath.Vector Lean(ArmPoseMath.Vector displacement)
    {
        if (!displacement.Finite)
            return default;
        var length = displacement.Length;
        if (length < .00001f)
            return default;
        const float follow = .35f, maximum = .12f;
        // Smooth saturation avoids a velocity kink at the travel limit.
        var distance = maximum * (float)Math.Tanh(length * follow / maximum);
        return displacement * (distance / length);
    }
}
