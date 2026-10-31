using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using N = System.Numerics;

namespace NOVR.MotionControls;

// Owns only render copies. Native meshes, transforms, UVs and materials stay
// untouched; mirroring switches between cached meshes without per-frame work.
internal sealed class MirroredControlGeometry : IDisposable
{
    private readonly List<(MeshFilter filter, Mesh original, Mesh mirrored)> _meshes = new();
    private bool _mirrored;

    internal MirroredControlGeometry(Transform handle, IEnumerable<MeshFilter> filters)
    {
        try
        {
            foreach (var filter in filters)
            {
                if (filter == null || filter.sharedMesh == null) continue;
                var original = filter.sharedMesh;
                var copy = NativeCockpitMesh.ReadableCopy(original);
                _meshes.Add((filter, original, copy));
                copy.name = "NOVR mirrored " + original.name;
                var matrix = handle.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                // Numerics uses row vectors; Unity uses column vectors.
                var math = new ControlMirrorMath(new N.Matrix4x4(
                    matrix.m00, matrix.m10, matrix.m20, matrix.m30,
                    matrix.m01, matrix.m11, matrix.m21, matrix.m31,
                    matrix.m02, matrix.m12, matrix.m22, matrix.m32,
                    matrix.m03, matrix.m13, matrix.m23, matrix.m33));
                var vertices = copy.vertices; var normals = copy.normals; var tangents = copy.tangents;
                for (var i = 0; i < vertices.Length; i++) vertices[i] = Unity(math.Position(Numerics(vertices[i])));
                for (var i = 0; i < normals.Length; i++) normals[i] = Unity(math.Normal(Numerics(normals[i])));
                for (var i = 0; i < tangents.Length; i++)
                {
                    var source = tangents[i]; var result = math.Tangent(new N.Vector4(source.x, source.y, source.z, source.w));
                    tangents[i] = new Vector4(result.X, result.Y, result.Z, result.W);
                }
                copy.vertices = vertices;
                if (normals.Length > 0) copy.normals = normals;
                if (tangents.Length > 0) copy.tangents = tangents;
                for (var submesh = 0; submesh < copy.subMeshCount; submesh++)
                {
                    if (copy.GetTopology(submesh) != MeshTopology.Triangles)
                        throw new InvalidOperationException("Control geometry is not triangulated.");
                    var indices = copy.GetTriangles(submesh);
                    ControlMirrorMath.ReverseTriangles(indices);
                    copy.SetTriangles(indices, submesh, false);
                }
                if (normals.Length == 0) copy.RecalculateNormals();
                copy.RecalculateBounds();
            }
        }
        catch { Dispose(); throw; }
    }

    internal void SetMirrored(bool mirrored)
    {
        if (_mirrored == mirrored) return;
        _mirrored = mirrored;
        foreach (var item in _meshes)
            if (item.filter != null) item.filter.sharedMesh = mirrored ? item.mirrored : item.original;
    }

    private static N.Vector3 Numerics(Vector3 value) => new(value.x, value.y, value.z);
    private static Vector3 Unity(N.Vector3 value) => new(value.X, value.Y, value.Z);

    public void Dispose()
    {
        foreach (var item in _meshes)
        {
            if (item.filter != null && item.filter.sharedMesh == item.mirrored) item.filter.sharedMesh = item.original;
            Object.Destroy(item.mirrored);
        }
        _meshes.Clear(); _mirrored = false;
    }
}
