using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

// FS-3 Ternion 1.0.1 stores its red wheel and stem in cockpit_int. Animate
// those original triangles only; the surrounding UP/DOWN plate stays fixed.
internal sealed class TernionNativeGear : IDisposable
{
    internal const int TriangleCount = 120;
    internal static readonly Vector3 NativePoint = new(-.34225461f, .80047104f, 1.05946076f);
    private static readonly Vector3 Pivot = new(-.342225f, .777765f, 1.12002f);
    private static readonly Bounds Wheel = Region(new(-.348239f, .780489f, 1.039479f), new(-.336270f, .820453f, 1.079443f));
    private static readonly Bounds Stem = Region(new(-.35027f, .76374f, 1.06550f), new(-.33420f, .80724f, 1.12527f));
    private readonly MeshFilter _filter;
    private readonly Mesh _original;
    private Mesh? _body, _piece;
    private GameObject? _object;
    private bool _initialized, _disposed;
    internal Transform Lever => _object!.transform;
    internal Vector3 Center => Lever.TransformPoint(NativePoint - Pivot);
    internal static bool Matches(Mesh mesh) => mesh != null && mesh.name == "cockpit_int" && mesh.vertexCount == 39893 &&
        mesh.subMeshCount == 2 && mesh.GetIndexCount(0) == 65217 && mesh.GetIndexCount(1) == 19146;
    private static Bounds Region(Vector3 min, Vector3 max) => new((min + max) * .5f, max - min);
    internal static bool Contains(Vector3 a, Vector3 b, Vector3 c) =>
        Wheel.Contains(a) && Wheel.Contains(b) && Wheel.Contains(c) || Stem.Contains(a) && Stem.Contains(b) && Stem.Contains(c);

    internal TernionNativeGear(MeshFilter filter)
    {
        _filter = filter; _original = filter.sharedMesh;
        try
        {
            if (!Matches(_original)) throw new InvalidOperationException("Ternion cockpit mesh signature changed.");
            _body = NativeCockpitMesh.ReadableCopy(_original);
            _piece = NativeCockpitMesh.ReadableCopy(_original);
            var vertices = _body.vertices;
            var kept = new List<int>(); var selected = new List<int>();
            if (_body.GetTopology(0) != MeshTopology.Triangles || _body.GetTopology(1) != MeshTopology.Triangles)
                throw new InvalidOperationException("Ternion cockpit topology changed.");
            var triangles = _body.GetTriangles(0);
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var destination = Contains(vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]) ? selected : kept;
                destination.Add(triangles[i]); destination.Add(triangles[i + 1]); destination.Add(triangles[i + 2]);
            }
            if (selected.Count != TriangleCount * 3)
                throw new InvalidOperationException("Ternion original gear wheel/stem selection changed.");
            _body.SetTriangles(kept, 0, false); _body.bounds = _original.bounds;
            _body.name = "NOVR Ternion cockpit with separate original gear lever";
            var bounds = new Bounds(NativePoint - Pivot, Vector3.zero);
            foreach (var index in selected) bounds.Encapsulate(vertices[index] - Pivot);
            for (var i = 0; i < vertices.Length; i++) vertices[i] -= Pivot;
            _piece.vertices = vertices; _piece.SetTriangles(selected, 0, false);
            _piece.SetTriangles(Array.Empty<int>(), 1, false); _piece.bounds = bounds;
            _piece.name = "NOVR original Ternion gear wheel and stem";
            _object = new GameObject("FS-3 original gear wheel and stem", typeof(MeshFilter), typeof(MeshRenderer));
            _object.SetActive(false);
            _object.transform.SetParent(filter.transform, false); _object.transform.localPosition = Pivot;
            _object.layer = filter.gameObject.layer;
            _object.GetComponent<MeshFilter>().sharedMesh = _piece;
            var native = filter.GetComponent<MeshRenderer>(); var renderer = _object.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = native.sharedMaterials;
            renderer.shadowCastingMode = native.shadowCastingMode; renderer.receiveShadows = native.receiveShadows;
            renderer.lightProbeUsage = native.lightProbeUsage; renderer.reflectionProbeUsage = native.reflectionProbeUsage;
            var properties = new MaterialPropertyBlock(); native.GetPropertyBlock(properties); renderer.SetPropertyBlock(properties);
            // Commit only after every signature and selection check succeeds.
            _filter.sharedMesh = _body; _object.SetActive(true);
        }
        catch { Dispose(); throw; }
    }

    internal void Update(bool deployed, float elapsed)
    {
        if (_disposed) return;
        var target = Quaternion.Euler(deployed ? 0 : 25, 0, 0);
        Lever.localRotation = _initialized ? Quaternion.Slerp(Lever.localRotation, target,
            FlightControlMath.SmoothingWeight(elapsed, 18)) : target;
        _initialized = true;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_filter != null && _filter.sharedMesh == _body) _filter.sharedMesh = _original;
        if (_object != null) { _object.SetActive(false); Object.Destroy(_object); }
        if (_piece != null) Object.Destroy(_piece);
        if (_body != null) Object.Destroy(_body);
    }
}
