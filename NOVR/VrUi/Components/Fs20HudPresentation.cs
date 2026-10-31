using System.Collections.Generic;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Suppress only a status container emptied by successful widget leases. The old
// FS-20 blanket hide lost weapon/CM/capacitor readouts after its MFDs were removed.
// Flight instruments and native threat fallbacks remain game-owned and visible.
[DefaultExecutionOrder(140)]
internal sealed class Fs20HudPresentation : MonoBehaviour
{
    private readonly Dictionary<Transform, CanvasGroup> _empty = new();
    private float _nextFind;
    private FlightHud? _hud;
    private void Awake() => _hud = GetComponent<FlightHud>();
    private void LateUpdate()
    {
        if (_hud != null && Time.unscaledTime >= _nextFind)
        {
            _nextFind = Time.unscaledTime + .5f;
            foreach (var panel in _hud.GetComponentsInChildren<HudReadability>(true))
                if (!_empty.ContainsKey(panel.transform))
                    _empty.Add(panel.transform, panel.gameObject.AddComponent<CanvasGroup>());
        }
        // Own a separate multiplier; do not overwrite HudReadability's group.
        foreach (var pair in _empty)
            if (pair.Value != null) pair.Value.alpha = CockpitStatusSources.EmptiedStatusPanel(pair.Key) ? 0 : 1;
    }
    private void Release()
    {
        foreach (var group in _empty.Values) if (group != null) { group.alpha = 1; Destroy(group); }
        _empty.Clear(); _nextFind = 0;
    }
    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}
