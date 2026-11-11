using System.Collections.Generic;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.Rendering;

namespace NOVR.MotionControls;

// The HUD camera has its own tracking space and clears depth, so a widget
// behind the virtual stick or throttle was drawn over them: hold the stick in
// front of the cockpit map and the map covered it. Like PilotHandUiDepth for
// the gloves, these colourless copies give that camera the solid parts of the
// flight controls in its own coordinates, so a widget is hidden where a
// control is in front of it. See-through parts (the fading shaft end, the
// captions) do not hide anything. Nothing visible is added or changed.
[DefaultExecutionOrder(11000)]
internal sealed class FlightControlUiDepth : MonoBehaviour
{
    // The set of control meshes changes when the aircraft does or a model is
    // swapped; both change the object count below the controls' root. A
    // material that turned see-through on an existing object is caught by
    // this periodic pass.
    internal const float RescanSeconds = .5f;

    private sealed class Proxy
    {
        internal readonly MeshRenderer Source, Renderer;
        internal readonly MeshFilter SourceFilter, Filter;
        private readonly Transform _transform;
        private readonly Material _depth;
        internal bool Solid, Seen;

        internal Proxy(MeshRenderer source, MeshFilter sourceFilter, Transform holder, Material depth)
        {
            Source = source;
            SourceFilter = sourceFilter;
            _depth = depth;
            var go = new GameObject("NOVR control UI depth");
            _transform = go.transform;
            _transform.SetParent(holder, false);
            go.layer = (int)LayerHelper.GetVrUiLayer();
            Filter = go.AddComponent<MeshFilter>();
            Renderer = go.AddComponent<MeshRenderer>();
            Renderer.enabled = false;
            Renderer.shadowCastingMode = ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
            Renderer.lightProbeUsage = LightProbeUsage.Off;
            Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Renderer.allowOcclusionWhenDynamic = false;
        }

        internal bool Alive => Source != null && SourceFilter != null && Renderer != null;

        internal void Update(Transform gameRoot)
        {
            var mesh = SourceFilter.sharedMesh;
            if (!Solid || mesh == null || !Source.enabled || Source.forceRenderingOff || !Source.gameObject.activeInHierarchy)
            {
                Renderer.enabled = false;
                return;
            }
            if (Filter.sharedMesh != mesh)
            {
                Filter.sharedMesh = mesh;
                var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (var i = 0; i < materials.Length; i++)
                    materials[i] = _depth;
                Renderer.sharedMaterials = materials;
            }
            var source = Source.transform;
            _transform.localPosition = gameRoot.InverseTransformPoint(source.position);
            _transform.localRotation = Quaternion.Inverse(gameRoot.rotation) * source.rotation;
            _transform.localScale = Divide(source.lossyScale, gameRoot.lossyScale);
            Renderer.enabled = true;
        }

        internal void Dispose()
        {
            if (_transform != null)
                Destroy(_transform.gameObject);
        }

        private static Vector3 Divide(Vector3 value, Vector3 scale) => new(
            Ratio(value.x, scale.x), Ratio(value.y, scale.y), Ratio(value.z, scale.z));
        private static float Ratio(float value, float scale) => Mathf.Abs(scale) > .000001f ? value / scale : 1;
    }

    private readonly List<Proxy> _proxies = new();
    private readonly List<MeshRenderer> _found = new();
    private readonly List<Material> _materials = new();
    private Transform? _source, _holder;
    private int _size;
    private float _nextScan;
    private Material? _depth;
    private bool _unavailable;

    private void LateUpdate()
    {
        var gameCamera = APIBus.MainCamera;
        var uiCamera = APIBus.CockpitHudCamera;
        var gameRoot = gameCamera != null ? gameCamera.transform.parent : null;
        var uiRoot = uiCamera != null ? uiCamera.transform.parent : null;
        var controls = MotionControlVisuals.ActiveRoot;
        var ready = ModConfiguration.Instance.ControlsHideWidgets.Value && UnityEngine.XR.XRSettings.enabled &&
            gameRoot != null && uiRoot != null && controls != null &&
            CockpitHudWidgets.Instance?.Available == true && CameraStateManager.cameraMode == CameraMode.cockpit;
        if (!ready || !EnsureMaterial())
        {
            Hide();
            return;
        }
        if (_holder == null)
        {
            _holder = new GameObject("NOVR control UI depth copies").transform;
            _holder.gameObject.layer = (int)LayerHelper.GetVrUiLayer();
        }
        if (_holder.parent != uiRoot)
            _holder.SetParent(uiRoot, false);
        var size = controls!.hierarchyCount;
        var now = Time.unscaledTime;
        if (controls != _source || size != _size || now >= _nextScan)
        {
            _source = controls;
            _size = size;
            _nextScan = now + RescanSeconds;
            Scan(controls);
        }
        if (!_holder.gameObject.activeSelf)
            _holder.gameObject.SetActive(true);
        foreach (var proxy in _proxies)
            if (proxy.Alive)
                proxy.Update(gameRoot!);
    }

    // Keeps one copy per control mesh and notes which are solid.
    private void Scan(Transform controls)
    {
        foreach (var proxy in _proxies)
            proxy.Seen = false;
        _found.Clear();
        controls.GetComponentsInChildren(true, _found);
        foreach (var renderer in _found)
        {
            if (renderer == null)
                continue;
            Proxy? proxy = null;
            foreach (var candidate in _proxies)
                if (candidate.Alive && candidate.Source == renderer)
                {
                    proxy = candidate;
                    break;
                }
            if (proxy == null)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null)
                    continue;
                proxy = new Proxy(renderer, filter, _holder!, _depth!);
                _proxies.Add(proxy);
            }
            proxy.Seen = true;
            proxy.Solid = Solid(renderer);
        }
        _found.Clear();
        for (var i = _proxies.Count - 1; i >= 0; i--)
            if (!_proxies[i].Seen || !_proxies[i].Alive)
            {
                _proxies[i].Dispose();
                _proxies.RemoveAt(i);
            }
    }

    // A see-through part must not cut a hole in the widget behind it.
    private bool Solid(MeshRenderer renderer)
    {
        renderer.GetSharedMaterials(_materials);
        var solid = _materials.Count > 0;
        foreach (var material in _materials)
            if (material == null || material.renderQueue > (int)RenderQueue.AlphaTest + 50 ||
                material.HasProperty("_Surface") && material.GetFloat("_Surface") >= .5f)
            {
                solid = false;
                break;
            }
        _materials.Clear();
        return solid;
    }

    private bool EnsureMaterial()
    {
        if (_depth != null)
            return true;
        if (_unavailable)
            return false;
        _depth = PilotHandUiDepth.CreateDepthMaterial("NOVR control UI depth only");
        if (_depth != null)
            return true;
        _unavailable = true;
        Debug.LogWarning("[NOVR] Control/UI depth unavailable: required opaque blend properties missing; widgets keep drawing over the stick and throttle.");
        return false;
    }

    private void Hide()
    {
        if (_holder != null && _holder.gameObject.activeSelf)
            _holder.gameObject.SetActive(false);
    }

    private void OnDisable() => Hide();

    private void OnDestroy()
    {
        foreach (var proxy in _proxies)
            proxy.Dispose();
        _proxies.Clear();
        if (_holder != null)
            Destroy(_holder.gameObject);
        if (_depth != null)
            Destroy(_depth);
    }
}
