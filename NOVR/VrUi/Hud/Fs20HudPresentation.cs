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
    private int _version = -1;
    private FlightHud? _hud;
    private void Awake() => _hud = GetComponent<FlightHud>();
    private void LateUpdate()
    {
        if (_hud != null && _version != HudReadability.PanelsVersion)
        {
            _version = HudReadability.PanelsVersion;
            foreach (var panel in HudReadability.Panels)
                if (panel != null && panel.transform.IsChildOf(_hud.transform))
                    _panels.Add(panel);
        }
        // One component owns the existing group's alpha; Unity rejects a second
        // CanvasGroup, and two independent writers would fight over opacity.
        foreach (var panel in _panels)
            if (panel != null)
                panel.SetWidgetSourcesMoved(CockpitStatusSources.EmptiedStatusPanel(panel.transform));
    }
    private void Release()
    {
        foreach (var panel in _panels)
            if (panel != null)
                panel.SetWidgetSourcesMoved(false);
        _panels.Clear();
        _version = -1;
    }
    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}
