using UnityEngine;

namespace NOVR.MotionControls;

// Fitted to the installed utility helicopter's complete collective, at the
// front of its sleeve. The native model stays full size; the opposite hand and
// model reflect together. Only upper thumb/index joints respond to buttons.
internal static class CollectiveHandPoses
{
    internal static Vector3 Wrist(bool left) => new(left ? -0.037627356f : 0.037627356f, 0.080000000f, -0.005000000f);
    internal static Quaternion Rotation(bool left) => new(0.447479003f, left ? -0.547505746f : 0.547505746f, left ? 0.447479003f : -0.447479003f, 0.547505746f);

    internal static Quaternion Joint(int bone, bool left, Quaternion open, Quaternion closed, bool thumbContact = false, bool trigger = false)
    {
        var curl = bone switch
        {
            2 => 0.018104892f,
            3 => 0.000000000f,
            4 => 0.000000000f,
            5 => 0.650000000f,
            6 => 0.480000000f,
            7 => 0.197162456f,
            8 => 0.309078261f,
            9 => 0.259467353f,
            10 => 0.480000000f,
            11 => 0.500000000f,
            12 => 0.208524411f,
            13 => 0.319595751f,
            14 => 0.213553078f,
            15 => 0.500000000f,
            16 => 0.500000000f,
            17 => 0.183903737f,
            18 => 0.363831181f,
            19 => 0.207831235f,
            20 => 0.500000000f,
            21 => 0.500000000f,
            22 => 0.169372131f,
            23 => 0.237989684f,
            24 => 0.291563753f,
            25 => 0.500000000f,
            26 => 0.650000000f,
            27 => 0.480000000f,
            28 => 0.500000000f,
            29 => 0.500000000f,
            30 => 0.500000000f,
            _ => 0f
        };
        if (trigger && bone >= 7 && bone <= 9) curl += bone == 7 ? .006f : .004f;
        var pose = Quaternion.Slerp(open, closed, curl);
        var angles = bone switch
        {
            2 => new Vector3(-20.58079811f, 58.51328676f, -22.24545334f),
            3 => new Vector3(3.55692169f, 25.00000000f, 6.95526159f),
            7 => new Vector3(9.07106058f, -5.88634888f, 6.62098785f),
            12 => new Vector3(12.14456284f, 3.32369304f, -4.64781591f),
            17 => new Vector3(6.26830224f, 15.24513342f, -6.56073567f),
            22 => new Vector3(6.12454168f, 22.90363742f, -23.54370244f),
            _ => Vector3.zero
        };
        // The numeric fit is authored on the left glove; reflect rotations for
        // the right-hand collective without moving its supporting palm.
        if (!left) { angles.y = -angles.y; angles.z = -angles.z; }
        pose = Quaternion.AngleAxis(angles.z, Vector3.forward) * Quaternion.AngleAxis(angles.y, Vector3.up) * Quaternion.AngleAxis(angles.x, Vector3.right) * pose;
        return thumbContact && bone == 4 ? Quaternion.AngleAxis(.8f, Vector3.right) * pose : pose;
    }
}
