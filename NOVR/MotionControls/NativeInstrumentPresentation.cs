using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace NOVR.MotionControls;

// Own only presentation, never aircraft/sensor state. The live apps are also
// the sources of our MFD pages, so destroying the native hierarchy is unsafe.
internal sealed class NativeInstrumentPresentation : IDisposable
{
    private sealed class Surface
    {
        internal Renderer Renderer = null!;
        internal bool Hidden;
        internal MeshFilter? Filter;
        internal Mesh? Original, Replacement;
    }
    private readonly List<Surface> _surfaces = new();
    private readonly HashSet<Renderer> _seen = new();
    private Aircraft? _aircraft;
    private Camera? _camera;
    private Canvas? _canvas;
    private bool _cameraEnabled, _canvasEnabled, _suspended, _visible;
    private float _nextFind;
    private int _findAttempts;
    internal static bool Replacing { get; private set; }
    internal static bool NeedsTactical => ModConfiguration.Instance.LeftMfdPage.Value == MfdPage.Tactical ||
                                          ModConfiguration.Instance.RightMfdPage.Value == MfdPage.Tactical;

    internal void Update(Aircraft aircraft, bool visible)
    {
        if (_aircraft != aircraft) { Dispose(); _aircraft = aircraft; _nextFind = 0; _findAttempts = 0; }
        if (visible && _findAttempts < 6 && Time.unscaledTime >= _nextFind)
        {
            _nextFind = Time.unscaledTime + 2; _findAttempts++;
            Discover(aircraft);
        }
        SetVisible(visible);
        var suspend = visible && !NeedsTactical && _camera != null && _canvas != null;
        if (suspend != _suspended)
        {
            if (suspend)
            {
                _cameraEnabled = _camera != null && _camera.enabled;
                _canvasEnabled = _canvas != null && _canvas.enabled;
            }
            if (_camera != null) _camera.enabled = suspend ? false : _cameraEnabled;
            if (_canvas != null) _canvas.enabled = suspend ? false : _canvasEnabled;
            _suspended = suspend;
        }
    }

