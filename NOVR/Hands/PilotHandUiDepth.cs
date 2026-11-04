using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NOVR.VrUi.SpecialBehavior;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

// The HUD has its own tracking space and clears depth. These colourless copies
// give that camera the displayed hand geometry in its own coordinates. The
// ordinary cockpit still supplies every visible glove/sleeve pixel.
[DefaultExecutionOrder(11000)]
internal sealed class PilotHandUiDepth : MonoBehaviour
{
    private sealed class Proxy : IDisposable
    {
        internal readonly SkinnedMeshRenderer Source, Renderer;
        private readonly Transform _root;
        private readonly Transform[] _sources, _bones;
        private readonly List<Material> _sourceMaterials = new();
        private readonly Transform _sourceRootBone;
        internal Proxy(SkinnedMeshRenderer source, Transform uiRoot, Material depth)
        {
            Source = source;
            _sources = source.bones;
            _sourceRootBone = source.rootBone;
            _root = new GameObject("NOVR hand UI depth").transform;
            _root.SetParent(uiRoot, false);
            _root.gameObject.layer = (int)LayerHelper.GetVrUiLayer();
            _bones = new Transform[_sources.Length];
            for (var i = 0; i < _bones.Length; i++)
            {
                _bones[i] = new GameObject("UI depth bone").transform;
                _bones[i].SetParent(_root, false);
            }
            Renderer = _root.gameObject.AddComponent<SkinnedMeshRenderer>();
            Renderer.enabled = false;
            Renderer.sharedMesh = source.sharedMesh;
            Renderer.bones = _bones;
            var rootIndex = Array.IndexOf(_sources, source.rootBone);
            Renderer.rootBone = rootIndex >= 0 ? _bones[rootIndex] : _root;
            var materials = new Material[Math.Max(1, source.sharedMesh.subMeshCount)];
            for (var i = 0; i < materials.Length; i++)
                materials[i] = depth;
            Renderer.sharedMaterials = materials;
            Renderer.shadowCastingMode = ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
            Renderer.lightProbeUsage = LightProbeUsage.Off;
            Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Renderer.allowOcclusionWhenDynamic = false;
            Renderer.updateWhenOffscreen = true;
        }

        internal bool Matches(SkinnedMeshRenderer? source) => Source != null && source == Source && Renderer != null &&
            Renderer.sharedMesh == Source.sharedMesh && _sourceRootBone == Source.rootBone;
        internal void Hide()
        {
            if (_root != null)
                _root.gameObject.SetActive(false);
        }
        internal void Update(Transform gameRoot, Transform uiRoot, bool allowed)
        {
            if (!allowed || Source == null || !Source.enabled || !Source.gameObject.activeInHierarchy || Source.sharedMesh == null)
            {
                Hide();
                return;
            }
            // A nearly hidden sleeve must not carve an opaque silhouette out of
            // a panel. Gloves remain opaque; their visible surface owns contact.
            Source.GetSharedMaterials(_sourceMaterials);
            foreach (var material in _sourceMaterials)
                if (material == null || material.HasProperty("_Surface") && material.GetFloat("_Surface") >= .5f)
                {
                    Hide();
                    return;
                }
            // Every bone uses the same renderer basis this frame. Keep those
            // native transform reads/inversion outside the skinning palette loop.
            var sourceTransform = Source.transform;
            var sourceRotation = sourceTransform.rotation;
            var sourceInverseRotation = Quaternion.Inverse(sourceRotation);
            var sourceScale = sourceTransform.lossyScale;
            if (_root.parent != uiRoot)
                _root.SetParent(uiRoot, false);
            _root.localPosition = gameRoot.InverseTransformPoint(sourceTransform.position);
            _root.localRotation = Quaternion.Inverse(gameRoot.rotation) * sourceRotation;
            _root.localScale = Divide(sourceScale, gameRoot.lossyScale);
            for (var i = 0; i < _bones.Length; i++)
            {
                var source = _sources[i];
                if (source == null)
                {
                    Hide();
                    return;
                }
                _bones[i].localPosition = sourceTransform.InverseTransformPoint(source.position);
                _bones[i].localRotation = sourceInverseRotation * source.rotation;
                _bones[i].localScale = Divide(source.lossyScale, sourceScale);
            }
            Renderer.localBounds = Source.localBounds;
            Renderer.enabled = true;
            _root.gameObject.SetActive(true);
        }
        private static Vector3 Divide(Vector3 value, Vector3 scale) => new(
            Ratio(value.x, scale.x), Ratio(value.y, scale.y), Ratio(value.z, scale.z));
        private static float Ratio(float value, float scale) => Mathf.Abs(scale) > .000001f ? value / scale : 1;
        public void Dispose()
        {
            if (_root != null)
                Object.Destroy(_root.gameObject);
        }
    }

