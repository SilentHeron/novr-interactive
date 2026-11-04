using System;
using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

internal enum ControllerDiagramInput { None, Touch, Press }

// Physical state only: never consult bindings or dispatch a gameplay action.
internal static class ControllerDiagramActivity
{
    internal static ControllerDiagramInput Read(ControllerSample sample, ControllerButton? button, bool left, bool frame)
    {
        if (!sample.Valid || !frame && (!left && button == ControllerButton.Menu ||
            button is ControllerButton.Top or ControllerButton.Outside or ControllerButton.Shoulder)) return ControllerDiagramInput.None;
        var pressed = button switch
        {
            null => sample.Grip > .15f, ControllerButton.Trigger => sample.Trigger > .1f,
            ControllerButton.Primary => sample.Primary, ControllerButton.Secondary => sample.Secondary,
            ControllerButton.Top => sample.Top, ControllerButton.Outside => sample.Outside,
            ControllerButton.Menu => sample.Menu, ControllerButton.Shoulder => sample.Shoulder,
            ControllerButton.StickClick => sample.StickClick, _ => false
        };
        if (pressed) return ControllerDiagramInput.Press;
        var touched = button switch
        {
            null => sample.GripTouched, ControllerButton.Trigger => sample.TriggerTouched,
            ControllerButton.Primary => sample.PrimaryTouched, ControllerButton.Secondary => sample.SecondaryTouched,
            ControllerButton.Top => sample.TopTouched, ControllerButton.Outside => sample.OutsideTouched,
            ControllerButton.StickClick => sample.ThumbstickTouched, _ => false
        };
        return touched ? ControllerDiagramInput.Touch : ControllerDiagramInput.None;
    }
    internal static Color Color(ControllerDiagramInput state) => state == ControllerDiagramInput.Press
        ? new Color(.1f, .85f, .3f, 1) : new Color(1, .64f, .05f, 1);
}

internal sealed class ControllerDiagramActivityLifecycle : MonoBehaviour
{
    internal Action? Clear;
    private void OnDisable() => Clear?.Invoke();
}

internal sealed class ControllerDiagramActivityRing : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); const int count = 24; const float radius = 7, inner = 5;
        for (var i = 0; i < count; i++)
        {
            var a = i * 2 * Math.PI / count; var b = (i + 1) * 2 * Math.PI / count;
            var first = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
            var second = new Vector2((float)Math.Cos(b), (float)Math.Sin(b));
            var index = vh.currentVertCount;
            vh.AddVert(first * inner, color, Vector2.zero); vh.AddVert(first * radius, color, Vector2.zero);
            vh.AddVert(second * radius, color, Vector2.zero); vh.AddVert(second * inner, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