    private void Discover(Aircraft aircraft)
    {
        var materials = new HashSet<Material>();
        foreach (var tac in aircraft.cockpit.GetComponentsInChildren<TacScreen>(true))
        {
            if (AccessTools.Field(typeof(TacScreen), "screenMaterial")?.GetValue(tac) is Material material) materials.Add(material);
            if (_camera == null) _camera = AccessTools.Field(typeof(TacScreen), "cam")?.GetValue(tac) as Camera;
            if (_canvas == null) _canvas = AccessTools.Field(typeof(TacScreen), "canvas")?.GetValue(tac) as Canvas;
        }
        // The target screen can be a second, separate renderer, or a material
        // slot in a combined cockpit mesh. Match its live render texture only.
        var sensor = aircraft.targetCam;
        var sensorCamera = sensor == null ? null : AccessTools.Field(typeof(TargetCam), "cam")?.GetValue(sensor) as Camera;
        var sensorTexture = sensorCamera?.targetTexture;
        var candidates = new HashSet<Renderer>();
        foreach (var root in new[] { aircraft.transform, aircraft.cockpit.transform })
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) candidates.Add(renderer);
        foreach (var renderer in candidates)
        {
            if (_seen.Contains(renderer)) continue;
            var slots = renderer.sharedMaterials; var removed = new HashSet<int>();
            for (var i = 0; i < slots.Length; i++)
                if (slots[i] != null && (materials.Contains(slots[i]) || sensorTexture != null && slots[i].mainTexture == sensorTexture)) removed.Add(i);
            if (removed.Count == 0) continue;
            var surface = new Surface { Renderer = renderer, Hidden = renderer.forceRenderingOff };
            try
            {
                if (removed.Count != slots.Length)
                {
                    surface.Filter = renderer.GetComponent<MeshFilter>();
                    surface.Original = surface.Filter?.sharedMesh;
                    if (surface.Original == null) continue;
                    surface.Replacement = WithoutScreenSlots(surface.Original, removed);
                    if (surface.Replacement == null) continue;
                }
                _surfaces.Add(surface); _seen.Add(renderer);
                if (_visible) Apply(surface, true);
            }
            catch (Exception e) { Debug.LogWarning("[NOVR] Native display suppression unavailable for " + renderer.name + ": " + e.Message); }
        }
    }

    // Preserve every vertex stream, material slot and non-screen submesh. Some
    // imported meshes have no CPU copy, so use a bounded one-time GPU readback.
    private static Mesh? WithoutScreenSlots(Mesh original, HashSet<int> removed)
    {
        var descriptors = original.GetVertexAttributes();
        var mesh = new Mesh { name = original.name + " (VR instruments hidden)", indexFormat = original.indexFormat };
        try
        {
            mesh.SetVertexBufferParams(original.vertexCount, descriptors);
            for (var stream = 0; stream < original.vertexBufferCount; stream++)
            {
                using var buffer = original.GetVertexBuffer(stream);
                if (buffer == null || (long)buffer.count * buffer.stride > 16 * 1024 * 1024) { UnityEngine.Object.Destroy(mesh); return null; }
                var bytes = new byte[buffer.count * buffer.stride]; buffer.GetData(bytes);
                mesh.SetVertexBufferData(bytes, 0, 0, bytes.Length, stream, MeshUpdateFlags.DontRecalculateBounds);
            }
            using var indices = original.GetIndexBuffer();
            if (indices == null || (long)indices.count * indices.stride > 16 * 1024 * 1024) { UnityEngine.Object.Destroy(mesh); return null; }
            var data = new byte[indices.count * indices.stride]; indices.GetData(data);
            mesh.SetIndexBufferParams(data.Length / (original.indexFormat == IndexFormat.UInt16 ? 2 : 4), original.indexFormat);
            mesh.SetIndexBufferData(data, 0, 0, data.Length, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.subMeshCount = original.subMeshCount;
            for (var i = 0; i < original.subMeshCount; i++)
            {
                var sub = original.GetSubMesh(i);
                if (removed.Contains(i)) sub.indexCount = 0;
                mesh.SetSubMesh(i, sub, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            }
            mesh.bounds = original.bounds;
            return mesh;
        }
        catch { UnityEngine.Object.Destroy(mesh); throw; }
    }
    private static void Apply(Surface surface, bool hide)
    {
        if (surface.Renderer == null) return;
        if (surface.Replacement == null) surface.Renderer.forceRenderingOff = hide || surface.Hidden;
        else if (surface.Filter != null && (surface.Filter.sharedMesh == surface.Original || surface.Filter.sharedMesh == surface.Replacement))
            surface.Filter.sharedMesh = hide ? surface.Replacement : surface.Original;
    }
    internal void SetVisible(bool visible)
    {
        if (_visible != visible) { foreach (var surface in _surfaces) Apply(surface, visible); _visible = visible; }
        Replacing = visible;
        if (!visible && _suspended)
        {
            if (_camera != null) _camera.enabled = _cameraEnabled;
            if (_canvas != null) _canvas.enabled = _canvasEnabled;
            _suspended = false;
        }
    }
    public void Dispose()
    {
        SetVisible(false);
        foreach (var surface in _surfaces) if (surface.Replacement != null) UnityEngine.Object.Destroy(surface.Replacement);
        _surfaces.Clear(); _seen.Clear(); _camera = null; _canvas = null; _aircraft = null;
    }
}

[HarmonyPatch(typeof(MFDAppManager), "Update")]
internal static class NativeMfdRefreshPatch
{
    [HarmonyPrefix]
    private static bool Prefix(HUDApp[] ___apps)
    {
        if (!NativeInstrumentPresentation.Replacing || NativeInstrumentPresentation.NeedsTactical) return true;
        foreach (var app in ___apps)
            if (app != null && app.gameObject.activeInHierarchy && UniversalCockpitPanel.IsDocked(app.transform)) app.Refresh();
        return false;
    }
}

[HarmonyPatch(typeof(TacScreen), "Update")]
internal static class NativeTacticalAnimationPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => !NativeInstrumentPresentation.Replacing || NativeInstrumentPresentation.NeedsTactical;
}