    private readonly Proxy?[] _proxies = new Proxy?[4];
    private Material? _depth;
    private bool _unavailable;

    private bool EnsureMaterial()
    {
        if (_depth != null)
            return true;
        if (_unavailable)
            return false;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
        {
            var material = new Material(shader) { name = "NOVR hand UI depth only", renderQueue = (int)RenderQueue.Geometry };
            // The installed Lit DepthOnly pass writes the red channel. Use its
            // normal opaque pass with colour/alpha blending that keeps the
            // destination exactly intact, and write only the hand depth.
            var required = new[] { "_SrcBlend", "_DstBlend", "_SrcBlendAlpha", "_DstBlendAlpha", "_ZWrite", "_Surface" };
            var supported = material.FindPass("ForwardLit") >= 0;
            foreach (var property in required)
                supported &= material.HasProperty(property);
            if (supported)
            {
                material.SetFloat("_Surface", 0);
                material.SetFloat("_ZWrite", 1);
                material.SetFloat("_SrcBlend", (float)BlendMode.Zero);
                material.SetFloat("_DstBlend", (float)BlendMode.One);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.Zero);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_AlphaClip", 0);
                material.SetFloat("_AlphaToMask", 0);
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                material.SetFloat("_Smoothness", 0);
                material.SetFloat("_Metallic", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                _depth = material;
                return true;
            }
            Object.Destroy(material);
        }
        _unavailable = true;
        Debug.LogWarning("[NOVR] Hand/UI depth unavailable: required opaque blend properties missing; original rendering retained.");
        return false;
    }

    private void LateUpdate()
    {
        var gameCamera = APIBus.MainCamera;
        var uiCamera = APIBus.CockpitHudCamera;
        var gameRoot = gameCamera != null ? gameCamera.transform.parent : null;
        var uiRoot = uiCamera != null ? uiCamera.transform.parent : null;
        var ready = UnityEngine.XR.XRSettings.enabled && gameRoot != null && uiRoot != null &&
            CockpitHudWidgets.Instance?.Available == true && CameraStateManager.cameraMode == CameraMode.cockpit;
        if (!ready)
        {
            Hide();
            return;
        }
        for (var i = 0; i < _proxies.Length; i++)
        {
            var source = PilotHandVisuals.GetRenderer(i);
            if (_proxies[i] != null && !_proxies[i]!.Matches(source))
            {
                _proxies[i]!.Dispose();
                _proxies[i] = null;
            }
            if (source != null && source.sharedMesh != null && _proxies[i] == null && EnsureMaterial())
                _proxies[i] = new Proxy(source, uiRoot!, _depth!);
            _proxies[i]?.Update(gameRoot!, uiRoot!, source != null);
        }
    }
    private void Hide()
    {
        foreach (var proxy in _proxies)
            proxy?.Hide();
    }
    private void OnDisable() => Hide();
    private void OnDestroy()
    {
        foreach (var proxy in _proxies)
            proxy?.Dispose();
        if (_depth != null)
            Object.Destroy(_depth);
    }
}
