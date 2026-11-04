using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class NoVrHudBehavior : UIRenderedCanvasBehavior
{
    protected override void Update()
    {
        base.Update();
        transform.position = new Vector3(0f, 0f, 1000f);
        transform.rotation = Quaternion.identity;
    }
}
