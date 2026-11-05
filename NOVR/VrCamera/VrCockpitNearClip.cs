using UnityEngine;
using UnityEngine.XR;

namespace NOVR.VrCamera;

// Native cockpit entry gives the main view a 20 cm near plane. URP builds its XR
// eye layout from that base camera's clip range, even with a separate cockpit
// overlay camera. A tracked head can lean much closer to a side window. Keep
// the original glass/seat renderers and borrow only the tracked view's near plane.
[DefaultExecutionOrder(12000)]
internal sealed class VrCockpitNearClip : MonoBehaviour
{
    private const float CockpitNear = .03f;
    private Camera? _camera;
    private float _original, _written;
    private bool _owned;

    private void OnEnable() => _camera = GetComponent<Camera>();

    private void LateUpdate()
    {
        if (_camera == null || !_camera.isActiveAndEnabled ||
            _camera != APIBus.MainCamera || !XRSettings.enabled ||
            CameraStateManager.cameraMode != CameraMode.cockpit ||
            _camera.stereoTargetEye == StereoTargetEyeMask.None)
        {
            Restore();
            return;
        }

        var current = _camera.nearClipPlane;
        if (_owned && current == _written)
            return;

        // A native camera-state change or another mod may have written a new
        // value. Adopt it as the restore value; never resurrect a stale setting.
        _owned = false;
        if (float.IsNaN(current) || float.IsInfinity(current) || current <= CockpitNear)
            return;
        _original = current;
        _written = CockpitNear;
        _camera.nearClipPlane = _written;
        _owned = true;
    }

    private void Restore()
    {
        if (_owned && _camera != null && _camera.nearClipPlane == _written)
            _camera.nearClipPlane = _original;
        _owned = false;
    }

    private void OnDisable() => Restore();
    private void OnDestroy() => Restore();
}
