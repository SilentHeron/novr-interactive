using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class NOVRGameplayUIBehaviour : UIRenderedCanvasBehavior
{

    protected override void Update()
    {
        base.Update();
        transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);
        transform.position = new Vector3(0f, 0f, 3f);
    }
}
