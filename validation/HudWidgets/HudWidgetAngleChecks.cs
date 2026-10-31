using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class HudWidgetAngleChecks
{
    internal static void Run(Action<bool, string> check)
    {
        void Near(float a, float b, string label) => check(Math.Abs(a - b) < .0002f, label);
        foreach (var pitch in new[] { -65f, 0, 47 })
        foreach (var yaw in new[] { -179f, -60, 0, 135 })
        foreach (var roll in new[] { -140f, 0, 83 })
        {
            var forward = Quaternion.Euler(pitch, yaw, roll) * new Vector3(0, 0, 1);
            var angles = HudWidgetAngleMath.Direction(forward.x, forward.y, forward.z, 0, 0);
            Near(angles.pitch, pitch, "Wrist roll cannot change widget pitch");
            Near(angles.yaw, yaw, "Wrist roll cannot change widget yaw");
        }
        var unchanged = HudWidgetAngleMath.Drag(24, -51, -20, 100, -20, 100);
        Near(unchanged.pitch, 24, "Acquiring/rebasing a tilted widget preserves pitch");
        Near(unchanged.yaw, -51, "Acquiring/rebasing a tilted widget preserves yaw");
        var wrapped = HudWidgetAngleMath.Drag(10, 30, 0, 179, 5, -179);
        Near(wrapped.pitch, 15, "Controller tilt adds relative pitch");
        Near(wrapped.yaw, 32, "Heading crossing 180 degrees moves two degrees, not a full turn");
        var pole = HudWidgetAngleMath.Direction(0, 1, 0, 12, 48);
        Near(pole.yaw, 48, "Near vertical controller retains heading");
        foreach (var sign in new[] { -1, 1 })
        {
            var previousPitch = sign * 70f; var previousYaw = 32f;
            for (var wrist = 71; wrist <= 110; wrist++)
            {
                var f = Quaternion.Euler(sign * wrist, 32, 41) * new Vector3(0, 0, 1);
                var a = HudWidgetAngleMath.Direction(f.x, f.y, f.z, previousPitch, previousYaw, true);
                Near(a.yaw, 32, "Crossing either vertical pole never flips widget yaw");
                Near(a.pitch, sign * wrist, "Pitch stays continuous while crossing either vertical pole");
                previousPitch = a.pitch; previousYaw = a.yaw;
            }
        }
        Near(HudWidgetAngleMath.Drag(75, 0, 0, 0, 20, 0).pitch, 80, "Pitch cannot cross a panel singularity");
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var safe = HudWidgetAngleMath.Direction(invalid, 0, 1, 12, 48);
            check(safe.pitch == 12 && safe.yaw == 48, "Bad tracking retains prior angle");
            Near(HudWidgetAngleMath.Pitch(invalid, 13), 13, "Invalid pitch uses fallback");
            Near(HudWidgetAngleMath.Yaw(invalid, 37), 37, "Invalid yaw uses fallback");
        }
        // Holding an off-centre point while tilting must keep that exact point
        // attached, rather than orbiting the whole panel around its centre.
        var anchor = new Vector3(.11f, -.06f, .015f);
        var hand = new Vector3(.4f, -.3f, .65f);
        var rotation = Quaternion.Euler(32, -51, 0);
        var centre = hand - rotation * anchor;
        var contact = centre + rotation * anchor;
        Near((contact - hand).magnitude, 0, "Tilt preserves off-centre contact");
        foreach (var id in HudWidgetProfiles.Ids)
        {
            var pose = HudWidgetProfiles.FactoryDefaults[id]; pose.Euler = new Vector3(35, -70, 45);
            var clean = HudWidgetProfiles.Sanitize(id, pose);
            check(clean.Euler.x == 35 && clean.Euler.y == -70 && clean.Euler.z == 0,
                "Every saved widget allows pitch/yaw and rejects roll: " + id);
        }
    }
}
