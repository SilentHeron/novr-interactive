using System;

namespace NOVR.MotionControls;

// A small visual extension keeps the cuff connected while the sleeve fades.
// It never changes the tracked glove, hit tests, native skeleton or flight input.
internal static class ArmReachMath
{
    internal readonly struct Reach
    {
        internal readonly float Distance, Scale, Opacity;
        internal Reach(float distance, float scale, float opacity)
        { Distance = distance; Scale = scale; Opacity = opacity; }
        internal bool Valid => Scale >= 1;
    }
    internal static Reach Evaluate(float upper, float forearm, float distance)
    {
        if (!FlightControlMath.IsFinite(upper) || !FlightControlMath.IsFinite(forearm) ||
            !FlightControlMath.IsFinite(distance) || upper < .05f || forearm < .05f || distance < .001f)
            return default;
        var length = upper + forearm;
        if (!FlightControlMath.IsFinite(length)) return default;
        var extension = Math.Min(.06f, length * .1f);
        var maximum = length + extension - .002f;
        var minimum = Math.Abs(upper - forearm) + .002f;
        var solvedDistance = Math.Max(minimum, Math.Min(maximum, distance));
        var extra = solvedDistance + .002f - length;
        // Ease into extension over two centimeters, so the elbow's velocity
        // does not kink at the frame where the rigid arm first runs out.
        var smoothExtra = extra <= -.01f ? 0 : extra >= .01f ? extra : (extra + .01f) * (extra + .01f) / .04f;
        var scale = 1 + Math.Min(extension, smoothExtra) / length;
        // Reach fades spatially, avoiding a frame-rate-dependent detached cuff.
        var t = FlightControlMath.Clamp((distance - (length - .015f)) / (extension + .013f), 0, 1);
        var opacity = 1 - t * t * (3 - 2 * t);
        // A hand folded almost into the shoulder also approaches an invalid IK
        // triangle. Fade that narrow range rather than abruptly clipping it.
        var near = FlightControlMath.Clamp((distance - minimum) / .025f, 0, 1);
        opacity *= near * near * (3 - 2 * near);
        return new Reach(solvedDistance, scale, opacity);
    }

    internal static bool Triangle(float upper,float forearm,float distance,out float along,out float height)
    {
        along=height=0;
        if (!FlightControlMath.IsFinite(upper) || !FlightControlMath.IsFinite(forearm) || !FlightControlMath.IsFinite(distance) ||
            upper<=0 || forearm<=0 || distance<=0 || distance>=upper+forearm || distance<=Math.Abs(upper-forearm))return false;
        // Double precision and Heron's factors retain a useful bend near full
        // extension instead of subtracting two almost identical float squares.
        double a=upper,b=forearm,d=distance;
        along=(float)((a*a-b*b+d*d)/(2*d));
        height=(float)(Math.Sqrt((a+b+d)*(a+b-d)*(d+a-b)*(d-a+b))/(2*d));
        return FlightControlMath.IsFinite(along) && FlightControlMath.IsFinite(height);
    }
}
