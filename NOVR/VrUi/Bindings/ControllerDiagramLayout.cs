using NOVR.MotionControls;
using UnityEngine;

namespace NOVR.VrUi;

// Place labels by the physical control's side and height, not its action group.
// These ordered banks keep the original artwork clear and leaders uncrossed.
internal static class ControllerDiagramLayout
{
    internal static Vector2 Position(bool frame, bool left, ControllerButton? button)
    {
        bool columnLeft;
        float y;
        if (frame)
        {
            columnLeft = left ? button is ControllerButton.Top or ControllerButton.Outside or ControllerButton.Primary :
                button == null || button is ControllerButton.Menu or ControllerButton.Secondary or ControllerButton.StickClick;
            y = button switch
            {
                ControllerButton.Menu or ControllerButton.Top => 124,
                ControllerButton.Secondary or ControllerButton.Outside => 76,
                ControllerButton.StickClick or ControllerButton.Primary => 28,
                ControllerButton.Shoulder => -68,
                ControllerButton.Trigger => -116,
                _ => -20
            };
        }
        else
        {
            var thumbstickSide = button is ControllerButton.Trigger or ControllerButton.StickClick or ControllerButton.Menu;
            columnLeft = thumbstickSide ? left : !left;
            y = button switch
            {
                ControllerButton.Trigger => 124,
                ControllerButton.Secondary => 76,
                ControllerButton.StickClick => 60,
                ControllerButton.Primary => 20,
                ControllerButton.Menu => 4,
                _ => -44
            };
        }
        return new Vector2(columnLeft ? -195 : 195, y);
    }
}
