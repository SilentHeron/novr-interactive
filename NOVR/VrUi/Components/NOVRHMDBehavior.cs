using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

[DefaultExecutionOrder(60)]
public class NOVRHMDBehavior : UIRenderedCanvasBehavior
{
    private Vector3 _nativeScale;

    public override void Awake()
    {
        base.Awake();
        _nativeScale = transform.localScale;
        InstrumentBrightness.Add(transform);
    }

    private void LateUpdate()
    {
        if (NOUIManager.I == null) return;
        var reference = APIBus.CockpitHudReference;
        if (reference == null) return;
        transform.position = reference.transform.position + reference.transform.forward * 1000f;
        transform.rotation = reference.transform.rotation;

        // Keep the native speed/altitude/bearing/horizon arrangement, including
        // the player's native HMD settings. Scale their common parent instead
        // of repeatedly replacing four child positions with a custom layout.
        // Native visibility still hides the HMD duplicates while looking at
        // the aircraft's forward diegetic HUD.
        var config = ModConfiguration.Instance;
        transform.localScale = _nativeScale * (config.FocusedFlightHud.Value ? config.FlightHudScale.Value : 1f);
    }

    private void OnDisable() => transform.localScale = _nativeScale;
}
