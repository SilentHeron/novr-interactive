using System.Collections.Generic;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Hide drawing, not native HUD logic. Additive HUD shaders need their canvas
// disabled: multiplying CanvasGroup alpha does not reliably hide their pixels.
internal sealed class HudCanvasVisibilityLease
{
    private readonly Dictionary<Canvas, bool> _states = new();
    private readonly List<Canvas> _canvases = new();
    private readonly List<Canvas> _released = new();

    internal void Apply(Transform root, bool hidden)
    {
        if (!hidden) { Restore(); return; }
        // The native tactical map leaves FlightHud when maximized. Restore its
        // canvas immediately; it must keep its map and respawn controls.
        foreach (var pair in _states)
        {
            var canvas = pair.Key;
            if (canvas != null && (canvas.transform == root || canvas.transform.IsChildOf(root))) continue;
            if (canvas != null) canvas.enabled = pair.Value;
            _released.Add(canvas!);
        }
        foreach (var canvas in _released) _states.Remove(canvas);
        _released.Clear();
        _canvases.Clear();
        root.GetComponentsInChildren(true, _canvases);
        foreach (var canvas in _canvases)
        {
            if (canvas == null) continue;
            if (!_states.ContainsKey(canvas)) _states.Add(canvas, canvas.enabled);
            canvas.enabled = false;
        }
        _canvases.Clear();
    }

    internal void Restore()
    {
        foreach (var pair in _states) if (pair.Key != null) pair.Key.enabled = pair.Value;
        _states.Clear(); _canvases.Clear(); _released.Clear();
    }
}
