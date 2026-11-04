using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Capture the displayed pose when the second corner is acquired. All three
// transforms then share that baseline, even while the displayed pose smooths.
internal readonly struct HudWidgetTwoHandGesture
{
    private readonly Vector3 _offset, _span, _euler;
    private readonly Vector2 _leftAngles, _rightAngles;
    private readonly float _scale;

    internal HudWidgetTwoHandGesture(HudWidgetPose pose, Vector3 left, Vector3 right, Vector2 leftAngles, Vector2 rightAngles)
    {
        var inverse = Quaternion.Inverse(Quaternion.Euler(pose.Euler.x, pose.Euler.y, 0));
        _offset = inverse * (pose.Position - (left + right) * .5f);
        _span = inverse * (right - left);
        _euler = pose.Euler;
        _scale = pose.Scale;
        _leftAngles = leftAngles; _rightAngles = rightAngles;
    }

    internal HudWidgetPose Move(HudWidgetPose current, Vector3 left, Vector3 right, Vector2 leftAngles, Vector2 rightAngles)
    {
        var angles = HudWidgetAngleMath.PairDrag(_euler.x, _euler.y,
            _leftAngles.x, _leftAngles.y, _rightAngles.x, _rightAngles.y,
            leftAngles.x, leftAngles.y, rightAngles.x, rightAngles.y);
        var rotation = Quaternion.Euler(angles.pitch, angles.yaw, 0);
        var span = Quaternion.Inverse(rotation) * (right - left);
        var scale = HudWidgetGestureMath.Resize(_scale, _span.x, _span.y, _span.z, span.x, span.y, span.z, current.Scale);
        current.Position = (left + right) * .5f + rotation * _offset * (scale / _scale);
        current.Euler = new Vector3(angles.pitch, angles.yaw, 0);
        current.Scale = scale;
        return current;
    }
}
