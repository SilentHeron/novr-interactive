using System.Numerics;
using NOVR.MotionControls;

internal static class PoseChecks
{
    private static Vector3 V(UnityEngine.Vector3 value) => new(value.x, value.y, value.z);

    internal static void Run(Action<bool, string> check)
    {
        var cosine = MathF.Cos(25 * MathF.PI / 180);
        foreach (var frame in new[] { false, true })
        {
            foreach (var left in new[] { false, true })
            {
                var side = left ? 1f : -1f;
                var deckNormal = Vector3.Normalize(frame
                    ? new Vector3(side * .1654186f, .7774802f, .6067629f)
                    : new Vector3(0, .7358035f, .6771951f));
                var deckForward = Vector3.Normalize(frame
                    ? new Vector3(side * .1007780f, -.6253439f, .7738145f)
                    : new Vector3(0, -.6771951f, .7358035f));
                var cap = frame ? new Vector3(side * .027826f, .0110762f, .0537274f)
                    : new Vector3(-side * .006413f, -.0166453f, .0537527f);
                HandOptionWheelPose.Get(left, frame, out var offsetValue, out var normalValue, out var upValue);
                var offset = V(offsetValue);
                var normal = V(normalValue);
                var up = V(upValue);
                check(Vector3.Distance(offset, cap + deckNormal * .055f) < .000001f,
                    "wheel center remains 5.5 cm above the actual cap for both profiles/hands");
                check(MathF.Abs(Vector3.Dot(normal, deckNormal) - cosine) < .00001f,
                    "wheel front is tilted exactly 25 degrees from the controller deck");
                check(Vector3.Dot(normal, deckForward) < -.42f,
                    "front tilts toward wrist rather than away from pilot");
                check(Vector3.Dot(up, deckNormal) > .42f && Vector3.Dot(up, deckForward) > .90f,
                    "far edge rises while wheel up still corresponds to forward stick input");
                check(MathF.Abs(Vector3.Dot(normal, up)) < .00001f &&
                    MathF.Abs(normal.Length() - 1) < .00001f && MathF.Abs(up.Length() - 1) < .00001f,
                    "render basis remains orthonormal");
                check(Vector3.Dot(Vector3.Cross(normal, up), Vector3.Cross(deckNormal, deckForward)) > .9999f,
                    "horizontal sector direction has no mirrored or reversed handedness");
                check(Vector3.Dot(offset - up * (.00065f * 190), deckNormal) - Vector3.Dot(cap, deckNormal) > .002f,
                    "lower wheel guidance stays above the thumbstick plane after tilt");
                HandOptionWheelPose.Get(!left, !frame, out _, out _, out _);
                HandOptionWheelPose.Get(left, frame, out var againOffset, out var againNormal, out var againUp);
                check(offsetValue == againOffset && normalValue == againNormal && upValue == againUp,
                    "pose is fixed per physical controller with no accumulated or gaze-dependent state");
            }
            HandOptionWheelPose.Get(true, frame, out var lo, out var ln, out var lu);
            HandOptionWheelPose.Get(false, frame, out var ro, out var rn, out var ru);
            check(Vector3.Distance(V(lo) * new Vector3(-1, 1, 1), V(ro)) < .000001f &&
                Vector3.Distance(V(ln) * new Vector3(-1, 1, 1), V(rn)) < .000001f &&
                Vector3.Distance(V(lu) * new Vector3(-1, 1, 1), V(ru)) < .000001f,
                "both controllers retain matching mirrored anchors and ergonomic tilt");
        }
    }
}
