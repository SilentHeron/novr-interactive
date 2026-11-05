using System;
using System.Collections.Generic;
using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.Rendering;
using NumericsVector = System.Numerics.Vector3;
using Object = UnityEngine.Object;

namespace NOVR.Comfort;

// A private render mesh. Split intersected triangles and interpolate their
// attributes; never remove an entire triangle merely because it touches the head.
// The original materials, collider meshes and shared asset remain unchanged.
internal sealed class SeatClearanceMesh : IDisposable
{
    private const int MaximumOutputVertices = 120000;
    private readonly MeshFilter _filter;
    private readonly MeshRenderer _renderer;
    private readonly Mesh _original, _cut;
    private readonly List<Material> _materials = new();
    private readonly Vector3[] _positions, _normals;
    private readonly Vector4[] _tangents;
    private readonly Color[] _colors;
    private readonly List<Vector4>[] _sourceUv = new List<Vector4>[8], _uv = new List<Vector4>[8];
    private readonly int[][] _triangles;
    private readonly TriangleBounds[][] _triangleBounds;
    private readonly List<int>[] _indices;
    private readonly List<Vector3> _vertices = new(), _outputNormals = new();
    private readonly List<Vector4> _outputTangents = new();
    private readonly List<Color> _outputColors = new();
    private readonly List<NumericsVector> _polygon = new(64);
    private readonly SeatClipGeometry _clipper = new();
    private readonly NumericsVector[] _metricVertices;
    private Vector3 _basisX, _basisY, _basisZ;
    private NumericsVector _metricMin, _metricMax;
    private NumericsVector _lastHead;
    private float _lastRadius;
    private bool _prepared, _hasCut, _disposed;
    private readonly struct TriangleBounds
    {
        internal readonly NumericsVector Min, Max;
        internal TriangleBounds(NumericsVector a, NumericsVector b, NumericsVector c)
        {
            Min = NumericsVector.Min(a, NumericsVector.Min(b, c));
            Max = NumericsVector.Max(a, NumericsVector.Max(b, c));
        }
        internal bool Outside(NumericsVector head, float radius) =>
            Min.X > head.X + radius || Max.X < head.X - radius ||
            Min.Y > head.Y + radius || Max.Y < head.Y - radius ||
            Min.Z > head.Z + radius || Max.Z < head.Z - radius;
    }

    internal SeatClearanceMesh(MeshFilter filter, MeshRenderer renderer)
    {
        _filter = filter;
        _renderer = renderer;
        _original = filter.sharedMesh;
        if (!MaterialsEligible())
            throw new InvalidOperationException("Seat contains non-opaque or ambiguous materials.");
        var source = NativeCockpitMesh.ReadableCopy(_original);
        try
        {
            if (source.blendShapeCount != 0 || source.subMeshCount < 1 || source.subMeshCount > 16)
                throw new InvalidOperationException("Animated seat geometry is unsupported.");
            _positions = source.vertices;
            _normals = source.normals;
            _tangents = source.tangents;
            _colors = source.colors;
            _metricVertices = new NumericsVector[_positions.Length];
            foreach (var vertex in _positions)
                if (!Finite(vertex))
                    throw new InvalidOperationException("Seat has non-finite vertices.");
            _triangles = new int[source.subMeshCount][];
            _triangleBounds = new TriangleBounds[source.subMeshCount][];
            _indices = new List<int>[source.subMeshCount];
            var count = 0;
            for (var group = 0; group < source.subMeshCount; group++)
            {
                if (source.GetTopology(group) != MeshTopology.Triangles)
                    throw new InvalidOperationException("Seat topology is unsupported.");
                var indices = _triangles[group] = source.GetTriangles(group);
                _triangleBounds[group] = new TriangleBounds[indices.Length / 3];
                count += indices.Length;
                if (count > 180000 || indices.Length % 3 != 0)
                    throw new InvalidOperationException("Seat geometry exceeds clearance budget.");
                foreach (var index in indices)
                    if (index < 0 || index >= _positions.Length)
                        throw new InvalidOperationException("Invalid seat triangle index.");
                _indices[group] = new List<int>(indices.Length + 256);
            }
            for (var channel = 0; channel < 8; channel++)
            {
                _sourceUv[channel] = new List<Vector4>();
                source.GetUVs(channel, _sourceUv[channel]);
                _uv[channel] = new List<Vector4>(_sourceUv[channel].Count + 256);
            }
            _cut = new Mesh { name = "NOVR local seat clearance", indexFormat = IndexFormat.UInt32 };
            _cut.MarkDynamic();
        }
        finally { Object.Destroy(source); }
    }

