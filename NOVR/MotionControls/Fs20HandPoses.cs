using UnityEngine;

namespace NOVR.MotionControls;

// Authored local joint poses, fitted to the installed FS-20 control meshes.
// Fingers follow the grip's taper independently; the thumb remains available
// for HOTAS controls. No native mesh data or per-frame collision solver ships.
internal static class Fs20HandPoses
{
    internal static Vector3 Wrist(bool stick, bool left) => stick
        ? new Vector3(left ? -.058f : .058f, .015f, -.068f)
        : new Vector3(left ? -.0045f : .0045f, .031f, -.05775f);

    internal static Quaternion FireButtonJoint(int bone, bool left, Quaternion open, Quaternion closed, bool pressed)
    {
        // Reach the release cap with an opposed thumb, rather than twisting the
        // base almost a quarter turn and folding the tip back toward the palm.
        var curl = bone == 2 ? .4059375f : bone == 3 ? .5465625f : .475f;
        var pose = Quaternion.Slerp(open, closed, curl);
        if (bone != 2) return pose;
        var angles = new Vector3(64f, -60.38583f, -40.44866f);
        if (left) { angles.y = -angles.y; angles.z = -angles.z; }
        pose = Quaternion.AngleAxis(angles.z, Vector3.forward) * Quaternion.AngleAxis(angles.y, Vector3.up) * Quaternion.AngleAxis(angles.x, Vector3.right) * pose;
        return pressed ? Quaternion.AngleAxis(-.8f, Vector3.right) * pose : pose;
    }

    internal static Quaternion HatPressJoint(int bone, bool left, Quaternion open, Quaternion closed)
    {
        var pose = Joint(bone, true, left, open, closed, true);
        return bone == 2 ? Quaternion.AngleAxis(-.8f, Vector3.right) * pose : pose;
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

    internal static Quaternion Joint(int bone, bool stick, bool left, Quaternion open, Quaternion closed, bool thumbContact = false)
    {
        var curl = stick ? bone switch
        {
            2 => .2121875f, 3 => .4575f, 4 => .3859375f,
            7 => .23f, 8 => .16f, 9 => .01f,
            12 => .47f, 13 => .60f, 14 => .66f,
            17 => .55f, 18 => .55f, 19 => .81f,
            22 => .83f, 23 => .48f, 24 => .66f, _ => 0
        } : bone switch
        {
            3 => .13f, 4 => .08f,
            7 => .12f, 8 => .24f, 9 => .37f,
            12 => .31f, 13 => .49f, 14 => .85f,
            17 => .32f, 18 => .63f, 19 => .85f,
            22 => .47f, 23 => .85f, 24 => .64f, _ => 0
        };
        if (thumbContact && bone >= 2 && bone <= 4)
            curl = stick ? (bone == 2 ? .11375f : bone == 3 ? .85f : .70f) : (bone == 2 ? .204672f : bone == 3 ? .095984f : .368217f);
        var pose = Quaternion.Slerp(open, closed, curl);
        if (bone == 2)
        {
            var angles = stick ? new Vector3(65.25f, -66.47958f, -40.44866f) : new Vector3(-26,18,0);
            if (thumbContact) angles = stick ? new Vector3(65.25f,-70.38583f,-40.44866f) : new Vector3(-1.57100f,48.67644f,14.84383f);
            if (stick == left) { angles.y = -angles.y; angles.z = -angles.z; }
            pose = Quaternion.AngleAxis(angles.z, Vector3.forward) * Quaternion.AngleAxis(angles.y, Vector3.up) * Quaternion.AngleAxis(angles.x, Vector3.right) * pose;
        }
        if (stick && bone == 7) pose = Quaternion.AngleAxis(left ? 3.5f : -3.5f, Vector3.up) * pose;
        return pose;
    }
}
