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
        // Grip orientation is arbitrary: both hands, backward/vertical wrists,
        // and rolled controllers must follow the same physical panel rotation.
        foreach (var pitch in new[] { -90f, -40, 0, 65, 90 })
            foreach (var yaw in new[] { -179f, -90, 0, 90, 180 })
                foreach (var roll in new[] { -110f, 0, 120 })
                {
                    var grip = Quaternion.Euler(pitch, yaw, roll);
                    var start = new Vector3(24, -51, 0);
                    var unchanged = HudWidgetRotationMath.Drag(start, grip, grip, start);
                    Near((unchanged - start).magnitude, 0, "Acquiring any wrist orientation preserves displayed panel");
                    var target = new Vector3(-23, -19, 0);
                    var delta = Quaternion.Euler(target.x, target.y, 0) * Quaternion.Inverse(Quaternion.Euler(start.x, start.y, 0));
                    var moved = HudWidgetRotationMath.Drag(start, grip, delta * grip, start);
                    Near((moved - target).magnitude, 0, "Panel follows actual tilt toward viewer regardless of controller heading/roll");
                    var twist = Quaternion.Euler(start.x, start.y, 67) * Quaternion.Inverse(Quaternion.Euler(start.x, start.y, 0));
                    var noRoll = HudWidgetRotationMath.Drag(start, grip, twist * grip, start);
                    Near((noRoll - start).magnitude, 0, "Rolling around panel normal keeps pitch/yaw and locks panel roll");
                }
        var wrapped = HudWidgetRotationMath.Drag(new Vector3(10, 30, 0), Quaternion.Euler(0, 179, 0),
            Quaternion.Euler(0, -179, 0), new Vector3(10, 30, 0));
        Near(wrapped.x, 10, "Heading seam cannot change panel tilt");
        Near(wrapped.y, 32, "Heading crossing 180 degrees moves two degrees, not a full turn");
        var pole = HudWidgetAngleMath.Direction(0, 1, 0, 12, 48);
        Near(pole.yaw, 48, "Near vertical controller retains heading");
        foreach (var sign in new[] { -1, 1 })
        {
            var previousPitch = sign * 70f;
            var previousYaw = 32f;
            for (var wrist = 71; wrist <= 110; wrist++)
            {
                var f = Quaternion.Euler(sign * wrist, 32, 41) * new Vector3(0, 0, 1);
                var a = HudWidgetAngleMath.Direction(f.x, f.y, f.z, previousPitch, previousYaw, true);
                Near(a.yaw, 32, "Crossing either vertical pole never flips widget yaw");
                Near(a.pitch, sign * wrist, "Pitch stays continuous while crossing either vertical pole");
                previousPitch = a.pitch;
                previousYaw = a.yaw;
            }
        }
        Near(HudWidgetRotationMath.Drag(new Vector3(75, 0, 0), Quaternion.identity, Quaternion.Euler(20, 0, 0),
            new Vector3(75, 0, 0)).x, 80, "Pitch cannot cross a panel singularity");
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var safe = HudWidgetAngleMath.Direction(invalid, 0, 1, 12, 48);
            check(safe.pitch == 12 && safe.yaw == 48, "Bad tracking retains prior angle");
            Near(HudWidgetAngleMath.Pitch(invalid, 13), 13, "Invalid pitch uses fallback");
            Near(HudWidgetAngleMath.Yaw(invalid, 37), 37, "Invalid yaw uses fallback");
            var previous = new Vector3(12, 48, 0);
            var held = HudWidgetRotationMath.Drag(previous, Quaternion.identity, Quaternion.Euler(invalid, 0, 0), previous);
            Near((held - previous).magnitude, 0, "Invalid controller rotation retains displayed panel angle");
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
            var pose = HudWidgetProfiles.FactoryDefaults[id];
            pose.Euler = new Vector3(35, -70, 45);
            var clean = HudWidgetProfiles.Sanitize(id, pose);
            check(clean.Euler.x == 35 && clean.Euler.y == -70 && clean.Euler.z == 0,
                "Every saved widget allows pitch/yaw and rejects roll: " + id);
        }
    }
}
