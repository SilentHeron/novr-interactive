using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

[DefaultExecutionOrder(60)]
public class NOVRTargetDesignatorBehavior : UIRenderedCanvasBehavior
{
    private float _offset = 1000;

    private void LateUpdate()
    {
        if (NOUIManager.I == null) return;
        var uiCam = APIBus.CockpitHudReference;
        if (uiCam == null) return;
        
        transform.rotation = Quaternion.SlerpUnclamped(Quaternion.identity, uiCam.transform.rotation, 1.1f);
        transform.position = uiCam.transform.position + transform.forward * _offset;
    }
}
