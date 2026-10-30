using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Own a separate multiplier so native color/alpha animation keeps working.
// Attach only to instruments, compass and generated pitch ladder: never to
// a whole HUD canvas containing target, flight-path or missile-warning markers.
[DefaultExecutionOrder(90)]
internal sealed class InstrumentBrightness : MonoBehaviour
{
    private CanvasGroup? _group;
    internal static void Add(Transform target)
    {
        if (!target.TryGetComponent<InstrumentBrightness>(out _)) target.gameObject.AddComponent<InstrumentBrightness>();
    }
    private void Awake() { _group = gameObject.AddComponent<CanvasGroup>(); }
    private void LateUpdate()
    {
        if (_group == null) return;
        var alpha = MotionControls.UniversalCockpitPanel.IsDocked(transform) ? 1f : ModConfiguration.Instance.InstrumentBrightness.Value;
        if (_group.alpha != alpha) _group.alpha = alpha;
    }
    private void OnDisable() { if (_group != null) _group.alpha = 1f; }
    private void OnDestroy() { if (_group != null) Destroy(_group); }
}
