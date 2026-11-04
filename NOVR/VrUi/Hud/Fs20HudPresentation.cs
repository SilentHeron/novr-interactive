using System.Collections.Generic;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Suppress only a status container emptied by successful widget leases. The old
// FS-20 blanket hide lost weapon/CM/capacitor readouts after its MFDs were removed.
// Flight instruments and native threat fallbacks remain game-owned and visible.
[DefaultExecutionOrder(140)]
internal sealed class Fs20HudPresentation : MonoBehaviour
{
    private readonly HashSet<HudReadability> _panels = new();
    private float _nextFind;
    private FlightHud? _hud;
    private void Awake() => _hud = GetComponent<FlightHud>();
    private void LateUpdate()
    {
        if (_hud != null && Time.unscaledTime >= _nextFind)
        {
            _nextFind = Time.unscaledTime + .5f;
            foreach (var panel in _hud.GetComponentsInChildren<HudReadability>(true))
                _panels.Add(panel);
        }
        // One component owns the existing group's alpha; Unity rejects a second
        // CanvasGroup, and two independent writers would fight over opacity.
        foreach (var panel in _panels)
            if (panel != null) panel.SetWidgetSourcesMoved(CockpitStatusSources.EmptiedStatusPanel(panel.transform));
    }
    private void Release()
    {
        foreach (var panel in _panels) if (panel != null) panel.SetWidgetSourcesMoved(false);
        _panels.Clear(); _nextFind = 0;
    }
    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}
