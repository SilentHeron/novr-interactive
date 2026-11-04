using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

// Native notification text uses an additive shader. Cull its draw geometry
// instead of relying on alpha; its text, enabled state and timers keep running.
internal sealed class HudGraphicVisibilityLease
{
    private readonly Graphic _source;
    private readonly bool _children;
    private readonly List<CanvasRenderer> _renderers = new();
    private readonly Dictionary<CanvasRenderer, bool> _original = new();
    private bool _hidden;

    internal HudGraphicVisibilityLease(Graphic source, bool children = false)
    {
        _source = source;
        _children = children;
    }

    internal void Apply()
    {
        if (!_hidden)
        {
            _hidden = true;
            // Subscribe after the existing UI registry. Layout/mask rebuilds
            // may clear culling, so enforce it again immediately before draw.
            Canvas.willRenderCanvases += BeforeRender;
        }
        BeforeRender();
    }

    private void BeforeRender()
    {
        if (!_hidden || _source == null)
            return;
        _renderers.Clear();
        if (_children)
            _source.GetComponentsInChildren(true, _renderers);
        else
            _renderers.Add(_source.canvasRenderer);
        foreach (var renderer in _renderers)
        {
            if (renderer == null)
                continue;
            if (!_original.ContainsKey(renderer))
                _original.Add(renderer, renderer.cull);
            renderer.cull = true;
        }
    }

    internal void Restore()
    {
        if (!_hidden)
            return;
        _hidden = false;
        Canvas.willRenderCanvases -= BeforeRender;
        foreach (var pair in _original)
            if (pair.Key != null)
                pair.Key.cull = pair.Value;
        _original.Clear();
        _renderers.Clear();
        if (_source != null)
            _source.SetVerticesDirty();
    }

    internal void Release() => Restore();
}
