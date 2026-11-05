using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Capture the displayed pose when the second corner is acquired. All three
// transforms then share that baseline, even while the displayed pose smooths.
internal readonly struct HudWidgetTwoHandGesture
{
    private readonly Vector3 _offset, _span, _euler;
    private readonly Quaternion _leftRotation, _rightRotation;
    private readonly float _scale, _aspect;
    private readonly bool _independentAxes, _preserveRoll;

    internal HudWidgetTwoHandGesture(HudWidgetPose pose, Vector3 left, Vector3 right, Quaternion leftRotation, Quaternion rightRotation, bool independentAxes = false)
    {
        _preserveRoll = pose.Anchor != HudWidgetAnchor.Cockpit;
        var inverse = Quaternion.Inverse(Quaternion.Euler(pose.Euler.x, pose.Euler.y, _preserveRoll ? pose.Euler.z : 0));
        _offset = inverse * (pose.Position - (left + right) * .5f);
        _span = inverse * (right - left);
        _euler = pose.Euler;
        _scale = pose.Scale;
        _aspect = pose.Aspect;
        _independentAxes = independentAxes;
        _leftRotation = leftRotation;
        _rightRotation = rightRotation;
    }

    internal HudWidgetPose Move(HudWidgetPose current, Vector3 left, Vector3 right, Quaternion leftRotation, Quaternion rightRotation)
    {
        var euler = HudWidgetRotationMath.PairDrag(_euler, _leftRotation, _rightRotation, leftRotation, rightRotation, current.Euler, _preserveRoll);
        var rotation = Quaternion.Euler(euler.x, euler.y, _preserveRoll ? euler.z : 0);
        var span = Quaternion.Inverse(rotation) * (right - left);
        if (_independentAxes)
        {
            var startShape = HudWidgetShape.Factors(_aspect);
            var currentShape = HudWidgetShape.Factors(current.Aspect);
            var width = HudWidgetShape.ResizeAxis(_scale * startShape.x, _span.x, span.x, current.Scale * currentShape.x);
            var height = HudWidgetShape.ResizeAxis(_scale * startShape.y, _span.y, span.y, current.Scale * currentShape.y);
            var rectangularScale = (float)System.Math.Sqrt(width * height);
            var aspect = width / height;
            HudWidgetShape.Normalize(ref rectangularScale, ref aspect, _scale);
            var shape = HudWidgetShape.Factors(aspect);
            var offset = new Vector3(_offset.x * rectangularScale * shape.x / (_scale * startShape.x),
                _offset.y * rectangularScale * shape.y / (_scale * startShape.y), _offset.z * rectangularScale / _scale);
            current.Position = (left + right) * .5f + rotation * offset;
            current.Euler = euler;
            current.Scale = rectangularScale;
            current.Aspect = aspect;
            return current;
        }
        var scale = HudWidgetGestureMath.Resize(_scale, _span.x, _span.y, _span.z, span.x, span.y, span.z, current.Scale);
        current.Position = (left + right) * .5f + rotation * _offset * (scale / _scale);
        current.Euler = euler;
        current.Scale = scale;
        return current;
    }
}
