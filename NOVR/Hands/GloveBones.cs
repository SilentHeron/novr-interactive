namespace NOVR.MotionControls;

// Stable indices in the bundled Valve glove rig. Pose tables keep their exact
// authored values while naming the joints that those values belong to.
internal static class GloveBones
{
    internal const int Root = 0;
    internal const int Wrist = 1;
    internal const int ThumbMetacarpal = 2;
    internal const int ThumbProximal = 3;
    internal const int ThumbDistal = 4;
    internal const int ThumbTip = 5;
    internal const int IndexMetacarpal = 6;
    internal const int IndexProximal = 7;
    internal const int IndexIntermediate = 8;
    internal const int IndexDistal = 9;
    internal const int IndexTip = 10;
    internal const int MiddleMetacarpal = 11;
    internal const int MiddleProximal = 12;
    internal const int MiddleIntermediate = 13;
    internal const int MiddleDistal = 14;
    internal const int MiddleTip = 15;
    internal const int RingMetacarpal = 16;
    internal const int RingProximal = 17;
    internal const int RingIntermediate = 18;
    internal const int RingDistal = 19;
    internal const int RingTip = 20;
    internal const int LittleMetacarpal = 21;
    internal const int LittleProximal = 22;
    internal const int LittleIntermediate = 23;
    internal const int LittleDistal = 24;
    internal const int LittleTip = 25;
    internal const int ThumbAuxiliary = 26;
    internal const int IndexAuxiliary = 27;
    internal const int MiddleAuxiliary = 28;
    internal const int RingAuxiliary = 29;
    internal const int LittleAuxiliary = 30;
    internal const int Count = 31;

    internal static bool IsThumbJoint(int bone) => bone >= ThumbMetacarpal && bone <= ThumbDistal;
    internal static bool IsIndexJoint(int bone) => bone >= IndexProximal && bone <= IndexDistal;
}
