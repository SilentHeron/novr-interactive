using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NOVR.MotionControls;

// Only the locally owned sleeve material copies are changed. Full reach keeps
// their normal opaque depth rendering; transparency is used only in the narrow
// fade band. Cockpit, seat and original pilot materials are never touched.
internal sealed class PilotSleeveFade
{
    private readonly Material[] _materials;
    private readonly Color[] _base, _color;
    private readonly int[] _queues;
    private readonly string[] _renderTypes;
    private readonly float[,] _original;
    private readonly bool[] _transparent;
    private static readonly string[] Properties = { "_Surface", "_Blend", "_SrcBlend", "_DstBlend", "_ZWrite" };
    private readonly bool _supported;
    private bool _fading;
    private float _opacity = -1;

    internal PilotSleeveFade(Material[] materials)
    {
        _materials = materials;
        _base = new Color[materials.Length];
        _color = new Color[materials.Length];
        _queues = new int[materials.Length];
        _renderTypes = new string[materials.Length];
        _original = new float[materials.Length, Properties.Length];
        _transparent = new bool[materials.Length];
        _supported = materials.Length > 0;
        for (var i = 0; i < materials.Length; i++)
        {
            var m = materials[i];
            _base[i] = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            _color[i] = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            _queues[i] = m.renderQueue;
            _renderTypes[i] = m.GetTag("RenderType", false, "");
            _transparent[i] = m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");
            for (var p = 0; p < Properties.Length; p++)
            {
                if (!m.HasProperty(Properties[p]))
                    _supported = false;
                else
                    _original[i, p] = m.GetFloat(Properties[p]);
            }
            if (!m.HasProperty("_BaseColor"))
                _supported = false;
        }
    }
    internal bool Apply(float opacity)
    {
        opacity = Mathf.Clamp01(opacity);
        if (!_supported)
            return opacity > 0; // Unknown shaders retain their original material.
        if (Mathf.Abs(_opacity - opacity) < .0001f)
            return opacity > .001f;
        _opacity = opacity;
        var fading = opacity < .9999f;
        for (var i = 0; i < _materials.Length; i++)
        {
            var m = _materials[i];
            if (fading != _fading)
            {
                if (fading)
                {
                    m.SetFloat("_Surface", 1);
                    m.SetFloat("_Blend", 0);
                    m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.renderQueue = (int)RenderQueue.Transparent;
                }
                else
                {
                    for (var p = 0; p < Properties.Length; p++)
                        m.SetFloat(Properties[p], _original[i, p]);
                    if (_transparent[i])
                        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    else
                        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.SetOverrideTag("RenderType", _renderTypes[i]);
                    m.renderQueue = _queues[i];
                }
            }
            var tint = _base[i];
            tint.a *= opacity;
            m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Color"))
            {
                tint = _color[i];
                tint.a *= opacity;
                m.SetColor("_Color", tint);
            }
        }
        _fading = fading;
        return opacity > .001f;
    }
}
