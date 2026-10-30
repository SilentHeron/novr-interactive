using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NOVR.MotionControls;

// Borrow the existing optical render for one frame. Native gameplay runs with
// its original camera state, and no second world render/texture is allocated.
internal sealed class OpticalCameraLease
{
    private static readonly AccessTools.FieldRef<TargetCam, GameObject> TargetCanvas = AccessTools.FieldRefAccess<TargetCam, GameObject>("canvasObjectTarget");
    private static readonly AccessTools.FieldRef<TargetCam, GameObject> LandingCanvas = AccessTools.FieldRefAccess<TargetCam, GameObject>("canvasObjectLanding");
    private Camera? _camera;
    private Vector3 _position;
    private Quaternion _rotation;
    private float _fov, _near, _far;
    private int _mask;
    private bool _enabled;
    private bool _borrowed;
    private GameObject? _targetRoot, _landingRoot;
    private readonly List<Canvas> _canvases = new();
    private readonly List<bool> _canvasEnabled = new();

    internal void Borrow(TargetCam sensor, Camera camera, Vector3 position, Quaternion rotation, float fov)
    {
        Restore();
        var targetRoot = TargetCanvas(sensor); var landingRoot = LandingCanvas(sensor);
        if (_targetRoot != targetRoot || _landingRoot != landingRoot)
        {
            _targetRoot = targetRoot; _landingRoot = landingRoot;
            _canvases.Clear(); _canvasEnabled.Clear();
            if (targetRoot != null) _canvases.AddRange(targetRoot.GetComponentsInChildren<Canvas>(true));
            if (landingRoot != null) _canvases.AddRange(landingRoot.GetComponentsInChildren<Canvas>(true));
            foreach (var _ in _canvases) _canvasEnabled.Add(false);
        }
        _camera = camera; _position = camera.transform.localPosition; _rotation = camera.transform.localRotation;
        _borrowed = true;
        _fov = camera.fieldOfView; _mask = camera.cullingMask; _enabled = camera.enabled;
        _near = camera.nearClipPlane; _far = camera.farClipPlane;
        for (var i = 0; i < _canvases.Count; i++)
        {
            var canvas = _canvases[i]; if (canvas == null) continue;
            _canvasEnabled[i] = canvas.enabled; canvas.enabled = false;
        }
        camera.cullingMask &= ~((1 << (int)LayerHelper.Layers.Cockpit) | (1 << (int)LayerHelper.GetVrUiLayer()) | (1 << 5));
        camera.fieldOfView = fov; camera.nearClipPlane = 2; camera.farClipPlane = 60000; camera.enabled = true;
        RefreshPose(position, rotation);
    }

    internal void RefreshPose(Vector3 position, Quaternion rotation)
    { if (_camera != null) _camera.transform.SetPositionAndRotation(position, rotation); }

    internal void Restore()
    {
        // Unity fake-null handles aircraft destruction between render callbacks.
        if (_camera != null)
        {
            _camera.transform.localPosition = _position; _camera.transform.localRotation = _rotation;
            _camera.fieldOfView = _fov; _camera.cullingMask = _mask; _camera.enabled = _enabled;
            _camera.nearClipPlane = _near; _camera.farClipPlane = _far;
        }
        if (_borrowed)
            for (var i = 0; i < _canvases.Count; i++)
                if (_canvases[i] != null) _canvases[i].enabled = _canvasEnabled[i];
        _camera = null; _borrowed = false;
    }
}

// Fallback restoration if a render frame is interrupted; runs before native
// TargetCam.Update and CombatHUD selection, not after their gameplay work.
[DefaultExecutionOrder(-2000)]
internal sealed class OpticalCameraRestore : MonoBehaviour
{
    private void Update() => OpticalTargetingHud.Instance?.RestoreCamera();
    private void FixedUpdate() => OpticalTargetingHud.Instance?.RestoreCamera();
}
