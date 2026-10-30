using System.Collections.Generic;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Only instrument groups are resized. Target, bombing, flight-path and threat
// projections keep their original world positions and angular alignment.
[DefaultExecutionOrder(80)]
internal sealed class FocusedFlightHud : MonoBehaviour
{
    private readonly Dictionary<Transform, (Vector3 position, Vector3 scale)> _instruments = new();
    private Transform? _compass;
    private Vector3 _compassPosition, _compassScale;
    private FlightHud? _hud;
    private float _nextFind;
    private void Awake() => _hud = GetComponent<FlightHud>();
    private void LateUpdate()
    {
        if (_hud == null) return;
        if (Time.unscaledTime >= _nextFind)
        {
            _nextFind = Time.unscaledTime + .5f;
            foreach (var manager in _hud.GetComponentsInChildren<HUDAppManager>(true))
                if (!_instruments.ContainsKey(manager.transform))
                {
                    _instruments.Add(manager.transform, (manager.transform.localPosition, manager.transform.localScale));
                    InstrumentBrightness.Add(manager.transform);
                }
            if (_compass == null)
            {
                _compass = _hud.GetHUDCenter().Find("compass");
                if (_compass != null) { InstrumentBrightness.Add(_compass); _compassPosition = _compass.localPosition; _compassScale = _compass.localScale; }
            }
        }
        var scale = ModConfiguration.Instance.FocusedFlightHud.Value ? ModConfiguration.Instance.FlightHudScale.Value : 1f;
        foreach (var pair in _instruments)
        {
            if (pair.Key == null) continue;
            pair.Key.localPosition = pair.Value.position * scale;
            pair.Key.localScale = pair.Value.scale * scale;
        }
        if (_compass != null)
        { _compass.localPosition = _compassPosition * scale; _compass.localScale = _compassScale * scale; }
    }
    private void OnDestroy()
    {
        foreach (var pair in _instruments)
            if (pair.Key != null) { pair.Key.localPosition = pair.Value.position; pair.Key.localScale = pair.Value.scale; }
        if (_compass != null) { _compass.localPosition = _compassPosition; _compass.localScale = _compassScale; }
    }
}
