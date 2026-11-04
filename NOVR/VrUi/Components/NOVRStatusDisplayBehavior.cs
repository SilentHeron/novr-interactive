using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

[DefaultExecutionOrder(150)]
public class NOVRStatusDisplayBehavior : UIRenderedCanvasBehavior
{
    private void LateUpdate()
    {
        // Source leases return this to FlightHud on aircraft loss. That root
        // suppresses drawing during recovery; do not recreate its large fallback.
        if (CockpitAlerts.InRecovery) return;
        if (CockpitStatusSources.Owns(transform)) return;
        transform.position = new Vector3(630f,145f,1000f);
    }
}
