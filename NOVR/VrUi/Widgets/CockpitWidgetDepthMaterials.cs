using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

// Some native HUD font shaders deliberately ignore depth. Borrowed cockpit
// text needs ordinary world-space UI depth so nearby hands occlude it.
// Source materials and native HUD/menu graphics are never assigned or edited.
[DefaultExecutionOrder(240)]
internal sealed class CockpitWidgetDepthMaterials : MonoBehaviour
{
    // A widget's graphics are walked again only when something could have
    // added or removed one. Changes the cheap test cannot see (a component
    // added to an existing object, or one object replacing another within the
    // same frame) are picked up by this periodic pass instead.
    internal const float RescanSeconds = .25f;

    private readonly Dictionary<Graphic, CockpitWidgetDepthMaterial> _modifiers = new();
    private readonly List<Graphic> _graphics = new(), _gone = new();
    private readonly Transform?[] _roots = new Transform?[HudWidgetProfiles.Ids.Length];
    private readonly int[] _sizes = new int[HudWidgetProfiles.Ids.Length];
    private uint _generation;
    private float _nextScan;
    private bool _scanned;

    // The cockpit map alone holds every map icon, so walking each widget on
    // every frame cost time in proportion to the number of units in the
    // mission. A widget's set of graphics can only change when its canvas
    // becomes (un)available or its transform hierarchy gains or loses an
    // object, and both are a few property reads to test.
    private void LateUpdate()
    {
        var changed = !_scanned;
        for (var i = 0; i < _roots.Length; i++)
        {
            var root = Eligible(HudWidgetProfiles.Ids[i]);
            var size = root != null ? root.hierarchyCount : 0;
            if (!ReferenceEquals(root, _roots[i]) || size != _sizes[i])
                changed = true;
            _roots[i] = root;
            _sizes[i] = size;
        }
        var now = Time.unscaledTime;
        if (!changed && now < _nextScan)
            return;
        _scanned = true;
        _nextScan = now + RescanSeconds;
        Scan();
    }

    // The widget's world-space canvas while it can show content, else null.
    private static Transform? Eligible(string id)
    {
        if (!CockpitHudWidgets.TryGetContent(id, out var content) || content == null || content.parent == null)
            return null;
        var root = content.parent;
        var canvas = root.GetComponent<Canvas>();
        return canvas == null || canvas.renderMode != RenderMode.WorldSpace || !root.gameObject.activeInHierarchy ? null : root;
    }

    private void Scan()
    {
        ++_generation;
        foreach (var root in _roots)
        {
            if (root == null)
                continue;
            _graphics.Clear();
            root.GetComponentsInChildren(true, _graphics);
            foreach (var graphic in _graphics)
            {
                if (graphic == null)
                    continue;
                if (!_modifiers.TryGetValue(graphic, out var modifier) || modifier == null)
                {
                    modifier = graphic.GetComponent<CockpitWidgetDepthMaterial>();
                    if (modifier == null)
                        modifier = graphic.gameObject.AddComponent<CockpitWidgetDepthMaterial>();
                    _modifiers[graphic] = modifier;
                }
                modifier.Attach(graphic, root, _generation);
            }
        }
        _graphics.Clear();
        _gone.Clear();
        foreach (var pair in _modifiers)
        {
            if (pair.Key == null || pair.Value == null)
            {
                _gone.Add(pair.Key!);
                continue;
            }
            if (pair.Value.Generation != _generation)
                pair.Value.Detach();
        }
        foreach (var graphic in _gone)
            _modifiers.Remove(graphic);
        _gone.Clear();
    }

    private void OnDisable()
    {
        foreach (var modifier in _modifiers.Values)
            if (modifier != null)
                modifier.Detach();
        // Every modifier is detached now; attach them again when re-enabled.
        _scanned = false;
    }
    private void OnDestroy()
    {
        foreach (var modifier in _modifiers.Values)
            if (modifier != null)
            {
                modifier.Detach();
                Destroy(modifier);
            }
        _modifiers.Clear();
        _graphics.Clear();
        _gone.Clear();
    }
}

