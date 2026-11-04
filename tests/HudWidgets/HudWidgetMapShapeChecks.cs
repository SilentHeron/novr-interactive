using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class HudWidgetMapShapeChecks
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var initialAspect in new[] { .5f, 1f, 2f })
            foreach (var diagonal in new[] { -1, 1 })
            {
                var pose = new HudWidgetPose(new Vector3(.18f, -.23f, .47f), new Vector3(12, -28, 0), .8f, .7f, .23f, false, initialAspect);
                var shape = HudWidgetShape.Factors(initialAspect);
                var initial = Quaternion.Euler(12, -28, 0);
                var offset = new Vector3(.012f, -.018f, .007f);
                var localSpan = new Vector3(.21f * shape.x, diagonal * .21f * shape.y, .01f);
                var midpoint = pose.Position - initial * offset;
                var span = initial * localSpan;
                var left = midpoint - span * .5f;
                var right = midpoint + span * .5f;
                var leftAngles = new Vector2(-7, 155);
                var rightAngles = new Vector2(15, -120);
                var gesture = new HudWidgetTwoHandGesture(pose, left, right, leftAngles, rightAngles, true);
                var joined = gesture.Move(pose, left, right, leftAngles, rightAngles);
                SameGeometry(joined, pose, "Second corner joins a rectangular map without a jump");

                var translation = new Vector3(-.07f, .03f, .05f);
                var translated = gesture.Move(pose, left + translation, right + translation, leftAngles, rightAngles);
                Near((translated.Position - pose.Position - translation).magnitude, 0, "Map follows two-hand midpoint");
                Near(translated.Scale, pose.Scale, "Translation retains map size");
                Near(translated.Aspect, pose.Aspect, "Translation retains map shape");

                var rotation = Quaternion.Euler(37, 42, 0);
                var targetMid = midpoint + translation;
                var targetAnglesLeft = new Vector2(18, -135);
                var targetAnglesRight = new Vector2(40, -50);
                var rotatedSpan = rotation * localSpan;
                var rotated = gesture.Move(pose, targetMid - rotatedSpan * .5f, targetMid + rotatedSpan * .5f, targetAnglesLeft, targetAnglesRight);
                Near(rotated.Scale, pose.Scale, "Rotating the viewport cannot resize it");
                Near(rotated.Aspect, pose.Aspect, "Rotating the viewport cannot reshape it");

                const float xFactor = 1.3f, yFactor = .9f;
                var targetSpan = rotation * new Vector3(localSpan.x * xFactor, localSpan.y * yFactor, localSpan.z * 3);
                var movedLeft = targetMid - targetSpan * .5f;
                var movedRight = targetMid + targetSpan * .5f;
                var resized = gesture.Move(pose, movedLeft, movedRight, targetAnglesLeft, targetAnglesRight);
                var newShape = HudWidgetShape.Factors(resized.Aspect);
                Near(resized.Scale * newShape.x, pose.Scale * shape.x * xFactor, "Horizontal corner travel independently resizes map width");
                Near(resized.Scale * newShape.y, pose.Scale * shape.y * yFactor, "Vertical corner travel independently resizes map height");
                Near(resized.Euler.x, 37, "Map resize keeps simultaneous pitch adjustment");
                Near(resized.Euler.y, 42, "Map resize keeps simultaneous yaw adjustment");
                Near(resized.Euler.z, 0, "Map reshape never introduces roll");
                var expectedOffset = new Vector3(offset.x * xFactor, offset.y * yFactor, offset.z * MathF.Sqrt(xFactor * yFactor));
                Near((resized.Position - targetMid - rotation * expectedOffset).magnitude, 0, "Off-centre corner contacts follow each dimension independently");
                check(resized.Opacity == pose.Opacity && resized.Background == pose.Background && resized.Visible == pose.Visible,
                    "Reshaping a map preserves appearance settings");
                SameGeometry(gesture.Move(resized, movedLeft, movedRight, targetAnglesLeft, targetAnglesRight), resized,
                    "Stationary hands cannot accumulate map shape or placement drift");
                var rejoined = new HudWidgetTwoHandGesture(resized, movedLeft, movedRight, targetAnglesLeft, targetAnglesRight, true);
                SameGeometry(rejoined.Move(resized, movedLeft, movedRight, targetAnglesLeft, targetAnglesRight), resized,
                    "A new two-hand gesture starts from the displayed rectangular shape");

                foreach (var remaining in new[] { movedLeft, movedRight })
                {
                    var anchor = Quaternion.Inverse(rotation) * (remaining - resized.Position);
                    Near((remaining - rotation * anchor - resized.Position).magnitude, 0,
                        "Releasing either corner keeps the one-hand drag anchored without a position jump");
                }
                var reversed = gesture.Move(resized, movedRight, movedLeft, targetAnglesLeft, targetAnglesRight);
                Near(reversed.Scale, resized.Scale, "Crossing both corners retains the displayed map dimensions");
                Near(reversed.Aspect, resized.Aspect, "Crossing corners cannot mirror map contents");

                var deepSpan = initial * new Vector3(localSpan.x, localSpan.y, .5f);
                var depth = gesture.Move(pose, midpoint - deepSpan * .5f, midpoint + deepSpan * .5f, leftAngles, rightAngles);
                Near(depth.Scale, pose.Scale, "Out-of-plane depth motion does not distort map size");
                Near(depth.Aspect, pose.Aspect, "Out-of-plane depth motion does not distort map proportions");
            }
        foreach (var aspect in new[] { .25f, .5f, 1f, 2f, 4f })
        {
            var size = HudWidgetShape.Size(new Vector2(300, 300), aspect);
            Near(size.x / size.y, aspect, "Native content viewport exposes the requested width/height ratio");
            Near(size.x * size.y, 90000, "Viewport shape changes preserve pixel area at a fixed uniform scale", .03f);
            foreach (var x in new[] { -1, 1 })
                foreach (var y in new[] { -1, 1 })
                {
                    var halfX = size.x * .0007f * .8f * .5f;
                    var halfY = size.y * .0007f * .8f * .5f;
                    check(HudWidgetGestureMath.Corner(x * halfX, y * halfY, 0, halfX, halfY) == ((x > 0 ? 1 : 0) | (y > 0 ? 2 : 0)),
                        "Corner contacts follow rectangular viewport bounds");
                }
        }
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, -.2f })
            Near(HudWidgetShape.ResizeAxis(.8f, .2f, invalid, .9f), .9f, "Invalid or crossed axis preserves the last dimension");
        Near(HudWidgetShape.ResizeAxis(.8f, .01f, .3f, .9f), .9f, "Almost aligned start contacts cannot explosively scale one axis");

        void SameGeometry(HudWidgetPose a, HudWidgetPose b, string label)
        {
            Near((a.Position - b.Position).magnitude, 0, label);
            Near((a.Euler - b.Euler).magnitude, 0, label);
            Near(a.Scale, b.Scale, label);
            Near(a.Aspect, b.Aspect, label);
        }
        void Near(float a, float b, string label, float tolerance = .00002f) => check(Math.Abs(a - b) <= tolerance, label);
    }
}
