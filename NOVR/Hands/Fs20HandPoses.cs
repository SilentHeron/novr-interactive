using UnityEngine;

namespace NOVR.MotionControls;

// Authored poses fitted to the native FS-20 handles. The palm stays supported
// while the upper thumb joints reach the hat and release button.
internal static class Fs20HandPoses
{
    private enum ThumbControl
    {
        Rest,
        Hat,
        FireButton
    }

    internal static Vector3 Wrist(bool stick, bool left) => stick
        ? new Vector3(left ? -.046f : .046f, -.025f, -.064f)
        : new Vector3(left ? -.0045f : .0045f, .031f, -.05775f);
    internal static Quaternion StickRotation(bool left) => Quaternion.Euler(-25, 0, left ? 90 : -90);
    internal static Quaternion FireButtonJoint(int bone, bool left, Quaternion open, Quaternion closed, bool pressed) =>
        StickThumb(bone, left, open, closed, ThumbControl.FireButton, pressed);

    internal static Quaternion HatPressJoint(int bone, bool left, Quaternion open, Quaternion closed) =>
        GloveBones.IsThumbJoint(bone)
            ? StickThumb(bone, left, open, closed, ThumbControl.Hat, true)
            : Joint(bone, true, left, open, closed);

    private static Quaternion StickThumb(int bone, bool left, Quaternion open, Quaternion closed, ThumbControl control, bool pressed = false)
    {
        // Keep the metacarpal/thenar contact identical in every thumb state.
        var curl = bone switch
        {
            GloveBones.ThumbMetacarpal => .1f,
            GloveBones.ThumbProximal => control switch
            {
                ThumbControl.Hat => .02607359f,
                ThumbControl.FireButton => .04442373f,
                _ => 0
            },
            _ => control switch
            {
                ThumbControl.Hat => .14790734f,
                ThumbControl.FireButton => 0,
                _ => .03f
            }
        };
        var angles = (bone, control) switch
        {
            (GloveBones.ThumbMetacarpal, _) => new Vector3(15, -15, -15),
            (GloveBones.ThumbProximal, ThumbControl.Hat) => new Vector3(2.304875f, -3.465798f, -1.265810f),
            (GloveBones.ThumbProximal, ThumbControl.FireButton) => new Vector3(4.892687f, -.085247f, 4.728454f),
            _ => Vector3.zero
        };
        var pose = Adjust(Quaternion.Slerp(open, closed, curl), angles, left);
        return pressed && bone == GloveBones.ThumbProximal ? Quaternion.AngleAxis(-.8f, Vector3.right) * pose : pose;
    }

    internal static Quaternion SwitchPinchJoint(int bone, bool left, Quaternion open, Quaternion closed)
    {
        // A precision pinch: an arched index meets an opposed thumb at their
        // tips, with a roughly 28 mm gap for the switch cap. The other fingers
        // stay loosely curled. This pose never changes tracked touch points.
        var curl = bone switch
        {
            GloveBones.ThumbMetacarpal => .49375f,
            GloveBones.ThumbProximal => .146875f,
            GloveBones.ThumbDistal => .303125f,
            GloveBones.IndexProximal => .32f,
            GloveBones.IndexIntermediate => .45f,
            GloveBones.IndexDistal => .30f,
            GloveBones.MiddleProximal => .40f,
            GloveBones.MiddleIntermediate => .48f,
            GloveBones.MiddleDistal => .38f,
            GloveBones.RingProximal => .44f,
            GloveBones.RingIntermediate => .52f,
            GloveBones.RingDistal => .42f,
            GloveBones.LittleProximal => .48f,
            GloveBones.LittleIntermediate => .56f,
            GloveBones.LittleDistal => .46f,
            _ => 0
        };
        var pose = Quaternion.Slerp(open, closed, curl);
        if (bone != GloveBones.ThumbMetacarpal)
            return pose;
        var angles = new Vector3(21.875f, -24.0625f, 2.8125f);
        if (left)
        {
            angles.y = -angles.y;
            angles.z = -angles.z;
        }

        return Quaternion.AngleAxis(angles.z, Vector3.forward) *
            Quaternion.AngleAxis(angles.y, Vector3.up) *
            Quaternion.AngleAxis(angles.x, Vector3.right) * pose;
    }