// Runs after the Graphic's normal stencil/RectMask material modifiers. Cache
// the resulting material, not the shared font asset or unmasked source.
internal sealed class CockpitWidgetDepthMaterial : MonoBehaviour, IMaterialModifier
{
    private Graphic? _graphic;
    private Transform? _root;
    private Material? _source, _copy;
    private Shader? _target;
    private static Shader? _font;
    internal uint Generation
    {
        get; private set;
    }

    internal void Attach(Graphic graphic, Transform root, uint generation)
    {
        Generation = generation;
        if (_graphic == graphic && _root == root)
            return;
        _graphic = graphic;
        _root = root;
        _graphic.SetMaterialDirty();
    }

    public Material GetModifiedMaterial(Material baseMaterial)
    {
        if (!InScope() || baseMaterial == null)
        {
            ClearCopy();
            return baseMaterial!;
        }
        var target = Replacement(baseMaterial.shader);
        if (target == null)
        {
            ClearCopy();
            return baseMaterial;
        }
        if (_copy == null || _source != baseMaterial || _target != target)
        {
            ClearCopy();
            _source = baseMaterial;
            _target = target;
            _copy = new Material(baseMaterial) { name = "NOVR cockpit depth / " + baseMaterial.name, shader = target };
        }
        else
        {
            // Native font/stencil materials can change properties in place.
            // This callback runs on UI material rebuilds, not once per eye.
            _copy.CopyPropertiesFromMaterial(baseMaterial);
            _copy.shader = target;
        }
        _copy.renderQueue = (int)RenderQueue.Transparent;
        // The verified native font alternative uses unity_GUIZTestMode. Unity
        // supplies LEqual for a WorldSpace canvas; do not override its global
        // value (screen-space menus intentionally use a different value).
        if (_copy.HasProperty("_ZTestMode"))
            _copy.SetInt("_ZTestMode", (int)CompareFunction.LessEqual);
        return _copy;
    }

    private bool InScope() => isActiveAndEnabled && _graphic != null && _root != null &&
        _root.gameObject.activeInHierarchy && (_graphic.transform == _root || _graphic.transform.IsChildOf(_root)) &&
        _graphic.canvas != null && _graphic.canvas.renderMode == RenderMode.WorldSpace;

    private static Shader? Replacement(Shader source)
    {
        if (source == null)
            return null;
        // UI/AdditiveMasked already uses unity_GUIZTestMode, including its
        // stencil/clip variants. Keep its additive blend: native map-grid
        // textures have opaque black pixels that must not cover the terrain.
        // Only these inspected native font shaders hard-code ZTest Always.
        var name = source.name;
        if (name == "TextMeshPro/Distance Field Additive" ||
            name == "TextMeshPro/Mobile/Distance Field Additive" ||
            name == "TextMeshPro/Distance Field Additive with Glow" ||
            name == "TextMeshPro/Distance Field Overlay")
            return _font != null ? _font : _font = Shader.Find("TextMeshPro/Mobile/Distance Field");
        return null;
    }

    internal void Detach()
    {
        if (_root == null && _copy == null)
            return;
        _root = null;
        ClearCopy();
        if (_graphic != null)
            _graphic.SetMaterialDirty();
    }
    private void ClearCopy()
    {
        if (_copy != null)
            Destroy(_copy);
        _copy = null;
        _source = null;
        _target = null;
    }
    private void OnTransformParentChanged()
    {
        if (!InScope())
            Detach();
        else if (_graphic != null)
            _graphic.SetMaterialDirty();
    }
    // The game hides and shows map icons and readouts by toggling them. Keep
    // the widget they belong to while they are off (only the copied material
    // is released), so they are in scope again on the frame they come back
    // rather than at the manager's next scan.
    private void OnDisable()
    {
        ClearCopy();
        if (_graphic != null)
            _graphic.SetMaterialDirty();
    }
    private void OnEnable()
    {
        if (_root != null && _graphic != null)
            _graphic.SetMaterialDirty();
    }
    private void OnDestroy() => Detach();
}
