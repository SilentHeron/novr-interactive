using System.Collections.Generic;
using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

// Only suppress the legacy floating status panels duplicated on the MFDs
// and the two empty native screen outlines left behind by integrated displays.
// The native HUDExtras and HMD own the flight/weapon readouts and must remain
// visible in their authored arrangement; FocusedFlightHud scales that layout.
[DefaultExecutionOrder(140)]
internal sealed class Fs20HudPresentation : MonoBehaviour
{
    private readonly Dictionary<Transform, (CanvasGroup group, float alpha, bool owned)> _hidden = new();
    private readonly Dictionary<Image, bool> _nativeFrames = new();
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
            if (GameManager.GetLocalAircraft(out var aircraft) && Fs20NativeCockpit.IsAircraft(aircraft) && aircraft.cockpit != null)
            {
                var screen = aircraft.cockpit.GetComponentInChildren<TacScreen>(true);
                if (screen != null && screen.name.StartsWith("tacScreen_smallFighter1"))
                {
                    CaptureFrame(screen.transform.Find("Canvas/border"));
                    CaptureFrame(screen.transform.Find("Canvas/Radar/border"));
                }
            }
        }
        foreach (var pair in _hidden)
            if (pair.Value.group != null)
                pair.Value.group.alpha = active && !UniversalCockpitPanel.IsDocked(pair.Key) ? 0 : pair.Value.alpha;
        foreach (var pair in _nativeFrames)
            if (pair.Key != null) pair.Key.enabled = active ? false : pair.Value;
    }
    private void CaptureFrame(Transform? frame)
    {
        var image = frame?.GetComponent<Image>();
        // These exact native objects are decorative sliced, unfilled rectangles,
        // outside the apps that move into our MFDs. Never hide a whole canvas or
        // radar viewport: the radar camera still supplies the tactical page.
        if (image == null || image.type != Image.Type.Sliced || image.fillCenter || _nativeFrames.ContainsKey(image)) return;
        _nativeFrames.Add(image, image.enabled);
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
        foreach (var pair in _nativeFrames) if (pair.Key != null) pair.Key.enabled = pair.Value;
        _nativeFrames.Clear(); _hidden.Clear(); _nextFind = 0;
    }
    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}
