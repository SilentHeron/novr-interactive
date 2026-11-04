using System;
using NOVR.VrUi;
using UnityEngine;

namespace NOVR;

public class APIBus : MonoBehaviour
{
    // Normalize Unity's destroyed references for callers that use ?. or CLR
    // null checks during a scene transition.
    public static Camera MainCamera => _previousMainCamera != null ? _previousMainCamera : null!;
    public static Camera CockpitHudCamera => NOUIManager.I != null ? NOUIManager.I.CockpitHudCamera : null!;
    public static GameObject CockpitHudReference => NOUIManager.I != null ? NOUIManager.I.CockpitHudReference : null!;
    public static double AngleFromZero;
    public static Action<Camera?, Camera?>? OnMainCameraChanged;

    private static Camera? _previousMainCamera;
    private static APIBus? _current;
    private bool _loggedExtraEventsError;

    private void Awake() => TryClaimOwnership();
    private void OnEnable() => TryClaimOwnership();

    public void Update()
    {
        if (!TryClaimOwnership()) return;
        var camera = Camera.main;
        Publish(camera != null ? camera : null);
    }

    private bool TryClaimOwnership()
    {
        if (!isActiveAndEnabled) return false;
        if (ReferenceEquals(_current, this)) return true;
        if (_current == null || !_current.isActiveAndEnabled)
        {
            _current = this;
            return true;
        }
        if (!_loggedExtraEventsError)
        {
            Debug.LogWarning("[NOVR] Additional camera dispatcher is waiting for the active instance to stop.");
            _loggedExtraEventsError = true;
        }
        return false;
    }

    private static void Publish(Camera? camera)
    {
        // Unity considers a destroyed camera equal to null, but subscribers
        // still need that loss notification and the stale reference cleared.
        if (ReferenceEquals(_previousMainCamera, camera)) return;
        var previous = _previousMainCamera;
        _previousMainCamera = camera;
        // Publish the cache first. A failing subscriber must not make the
        // same camera transition replay on every subsequent frame.
        OnMainCameraChanged?.Invoke(previous, camera);
    }

    private void ReleaseOwnership()
    {
        if (!ReferenceEquals(_current, this)) return;
        _current = null;
        Publish(null);
    }
    private void OnDisable() => ReleaseOwnership();
    private void OnDestroy() => ReleaseOwnership();
}
