using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Cockpit panels lock roll to zero. Wrist panels retain their captured local
// roll: turning the anchor hand must not cause a jump on the next edit grab.
// Subtracting controller Euler angles reverses pitch when a hand faces backward
// and loses heading when its pointing direction crosses a vertical pole.
internal static class HudWidgetRotationMath
{
    internal static Vector3 Drag(Vector3 start, Quaternion grip, Quaternion hand, Vector3 current, bool preserveRoll = false) =>
        WithoutRoll(hand * Quaternion.Inverse(grip) * Quaternion.Euler(start.x, start.y, preserveRoll ? start.z : 0), current, preserveRoll ? start.z : 0, preserveRoll);

    internal static Vector3 PairDrag(Vector3 start, Quaternion leftGrip, Quaternion rightGrip,
        Quaternion left, Quaternion right, Vector3 current, bool preserveRoll = false)
    {
        var delta = Quaternion.Slerp(left * Quaternion.Inverse(leftGrip), right * Quaternion.Inverse(rightGrip), .5f);
        return WithoutRoll(delta * Quaternion.Euler(start.x, start.y, preserveRoll ? start.z : 0), current, preserveRoll ? start.z : 0, preserveRoll);
    }

    private static Vector3 WithoutRoll(Quaternion rotation, Vector3 current, float fixedRoll, bool wrist)
    {
        var forward = rotation * new Vector3(0, 0, 1);
        var angles = HudWidgetAngleMath.Direction(forward.x, forward.y, forward.z, current.x, current.y, true);
        return new Vector3(wrist ? angles.pitch : HudWidgetAngleMath.Pitch(angles.pitch, current.x), HudWidgetAngleMath.Yaw(angles.yaw, current.y), fixedRoll);
    }
}
