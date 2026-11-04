using UnityEngine;

namespace NOVR.MotionControls;

// Fitted to the installed utility helicopter's complete collective, at the
// front of its sleeve. The native model stays full size; the opposite hand and
// model reflect together. Only upper thumb/index joints respond to buttons.
internal static class CollectiveHandPoses
{
    internal static Vector3 Wrist(bool left) => new(left ? -0.037627356f : 0.037627356f, 0.080000000f, -0.005000000f);
    internal static Quaternion Rotation(bool left) => new(
        0.447479003f, left ? -0.547505746f : 0.547505746f,
        left ? 0.447479003f : -0.447479003f, 0.547505746f);

    internal static Quaternion Joint(int bone, bool left, Quaternion open, Quaternion closed, bool thumbContact = false, bool trigger = false)
    {
        var curl = bone switch
        {
            GloveBones.ThumbMetacarpal => 0.018104892f,
            GloveBones.ThumbProximal => 0.000000000f,
            GloveBones.ThumbDistal => 0.000000000f,
            GloveBones.ThumbTip => 0.650000000f,
            GloveBones.IndexMetacarpal => 0.480000000f,
            GloveBones.IndexProximal => 0.197162456f,
            GloveBones.IndexIntermediate => 0.309078261f,
            GloveBones.IndexDistal => 0.259467353f,
            GloveBones.IndexTip => 0.480000000f,
            GloveBones.MiddleMetacarpal => 0.500000000f,
            GloveBones.MiddleProximal => 0.208524411f,
            GloveBones.MiddleIntermediate => 0.319595751f,
            GloveBones.MiddleDistal => 0.213553078f,
            GloveBones.MiddleTip => 0.500000000f,
            GloveBones.RingMetacarpal => 0.500000000f,
            GloveBones.RingProximal => 0.183903737f,
            GloveBones.RingIntermediate => 0.363831181f,
            GloveBones.RingDistal => 0.207831235f,
            GloveBones.RingTip => 0.500000000f,
            GloveBones.LittleMetacarpal => 0.500000000f,
            GloveBones.LittleProximal => 0.169372131f,
            GloveBones.LittleIntermediate => 0.237989684f,
            GloveBones.LittleDistal => 0.291563753f,
            GloveBones.LittleTip => 0.500000000f,
            GloveBones.ThumbAuxiliary => 0.650000000f,
            GloveBones.IndexAuxiliary => 0.480000000f,
            GloveBones.MiddleAuxiliary => 0.500000000f,
            GloveBones.RingAuxiliary => 0.500000000f,
            GloveBones.LittleAuxiliary => 0.500000000f,
            _ => 0f
        };
        if (trigger && GloveBones.IsIndexJoint(bone))
            curl += bone == GloveBones.IndexProximal ? .006f : .004f;
        var pose = Quaternion.Slerp(open, closed, curl);
        var angles = bone switch
        {
            GloveBones.ThumbMetacarpal => new Vector3(-20.58079811f, 58.51328676f, -22.24545334f),
            GloveBones.ThumbProximal => new Vector3(3.55692169f, 25.00000000f, 6.95526159f),
            GloveBones.IndexProximal => new Vector3(9.07106058f, -5.88634888f, 6.62098785f),
            GloveBones.MiddleProximal => new Vector3(12.14456284f, 3.32369304f, -4.64781591f),
            GloveBones.RingProximal => new Vector3(6.26830224f, 15.24513342f, -6.56073567f),
            GloveBones.LittleProximal => new Vector3(6.12454168f, 22.90363742f, -23.54370244f),
            _ => Vector3.zero
        };
        // The numeric fit is authored on the left glove; reflect rotations for
        // the right-hand collective without moving its supporting palm.
        if (!left)
        {
            angles.y = -angles.y;
            angles.z = -angles.z;
        }

        pose = Quaternion.AngleAxis(angles.z, Vector3.forward) *
            Quaternion.AngleAxis(angles.y, Vector3.up) *
            Quaternion.AngleAxis(angles.x, Vector3.right) * pose;
        return thumbContact && bone == GloveBones.ThumbDistal ? Quaternion.AngleAxis(.8f, Vector3.right) * pose : pose;
    }
}
