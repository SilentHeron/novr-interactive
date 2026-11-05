using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

// Native auxiliary roots are HUD positioning anchors, not diagram bounds. The
// Ibis fan readout, for example, is authored hundreds of pixels to the right of
// its empty root. Fit only drawn content, reserving a stable sweep for the two
// transforms animated by the native gauge so its size never pulses with angle.
internal sealed class AuxiliaryGaugeBounds
{
    private readonly RectTransform _root;
    private readonly Transform? _inputPivot, _diagramPivot;
    private readonly HashSet<Graphic> _fadeGraphics = new();
    private readonly Graphic? _label;
    private readonly Vector3[] _corners = new Vector3[4];

    internal AuxiliaryGaugeBounds(HUDApp app, RectTransform root)
    {
        _root = root;
        var type = app.GetType();
        _inputPivot = AccessTools.Field(type, "inputPivot")?.GetValue(app) as Transform;
        _diagramPivot = AccessTools.Field(type, app is WingAngleGauge ? "wingIcon" : "nozzleIcon")?.GetValue(app) as Transform;
        _label = AccessTools.Field(type, "label")?.GetValue(app) as Graphic;
        if (AccessTools.Field(type, "fadeImages")?.GetValue(app) is Image[] images)
            foreach (var image in images)
                if (image != null)
                    _fadeGraphics.Add(image);
    }

    internal Bounds Measure(List<Graphic> graphics)
    {
        var bounds = new Bounds();
        var started = false;
        foreach (var graphic in graphics)
        {
            if (graphic == null || !ActiveBranch(graphic.transform) ||
                (!graphic.enabled || graphic.color.a <= .001f) && graphic != _label && !_fadeGraphics.Contains(graphic))
                continue;
            graphic.rectTransform.GetWorldCorners(_corners);
            var pivot = MovingPivot(graphic.transform);
            if (pivot == null)
            {
                foreach (var corner in _corners)
                    Include(ref bounds, ref started, _root.InverseTransformPoint(corner));
                continue;
            }
            var radius = 0f;
            foreach (var corner in _corners)
            {
                var local = pivot.InverseTransformPoint(corner);
                radius = Mathf.Max(radius, Mathf.Sqrt(local.x * local.x + local.y * local.y));
            }
            // Native pivots rotate around local Z. Applying the largest XY
            // scale at each level also keeps nonuniform modded diagrams safe,
            // without using their current rotation or mutating the animation.
            for (var node = pivot; node != null && node != _root; node = node.parent)
                radius *= Mathf.Max(Mathf.Abs(node.localScale.x), Mathf.Abs(node.localScale.y));
            var center = _root.InverseTransformPoint(pivot.TransformPoint(Vector3.zero));
            var extent = new Vector3(radius, radius, 0);
            Include(ref bounds, ref started, center - extent);
            Include(ref bounds, ref started, center + extent);
        }
        // A wholly dormant gauge stays unscaled until its actual content is
        // available. Never fall back to its potentially distant empty anchor.
        return bounds;
    }

    private bool ActiveBranch(Transform graphic)
    {
        for (var node = graphic; node != null && node != _root; node = node.parent)
            if (!node.gameObject.activeSelf)
                return false;
        return true;
    }

    private Transform? MovingPivot(Transform graphic)
    {
        if (_inputPivot != null && _inputPivot != _root && _inputPivot.IsChildOf(_root) &&
            (graphic == _inputPivot || graphic.IsChildOf(_inputPivot)))
            return _inputPivot;
        if (_diagramPivot != null && _diagramPivot != _root && _diagramPivot.IsChildOf(_root) &&
            (graphic == _diagramPivot || graphic.IsChildOf(_diagramPivot)))
            return _diagramPivot;
        return null;
    }

    private static void Include(ref Bounds bounds, ref bool started, Vector3 point)
    {
        if (!started)
        {
            bounds = new Bounds(point, Vector3.zero);
            started = true;
        }
        else
            bounds.Encapsulate(point);
    }
}
