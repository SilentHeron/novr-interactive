using System;

namespace NOVR.VrUi.SpecialBehavior;

// Use the controller's forward vector, so rolling the controller around its
// pointing direction cannot tilt a widget. Angles are relative to the cockpit.
internal static class HudWidgetAngleMath
{
    internal static float Pitch(float value, float fallback = 0) => Finite(value) ? Math.Max(-80, Math.Min(80, value)) : fallback;
    internal static float Yaw(float value, float fallback = 0) => Finite(value) ? (float)Math.IEEERemainder(value, 360) : fallback;
    internal static (float pitch, float yaw) Direction(float x, float y, float z, float fallbackPitch, float fallbackYaw, bool continuous = false)
    {
        if (!Finite(x) || !Finite(y) || !Finite(z)) return (fallbackPitch, fallbackYaw);
        var horizontal = Math.Sqrt((double)x * x + (double)z * z);
        if (horizontal * horizontal + (double)y * y < .000001) return (fallbackPitch, fallbackYaw);
        var pitch = (float)(Math.Atan2(-y, horizontal) * 180 / Math.PI);
        // Yaw is undefined when pointing almost vertically; retain the last
        // usable heading instead of flipping a panel as tracking crosses it.
        var yaw = (float)(Math.Atan2(x, z) * 180 / Math.PI);
        if (continuous)
        {
            // The same direction has two pitch/yaw representations. Follow
            // the nearby one when the wrist crosses a vertical pole instead
            // of suddenly turning the widget by 180 degrees.
            var alternatePitch = pitch >= 0 ? 180 - pitch : -180 - pitch;
            var alternateYaw = Yaw(yaw + 180);
            var deltaPitch = Yaw(pitch - fallbackPitch);
            var alternateDeltaPitch = Yaw(alternatePitch - fallbackPitch);
            var deltaYaw = Yaw(yaw - fallbackYaw);
            var alternateDeltaYaw = Yaw(alternateYaw - fallbackYaw);
            if (alternateDeltaPitch * alternateDeltaPitch + alternateDeltaYaw * alternateDeltaYaw <
                deltaPitch * deltaPitch + deltaYaw * deltaYaw)
            { pitch = alternatePitch; yaw = alternateYaw; }
        }
        if (horizontal < .15) yaw = fallbackYaw;
        return (pitch, yaw);
    }
    internal static (float pitch, float yaw) Drag(float startPitch, float startYaw, float gripPitch, float gripYaw,
        float currentPitch, float currentYaw) =>
        (Pitch(startPitch + Yaw(currentPitch - gripPitch), startPitch), Yaw(startYaw + Yaw(currentYaw - gripYaw), startYaw));
    internal static (float pitch, float yaw) PairDrag(float startPitch, float startYaw,
        float leftGripPitch, float leftGripYaw, float rightGripPitch, float rightGripYaw,
        float leftPitch, float leftYaw, float rightPitch, float rightYaw) =>
        (Pitch(startPitch + MeanDelta(leftPitch - leftGripPitch, rightPitch - rightGripPitch), startPitch),
         Yaw(startYaw + MeanDelta(leftYaw - leftGripYaw, rightYaw - rightGripYaw), startYaw));
    private static float MeanDelta(float left, float right)
    {
        left = Yaw(left); right = Yaw(right);
        // Follow the short arc: +179 and -179 are adjacent directions, not zero.
        return Yaw(left + Yaw(right - left) * .5f);
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
