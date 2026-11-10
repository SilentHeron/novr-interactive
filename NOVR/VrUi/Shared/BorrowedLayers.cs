using System.Collections.Generic;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// A native widget borrowed into a cockpit slot must draw on the slot's layer,
// including objects the game adds to it later, and every object gets its
// original layer back when the widget is returned.
internal sealed class BorrowedLayers
{
    // One pass in this many re-checks every object; the others only look at
    // objects they have not seen before.
    internal const int VerifyEvery = 10;
    private const int PruneSlack = 64;

    private readonly Dictionary<Transform, int> _original = new();
    private readonly List<Transform> _gone = new();
    private int _layer = -1, _passes;

    // nodes: every transform currently under the borrowed widget.
    //
    // On the cockpit map that is every unit icon, and re-checking one costs
    // several engine calls. Nothing in the game changes a UI object's layer
    // after creating it, so objects already moved are skipped; the periodic
    // full pass (and any change of the slot's own layer) still repairs one
    // that was reset.
    internal void Apply(List<Transform> nodes, int layer)
    {
        // Count the pass first: || would skip the increment on a layer change.
        var verify = _passes++ % VerifyEvery == 0 || layer != _layer;
        _layer = layer;
        foreach (var node in nodes)
        {
            if (node == null)
                continue;
            var known = _original.ContainsKey(node);
            if (known && !verify)
                continue;
            var go = node.gameObject;
            var current = go.layer;
            if (!known)
                _original.Add(node, current);
            if (current != layer)
                go.layer = layer;
        }
        // Destroyed objects leave their entries behind. Drop them once there
        // are clearly more entries than live objects, not on every pass.
        if (_original.Count <= nodes.Count + PruneSlack)
            return;
        _gone.Clear();
        foreach (var pair in _original)
            if (pair.Key == null)
                _gone.Add(pair.Key!);
        foreach (var node in _gone)
            _original.Remove(node);
        _gone.Clear();
    }

    internal void Restore()
    {
        foreach (var pair in _original)
            if (pair.Key != null)
                pair.Key.gameObject.layer = pair.Value;
        _original.Clear();
        _gone.Clear();
        _layer = -1;
        _passes = 0;
    }
}
