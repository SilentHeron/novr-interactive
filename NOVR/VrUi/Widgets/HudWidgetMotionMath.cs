using System;

namespace NOVR.VrUi.SpecialBehavior;

internal static class HudWidgetMotionMath
{
    // A short time constant damps tracking tremor without making placement
    // floaty. Only held widgets use this; releasing freezes the displayed pose.
    internal static float Blend(float deltaTime) => float.IsNaN(deltaTime) || deltaTime <= 0
        ? 0 : (float)(1 - Math.Exp(-deltaTime / .04));

    internal static float Angle(float current, float target, float blend) =>
        HudWidgetAngleMath.Yaw(current + HudWidgetAngleMath.Yaw(target - current) * blend);
}