    internal bool Available => !_disposed && _filter != null && _renderer != null &&
        _original != null && _renderer.enabled && _renderer.gameObject.activeInHierarchy &&
        (_filter.sharedMesh == _original || _filter.sharedMesh == _cut) && MaterialsEligible();

    private bool MaterialsEligible()
    {
        // A damage skin or another mod may replace materials after discovery.
        // Recheck without cloning/editing a material or allocating per frame.
        _renderer.GetSharedMaterials(_materials);
        return SeatMaterialPolicy.AreEligible(_materials, _original.subMeshCount);
    }

    internal void Prepare(Vector3 headWorld, float radius)
    {
        if (!Available || !Finite(headWorld))
        {
            _hasCut = false;
            _prepared = false;
            Restore();
            return;
        }
        var transform = _filter.transform;
        // Use full transformed basis vectors, including nonuniform scale/shear.
        // Removing rigid rotation keeps the small polygonal boundary stable as
        // the aircraft turns, and metric distances stay in metres.
        var inverseRotation = Quaternion.Inverse(transform.rotation);
        var x = inverseRotation * transform.TransformVector(Vector3.right);
        var y = inverseRotation * transform.TransformVector(Vector3.up);
        var z = inverseRotation * transform.TransformVector(Vector3.forward);
        if (!Finite(x) || !Finite(y) || !Finite(z) || Mathf.Abs(Vector3.Dot(x, Vector3.Cross(y, z))) < 1e-8f)
            throw new InvalidOperationException("Seat transform is invalid.");
        var basisChanged = (x - _basisX).sqrMagnitude + (y - _basisY).sqrMagnitude + (z - _basisZ).sqrMagnitude > 1e-10f;
        if (!_prepared || basisChanged)
        {
            _basisX = x;
            _basisY = y;
            _basisZ = z;
            _metricMin = new NumericsVector(float.PositiveInfinity);
            _metricMax = new NumericsVector(float.NegativeInfinity);
            for (var i = 0; i < _positions.Length; i++)
            {
                _metricVertices[i] = Metric(_positions[i]);
                _metricMin = NumericsVector.Min(_metricMin, _metricVertices[i]);
                _metricMax = NumericsVector.Max(_metricMax, _metricVertices[i]);
            }
            for (var group = 0; group < _triangles.Length; group++)
            {
                var indices = _triangles[group];
                for (var i = 0; i < indices.Length; i += 3)
                    _triangleBounds[group][i / 3] = new TriangleBounds(_metricVertices[indices[i]],
                        _metricVertices[indices[i + 1]], _metricVertices[indices[i + 2]]);
            }
        }
        var head = Metric(transform.InverseTransformPoint(headWorld));
        // A sub-millimetre deadband avoids rebuilding for tracker noise. This
        // cache is reused by both eye/cockpit passes, without delaying fast motion.
        if (_prepared && !basisChanged && radius == _lastRadius &&
            NumericsVector.DistanceSquared(head, _lastHead) < .00000025f)
            return;
        _lastHead = head;
        _lastRadius = radius;
        _prepared = true;
        _hasCut = false;
        if (NumericsVector.DistanceSquared(head, NumericsVector.Min(_metricMax, NumericsVector.Max(_metricMin, head))) >= radius * radius)
            return;
        // The cached centre may lag by at most .5 mm. Keep the cut inside the
        // requested radius even at that movement threshold.
        var clipRadius = radius - .0005f;
        ResetBuffers();
        for (var group = 0; group < _triangles.Length; group++)
        {
            var triangles = _triangles[group];
            var output = _indices[group];
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];
                if (_triangleBounds[group][i / 3].Outside(head, clipRadius) ||
                    !_clipper.ClipTriangle(_metricVertices[a], _metricVertices[b], _metricVertices[c], head, clipRadius, _polygon))
                {
                    output.Add(a);
                    output.Add(b);
                    output.Add(c);
                    continue;
                }
                _hasCut = true;
                foreach (var weights in _polygon)
                {
                    if (_vertices.Count >= MaximumOutputVertices)
                        throw new InvalidOperationException("Seat cutout exceeds geometry budget.");
                    output.Add(_vertices.Count);
                    _vertices.Add(_positions[a] * weights.X + _positions[b] * weights.Y + _positions[c] * weights.Z);
                    if (_normals.Length == _positions.Length)
                        _outputNormals.Add((_normals[a] * weights.X + _normals[b] * weights.Y + _normals[c] * weights.Z).normalized);
                    if (_tangents.Length == _positions.Length)
                        _outputTangents.Add(_tangents[a] * weights.X + _tangents[b] * weights.Y + _tangents[c] * weights.Z);
                    if (_colors.Length == _positions.Length)
                        _outputColors.Add(_colors[a] * weights.X + _colors[b] * weights.Y + _colors[c] * weights.Z);
                    for (var channel = 0; channel < 8; channel++)
                    {
                        var uv = _sourceUv[channel];
                        if (uv.Count == _positions.Length)
                            _uv[channel].Add(uv[a] * weights.X + uv[b] * weights.Y + uv[c] * weights.Z);
                    }
                }
            }
        }
        if (!_hasCut)
            return;
        _cut.Clear();
        _cut.SetVertices(_vertices);
        if (_normals.Length == _positions.Length)
            _cut.SetNormals(_outputNormals);
        if (_tangents.Length == _positions.Length)
            _cut.SetTangents(_outputTangents);
        if (_colors.Length == _positions.Length)
            _cut.SetColors(_outputColors);
        for (var channel = 0; channel < 8; channel++)
            if (_sourceUv[channel].Count == _positions.Length)
                _cut.SetUVs(channel, _uv[channel]);
        _cut.subMeshCount = _triangles.Length;
        for (var group = 0; group < _triangles.Length; group++)
            _cut.SetTriangles(_indices[group], group, false);
        _cut.bounds = _original.bounds;
    }

    private NumericsVector Metric(Vector3 point)
    {
        var value = _basisX * point.x + _basisY * point.y + _basisZ * point.z;
        return new NumericsVector(value.x, value.y, value.z);
    }
    private static bool Finite(Vector3 v) =>
        !(float.IsNaN(v.x) || float.IsInfinity(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.z));
    private void ResetBuffers()
    {
        _vertices.Clear();
        _vertices.AddRange(_positions);
        _outputNormals.Clear();
        _outputNormals.AddRange(_normals);
        _outputTangents.Clear();
        _outputTangents.AddRange(_tangents);
        _outputColors.Clear();
        _outputColors.AddRange(_colors);
        for (var i = 0; i < 8; i++)
        {
            _uv[i].Clear();
            _uv[i].AddRange(_sourceUv[i]);
        }
        foreach (var indices in _indices)
            indices.Clear();
    }
    internal void Apply()
    {
        if (Available && _hasCut)
            _filter.sharedMesh = _cut;
    }
    internal void Restore()
    {
        // Another mod may replace a seat at runtime. Never overwrite its mesh.
        if (_filter != null && _filter.sharedMesh == _cut)
            _filter.sharedMesh = _original;
    }
    public void Dispose()
    {
        if (_disposed)
            return;
        Restore();
        _disposed = true;
        Object.Destroy(_cut);
    }
}
