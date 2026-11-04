using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class HudWidgetTwoHandChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var pose = new HudWidgetPose(new Vector3(.18f, -.23f, .47f), new Vector3(12, -28, 0), .65f, .7f, .23f, false);
        var rotation = Quaternion.Euler(12, -28, 0);
        var offset = new Vector3(.012f, -.018f, .007f);
        var localSpan = new Vector3(.22f, .15f, .018f);
        var midpoint = pose.Position - rotation * offset;
        var span = rotation * localSpan;
        var left = midpoint - span * .5f;
        var right = midpoint + span * .5f;
        var leftRotation = Quaternion.Euler(-7, 155, 43);
        var rightRotation = Quaternion.Euler(15, -120, -62);
        var gesture = new HudWidgetTwoHandGesture(pose, left, right, leftRotation, rightRotation);
        var joined = gesture.Move(pose, left, right, leftRotation, rightRotation);
        Near((joined.Position - pose.Position).magnitude, 0, "Acquiring the second hand does not move the displayed panel");
        Near((joined.Euler - pose.Euler).magnitude, 0, "Acquiring the second hand preserves its current pitch/yaw");
        Near(joined.Scale, pose.Scale, "Acquiring the second hand preserves its current size");
        check(joined.Opacity == pose.Opacity && joined.Background == pose.Background && joined.Visible == pose.Visible,
            "Gesture preserves appearance and visibility independently of geometry");

        var translation = new Vector3(-.08f, .04f, -.06f);
        var moved = gesture.Move(pose, left + translation, right + translation, leftRotation, rightRotation);
        Near((moved.Position - pose.Position - translation).magnitude, 0, "Two hands translate with their midpoint");
        Near(moved.Scale, pose.Scale, "Translation alone cannot resize the panel");

        // Change all three at once, including depth in the hand span. The old
        // fixed-plane algorithm shrank the widget as it rotated away from that plane.
        var targetRotation = Quaternion.Euler(37, 42, 0);
        var targetMidpoint = midpoint + translation;
        var targetSpan = targetRotation * localSpan * 1.4f;
        var movedLeft = targetMidpoint - targetSpan * .5f;
        var movedRight = targetMidpoint + targetSpan * .5f;
        var delta = targetRotation * Quaternion.Inverse(rotation);
        var movedLeftRotation = delta * leftRotation;
        var movedRightRotation = delta * rightRotation;
        var combined = gesture.Move(pose, movedLeft, movedRight, movedLeftRotation, movedRightRotation);
        Near(combined.Euler.x, 37, "Both controller pitches rotate the panel while resizing");
        Near(combined.Euler.y, 42, "Both controller headings rotate it across the angle seam");
        Near(combined.Euler.z, 0, "Two-hand manipulation never introduces roll");
        Near(combined.Scale, .91f, "True 3D hand separation determines size despite simultaneous rotation");
        Near((combined.Position - (targetMidpoint + targetRotation * offset * 1.4f)).magnitude, 0,
            "Off-centre contact offset follows translation, rotation and scale together");
        var repeat = gesture.Move(combined, movedLeft, movedRight, movedLeftRotation, movedRightRotation);
        Near((repeat.Position - combined.Position).magnitude, 0, "Repeated stationary input cannot accumulate translation");
        Near(repeat.Scale, combined.Scale, "Repeated stationary input cannot accumulate resize");
        Near((repeat.Euler - combined.Euler).magnitude, 0, "Repeated stationary input cannot accumulate rotation");

        targetSpan = targetRotation * localSpan;
        var rotated = gesture.Move(pose, targetMidpoint - targetSpan * .5f, targetMidpoint + targetSpan * .5f,
            movedLeftRotation, movedRightRotation);
        Near(rotated.Scale, pose.Scale, "Rotation with constant hand separation cannot shrink the panel");
        var crossed = gesture.Move(combined, movedRight, movedLeft, movedLeftRotation, movedRightRotation);
        Near(crossed.Scale, combined.Scale, "Crossed hands hold the rendered size instead of inverting the widget");

        // Releasing either hand rebases the existing one-hand path to the
        // displayed pose, not the unsmoothed gesture target.
        foreach (var remaining in new[] { movedLeft, movedRight })
        {
            var rebasedAnchor = Quaternion.Inverse(targetRotation) * (remaining - combined.Position);
            var a = HudWidgetRotationMath.Drag(combined.Euler, movedLeftRotation, movedLeftRotation, combined.Euler);
            var centre = remaining - Quaternion.Euler(a.x, a.y, 0) * rebasedAnchor;
            Near((centre - combined.Position).magnitude, 0, "Either remaining hand resumes without a release jump");
        }
        var regrab = new HudWidgetTwoHandGesture(combined, movedLeft, movedRight, movedLeftRotation, movedRightRotation);
        var restarted = regrab.Move(combined, movedLeft, movedRight, movedLeftRotation, movedRightRotation);
        Near((restarted.Position - combined.Position).magnitude, 0, "Rejoining the second hand captures the latest position");
        Near((restarted.Euler - combined.Euler).magnitude, 0, "Rejoining captures latest angles without applying old deltas");
        Near(restarted.Scale, combined.Scale, "Rejoining captures latest size");

        var wrapped = HudWidgetRotationMath.PairDrag(new Vector3(0, 10, 0), Quaternion.identity, Quaternion.identity,
            Quaternion.Euler(0, 179, 0), Quaternion.Euler(0, -179, 0), new Vector3(0, 10, 0));
        Near(wrapped.y, -170, "Two-hand mean uses the short angular arc around 180 degrees");
        var half = HudWidgetRotationMath.PairDrag(Vector3.zero, Quaternion.identity, Quaternion.identity,
            Quaternion.Euler(20, 0, 0), Quaternion.identity, Vector3.zero);
        Near(half.x, 10, "A single wrist contributes half the paired rotation");
        var clamped = HudWidgetRotationMath.PairDrag(new Vector3(75, 0, 0), Quaternion.identity, Quaternion.identity,
            Quaternion.Euler(30, 0, 0), Quaternion.Euler(30, 0, 0), new Vector3(75, 0, 0));
        Near(clamped.x, 80, "Paired pitch retains the same safe limit as one hand");
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Near(HudWidgetGestureMath.Resize(.65f, .2f, .1f, invalid, .3f, .2f, .1f, .9f), .9f, "Invalid starting depth retains last size");
            Near(HudWidgetGestureMath.Resize(.65f, .2f, .1f, .1f, .3f, .2f, invalid, .9f), .9f, "Invalid current depth retains last size");
        }
        void Near(float a, float b, string label) => check(Math.Abs(a - b) < .00002f, label);
    }
}
