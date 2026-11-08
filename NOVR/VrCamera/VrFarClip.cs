using UnityEngine;
using UnityEngine.XR;

namespace NOVR.VrCamera;

// The gameplay camera stops rendering 80 km out. That cut is a plane
// perpendicular to the view direction: a forward-facing screen never notices
// it, but a head-tracked view sees the wall of fogged terrain pivot with every
// head turn once the air is clear enough at altitude. Lease only the tracked VR
// view's far plane, extend it only beyond the game's own value, and give it
// back whenever the view stops being the stereo world camera.
[DefaultExecutionOrder(12000)]
internal sealed class VrFarClip : MonoBehaviour
{
    private Camera? _camera;
    private float _original, _written, _requested;
    private bool _owned;

    private void OnEnable() => _camera = GetComponent<Camera>();

    private void LateUpdate()
    {
        if (_camera == null || !_camera.isActiveAndEnabled ||
            _camera != APIBus.MainCamera || !XRSettings.enabled ||
            _camera.stereoTargetEye == StereoTargetEyeMask.None)
        {
            Restore();
            return;
        }

        var requested = ModConfiguration.Instance.VrFarClipDistance.Value;
        var current = _camera.farClipPlane;
        if (_owned && current == _written)
        {
            if (requested == _requested)
                return;
            // Only the preference changed: re-evaluate from the game's value.
            _camera.farClipPlane = _original;
            current = _original;
        }

        // A native camera-state change or another mod may have written a new
        // value. Adopt it as the restore value; never resurrect a stale setting.
        _owned = false;
        _requested = requested;
        if (!VrFarClipPolicy.TryExtend(current, requested, out var extended))
            return;
        _original = current;
        _written = extended;
        _camera.farClipPlane = _written;
        _owned = true;
    }

    private void Restore()
    {
        if (_owned && _camera != null && _camera.farClipPlane == _written)
            _camera.farClipPlane = _original;
        _owned = false;
    }

    private void OnDisable() => Restore();
    private void OnDestroy() => Restore();
}