    internal static Quaternion Joint(int bone, bool stick, bool left, Quaternion open, Quaternion closed, bool thumbContact = false, float triggerPressure = 0)
    {
        if (stick)
        {
            if (GloveBones.IsThumbJoint(bone))
                return StickThumb(bone, left, open, closed, thumbContact ? ThumbControl.Hat : ThumbControl.Rest);
            var curl = bone switch
            {
                GloveBones.IndexProximal => 0.29701892f,
                GloveBones.IndexIntermediate => 0.30079686f,
                GloveBones.IndexDistal => 0.21591434f,
                GloveBones.MiddleProximal => 0.38117668f,
                GloveBones.MiddleIntermediate => 0.36021986f,
                GloveBones.MiddleDistal => 0.29015218f,
                GloveBones.RingProximal => 0.39711289f,
                GloveBones.RingIntermediate => 0.40597436f,
                GloveBones.RingDistal => 0.30498918f,
                GloveBones.LittleProximal => 0.54262926f,
                GloveBones.LittleIntermediate => 0.17066973f,
                GloveBones.LittleDistal => 0.15824422f,
                _ => 0
            };
            // Trigger pressure moves the index; mirrored controls use the same
            // motion and finger lengths on both hands.
            if (GloveBones.IsIndexJoint(bone))
                curl += Mathf.Clamp01(triggerPressure) *
                    (bone == GloveBones.IndexProximal ? .004f : bone == GloveBones.IndexIntermediate ? .010f : .0065f);
            var angles = bone switch
            {
                GloveBones.IndexProximal => new Vector3(-18.579542f, 6.186357f, 16.121743f),
                GloveBones.MiddleProximal => new Vector3(-2.232396f, 8.204009f, 8.958790f),
                GloveBones.RingProximal => new Vector3(3.448915f, 5.863834f, 14.022061f),
                GloveBones.LittleMetacarpal => new Vector3(-12.000000f, -6.000000f, -18.000000f),
                GloveBones.LittleProximal => new Vector3(42.853569f, -1.550737f, 38.212761f),
                _ => Vector3.zero
            };
            return Adjust(Quaternion.Slerp(open, closed, curl), angles, left);
        }

        var throttleCurl = bone switch
        {
            GloveBones.ThumbProximal => .13f,
            GloveBones.ThumbDistal => .08f,
            GloveBones.IndexProximal => .12f,
            GloveBones.IndexIntermediate => .24f,
            GloveBones.IndexDistal => .37f,
            GloveBones.MiddleProximal => .31f,
            GloveBones.MiddleIntermediate => .49f,
            GloveBones.MiddleDistal => .85f,
            GloveBones.RingProximal => .32f,
            GloveBones.RingIntermediate => .63f,
            GloveBones.RingDistal => .85f,
            GloveBones.LittleProximal => .47f,
            GloveBones.LittleIntermediate => .85f,
            GloveBones.LittleDistal => .64f,
            _ => 0
        };
        if (thumbContact && GloveBones.IsThumbJoint(bone))
            throttleCurl = bone switch
            {
                GloveBones.ThumbMetacarpal => .204672f,
                GloveBones.ThumbProximal => .095984f,
                _ => .368217f
            };
        var throttlePose = Quaternion.Slerp(open, closed, throttleCurl);
        if (bone != GloveBones.ThumbMetacarpal)
            return throttlePose;
        var throttleAngles = thumbContact
            ? new Vector3(-1.57100f, 48.67644f, 14.84383f)
            : new Vector3(-26, 18, 0);
        return Adjust(throttlePose, throttleAngles, !left);
    }

    private static Quaternion Adjust(Quaternion pose, Vector3 angles, bool mirrored)
    {
        if (mirrored)
        {
            angles.y = -angles.y;
            angles.z = -angles.z;
        }

        return Quaternion.AngleAxis(angles.z, Vector3.forward) *
            Quaternion.AngleAxis(angles.y, Vector3.up) *
            Quaternion.AngleAxis(angles.x, Vector3.right) * pose;
    }
}
