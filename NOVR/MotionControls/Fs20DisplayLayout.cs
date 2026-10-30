using UnityEngine;

namespace NOVR.MotionControls;

// The upper FS-20 glass plane, extended down over the original switch strip.
// Each 192 x 219 mm display keeps uniform UI scale. An 8 mm forward lift clears
// the measured 6.2 mm structural step below the native upper glass; the fitted
// opaque backing and bezel occupy that lift rather than leaving a floating UI.
internal static class Fs20DisplayLayout
{
    internal const float Width = 237f;
    internal const float Height = 270f;
    internal const float Scale = .00081f;
    internal static readonly Quaternion Rotation = Quaternion.Euler(11.825f, 0, 0);
    internal static Vector3 Center(bool left) => new Vector3(left ? -.136f : .136f, .440f, .567758f)
        + Rotation * Vector3.back * .008f;
}
