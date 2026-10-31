using System.Collections.Generic;
using NOVR.MotionControls;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Keep the legacy floating status panels out of the FS-20 cockpit view.
// Native cockpit displays, including their frames and apps, remain untouched.
// The native HUDExtras and HMD own the flight/weapon readouts and must remain
// visible in their authored arrangement; FocusedFlightHud scales that layout.
[DefaultExecutionOrder(140)]
internal sealed class Fs20HudPresentation : MonoBehaviour
{
    private readonly Dictionary<Transform, (CanvasGroup group, float alpha, bool owned)> _hidden = new();
    private float _nextFind;
    private FlightHud? _hud;
    private void Awake() => _hud = GetComponent<FlightHud>();
    private void LateUpdate()
    {
        var active = Fs20NativeCockpit.Active;
        if (active && _hud != null && Time.unscaledTime >= _nextFind)
        {
            _nextFind = Time.unscaledTime + .5f;
            foreach (var panel in _hud.GetComponentsInChildren<HudReadability>(true)) Hide(panel.transform);
        }
        foreach (var pair in _hidden)
            if (pair.Value.group != null)
                pair.Value.group.alpha = active && !UniversalCockpitPanel.IsDocked(pair.Key) ? 0 : pair.Value.alpha;
    }
    private void Hide(Transform source)
    {
        if (_hidden.ContainsKey(source) || UniversalCockpitPanel.IsDocked(source)) return;
        var group = source.GetComponent<CanvasGroup>(); var owned = group == null;
        if (owned) group = source.gameObject.AddComponent<CanvasGroup>();
        if (group != null) _hidden.Add(source, (group, group.alpha, owned));
    }
    private void Release()
    {
        foreach (var pair in _hidden) if (pair.Value.group != null) { pair.Value.group.alpha = pair.Value.alpha; if (pair.Value.owned) Destroy(pair.Value.group); }
        _hidden.Clear(); _nextFind = 0;
    }
    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}
