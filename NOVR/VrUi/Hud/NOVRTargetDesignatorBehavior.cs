using UnityEngine;
using NOVR.MotionControls;

namespace NOVR.VrUi.SpecialBehavior;

[DefaultExecutionOrder(60)]
public class NOVRTargetDesignatorBehavior : UIRenderedCanvasBehavior
{
    private void LateUpdate()
    {
        if (NOUIManager.I == null)
            return;
        var uiCam = APIBus.CockpitHudCamera;
        if (uiCam == null)
            return;

        // The native designator and VR selection share the visible cue's ray,
        // including its user-adjustable position relative to the tracked view.
        GazeCueRay.AlignDesignator(transform, uiCam.transform);
    }
}
