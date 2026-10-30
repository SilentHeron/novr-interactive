using System.Collections.Generic;
using HarmonyLib;
using NOVR.VrCamera;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NOVR.MotionControls;

// The universal console is rendered in the UI camera after the native cockpit.
// A small, final stereo pass keeps the user's handles and attached hands above
// that console without moving their physical positions or changing eye matrices.
[DefaultExecutionOrder(10100)]
internal sealed class CockpitForeground : MonoBehaviour
{
    private const int Layer = (int)LayerHelper.Layers.VrForeground;
    private static readonly Dictionary<Transform, List<(Renderer renderer, int layer)>> Roots = new();
    private Camera? _camera, _base;
    private List<Camera>? _stack;
    private bool _front, _mainHadLayer;
    private readonly List<Transform> _stale = new();

    internal static void Register(Transform root)
    {
        Unregister(root);
        var items = new List<(Renderer renderer, int layer)>();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) items.Add((renderer, renderer.gameObject.layer));
        Roots[root] = items;
    }
    internal static void Unregister(Transform root)
    {
        if (!Roots.TryGetValue(root, out var items)) return;
        foreach (var item in items) if (item.renderer != null) item.renderer.gameObject.layer = item.layer;
        Roots.Remove(root);
    }
    private void LateUpdate()
    {
        var main = APIBus.MainCamera;
        if (_base != main) { ReleaseCamera(); _base = main; }
        var front = UniversalCockpitPanel.DrawsPanel && main != null && main.GetComponent<NOVRPoseDriver>() != null;
        if (front && _camera == null)
        {
            _stack = main!.GetComponent<UniversalAdditionalCameraData>()?.cameraStack;
            if (_stack != null)
            {
                var go = new GameObject("NOVR handles above console"); go.transform.SetParent(main.transform, false);
                _camera = go.AddComponent<Camera>(); _camera.CopyFrom(main);
                _camera.cullingMask = 1 << Layer; _camera.clearFlags = CameraClearFlags.Depth;
                _camera.targetTexture = null; _camera.nearClipPlane = .01f; _camera.farClipPlane = 8;
                _camera.stereoTargetEye = StereoTargetEyeMask.Both;
                _camera.allowHDR = false; _camera.allowMSAA = false;
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderType = CameraRenderType.Overlay;
                AccessTools.Field(typeof(UniversalAdditionalCameraData), "m_ClearDepth").SetValue(data, true);
                data.renderPostProcessing = false; data.allowXRRendering = true;
                VrCameraManager.IgnoredCameras.Add(_camera);
            }
        }
        front &= _camera != null && _stack != null;
        if (_camera != null)
        {
            _camera.enabled = front;
            if (front && (_stack!.Count == 0 || _stack[_stack.Count - 1] != _camera))
            { _stack.Remove(_camera); _stack.Add(_camera); }
        }
        if (front && !_front) _mainHadLayer = main != null && (main.cullingMask & (1 << Layer)) != 0;
        if (front && main != null) main.cullingMask &= ~(1 << Layer);
        else if (_front && main != null && _mainHadLayer) main.cullingMask |= 1 << Layer;
        _stale.Clear();
        foreach (var entry in Roots) if (entry.Key == null) _stale.Add(entry.Key!);
        foreach (var root in _stale) Roots.Remove(root);
        // Roots may be replaced on an aircraft change even while the pass is on.
        foreach (var entry in Roots)
            foreach (var item in entry.Value)
                if (item.renderer != null)
                {
                    var layer = front ? Layer : item.layer;
                    if (item.renderer.gameObject.layer != layer) item.renderer.gameObject.layer = layer;
                }
        _front = front;
    }
    private void ReleaseCamera()
    {
        if (_front && _base != null && _mainHadLayer) _base.cullingMask |= 1 << Layer;
        // Restore before replacing a base camera, even when the next one also
        // needs the foreground pass. Its initial layer mask may differ.
        _front = false;
        if (_camera != null)
        {
            _stack?.Remove(_camera); VrCameraManager.IgnoredCameras.Remove(_camera);
            Destroy(_camera.gameObject);
        }
        _camera = null; _stack = null;
    }
    private void OnDisable()
    {
        foreach (var entry in Roots) foreach (var item in entry.Value)
            if (item.renderer != null) item.renderer.gameObject.layer = item.layer;
        ReleaseCamera();
    }
}
