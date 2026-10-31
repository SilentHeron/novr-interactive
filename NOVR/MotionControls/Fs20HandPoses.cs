using UnityEngine;

namespace NOVR.MotionControls;

// Authored poses fitted to the native FS-20 handles. The palm stays supported
// while the upper thumb joints reach the hat and release button.
internal static class Fs20HandPoses
{
    internal static Vector3 Wrist(bool stick, bool left) => stick
        ? new Vector3(left ? -.046f : .046f, -.025f, -.064f)
        : new Vector3(left ? -.0045f : .0045f, .031f, -.05775f);

    internal static Quaternion StickRotation(bool left) => Quaternion.Euler(-25, 0, left ? 90 : -90);

    internal static Quaternion FireButtonJoint(int bone, bool left, Quaternion open, Quaternion closed, bool pressed)
        => StickThumb(bone, left, open, closed, 2, pressed);

    internal static Quaternion HatPressJoint(int bone, bool left, Quaternion open, Quaternion closed)
        => bone >= 2 && bone <= 4 ? StickThumb(bone, left, open, closed, 1, true)
            : Joint(bone, true, left, open, closed);

    private static Quaternion StickThumb(int bone, bool left, Quaternion open, Quaternion closed, int control, bool pressed = false)
    {
        // Keep the metacarpal/thenar contact identical in every thumb state.
        var curl = bone == 2 ? .1f : bone == 3
            ? control == 1 ? .02607359f : control == 2 ? .04442373f : 0
            : control == 1 ? .14790734f : control == 2 ? 0 : .03f;
        var angles = bone == 2 ? new Vector3(15, -15, -15)
            : bone == 3 && control == 1 ? new Vector3(2.304875f, -3.465798f, -1.265810f)
            : bone == 3 && control == 2 ? new Vector3(4.892687f, -.085247f, 4.728454f) : Vector3.zero;
        var pose = Adjust(Quaternion.Slerp(open, closed, curl), angles, left);
        return pressed && bone == 3 ? Quaternion.AngleAxis(-.8f, Vector3.right) * pose : pose;
    }

    internal static Quaternion SwitchPinchJoint(int bone, bool left, Quaternion open, Quaternion closed)
    {
        // A precision pinch: an arched index meets an opposed thumb at their
        // tips, with a roughly 28 mm gap for the switch cap. The other fingers
        // stay loosely curled. This pose never changes tracked touch points.
        var curl = bone switch
        {
            2 => .49375f, 3 => .146875f, 4 => .303125f,
            7 => .32f, 8 => .45f, 9 => .30f,
            12 => .40f, 13 => .48f, 14 => .38f,
            17 => .44f, 18 => .52f, 19 => .42f,
            22 => .48f, 23 => .56f, 24 => .46f, _ => 0
        };
        var pose = Quaternion.Slerp(open, closed, curl);
        if (bone != 2) return pose;
        var angles = new Vector3(21.875f, -24.0625f, 2.8125f);
        if (left) { angles.y = -angles.y; angles.z = -angles.z; }
        return Quaternion.AngleAxis(angles.z, Vector3.forward) * Quaternion.AngleAxis(angles.y, Vector3.up) * Quaternion.AngleAxis(angles.x, Vector3.right) * pose;
    }

    internal static Quaternion Joint(int bone, bool stick, bool left, Quaternion open, Quaternion closed, bool thumbContact = false, float triggerPressure = 0)
    {
        if (stick)
        {
            if (bone >= 2 && bone <= 4) return StickThumb(bone, left, open, closed, thumbContact ? 1 : 0);
            var curl = bone switch
            {
                7 => 0.29701892f, 8 => 0.30079686f, 9 => 0.21591434f,
                12 => 0.38117668f, 13 => 0.36021986f, 14 => 0.29015218f,
                17 => 0.39711289f, 18 => 0.40597436f, 19 => 0.30498918f,
                22 => 0.54262926f, 23 => 0.17066973f, 24 => 0.15824422f,
                _ => 0
            };
            // Trigger pressure moves the index; mirrored controls use the same
            // motion and finger lengths on both hands.
            if (bone >= 7 && bone <= 9)
                curl += Mathf.Clamp01(triggerPressure) * (bone == 7 ? .004f : bone == 8 ? .010f : .0065f);
            var angles = bone switch
            {
                7 => new Vector3(-18.579542f, 6.186357f, 16.121743f),
                12 => new Vector3(-2.232396f, 8.204009f, 8.958790f),
                17 => new Vector3(3.448915f, 5.863834f, 14.022061f),
                21 => new Vector3(-12.000000f, -6.000000f, -18.000000f),
                22 => new Vector3(42.853569f, -1.550737f, 38.212761f),
                _ => Vector3.zero
            };
            return Adjust(Quaternion.Slerp(open, closed, curl), angles, left);
        }
        var throttleCurl = bone switch
        {
            3 => .13f, 4 => .08f,
            7 => .12f, 8 => .24f, 9 => .37f,
            12 => .31f, 13 => .49f, 14 => .85f,
            17 => .32f, 18 => .63f, 19 => .85f,
            22 => .47f, 23 => .85f, 24 => .64f, _ => 0
        };
        if (thumbContact && bone >= 2 && bone <= 4)
            throttleCurl = bone == 2 ? .204672f : bone == 3 ? .095984f : .368217f;
        var throttlePose = Quaternion.Slerp(open, closed, throttleCurl);
        if (bone != 2) return throttlePose;
        var throttleAngles = thumbContact ? new Vector3(-1.57100f,48.67644f,14.84383f) : new Vector3(-26,18,0);
        return Adjust(throttlePose, throttleAngles, !left);
    }

    private static Quaternion Adjust(Quaternion pose, Vector3 angles, bool mirrored)
    {
        if (mirrored) { angles.y = -angles.y; angles.z = -angles.z; }
        return Quaternion.AngleAxis(angles.z, Vector3.forward) * Quaternion.AngleAxis(angles.y, Vector3.up)
            * Quaternion.AngleAxis(angles.x, Vector3.right) * pose;
    }
}
