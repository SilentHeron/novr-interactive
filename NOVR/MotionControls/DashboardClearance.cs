using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace NOVR.MotionControls;

// Bounded, one-time cockpit-entry check. Only the native interior layer matters:
// simplified exterior/LOD shells overlap the instruments but are not drawn here.
internal sealed class DashboardClearance : IDisposable
{
    private readonly List<(Vector3[] points, int[] indices)> _meshes = new();
    internal DashboardClearance(Aircraft aircraft)
    {
        var moving = new HashSet<Renderer>();
        foreach (var cockpit in aircraft.cockpit.GetComponentsInChildren<Cockpit>(true))
        foreach (var key in new[] { "joysticks", "throttles" })
        {
            if (AccessTools.Field(typeof(Cockpit), key)?.GetValue(cockpit) is not System.Collections.IEnumerable handles) continue;
            foreach (var handle in handles)
                if (AccessTools.Field(handle.GetType(), "transform")?.GetValue(handle) is Transform t)
                    foreach (var r in t.GetComponentsInChildren<Renderer>(true)) moving.Add(r);
        }
        var seen = new HashSet<Renderer>(); var budget = 350000;
        foreach (var root in new[] { aircraft.transform, aircraft.cockpit.transform })
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!seen.Add(renderer) || !renderer.enabled || moving.Contains(renderer) ||
                renderer.gameObject.layer != (int)LayerHelper.Layers.Cockpit) continue;
            var active = true;
            for (var t = renderer.transform; t != null && t != root && t != aircraft.transform; t = t.parent)
                if (!t.gameObject.activeSelf) { active = false; break; }
            if (!active || Vector3.Distance(renderer.bounds.ClosestPoint(aircraft.cockpitViewPoint.position), aircraft.cockpitViewPoint.position) > 1.8f) continue;
            var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null || mesh.vertexCount > budget) continue;
            budget -= mesh.vertexCount;
            try
            {
                var points = DashboardSurface.Read(mesh, VertexAttribute.Position);
                if (points == null) continue;
                var indices = new List<int>();
                if (mesh.isReadable) indices.AddRange(mesh.triangles);
                else
                {
                    using var buffer = mesh.GetIndexBuffer();
                    if (buffer == null || (long)buffer.count * buffer.stride > 8 * 1024 * 1024) continue;
                    var bytes = new byte[buffer.count * buffer.stride]; buffer.GetData(bytes);
                    var size = mesh.indexFormat == IndexFormat.UInt16 ? 2 : 4;
                    for (var s = 0; s < mesh.subMeshCount; s++)
                    {
                        var sub = mesh.GetSubMesh(s); if (sub.topology != MeshTopology.Triangles) continue;
                        for (var i = 0; i < sub.indexCount; i++)
                        {
                            var at = (sub.indexStart + i) * size;
                            indices.Add((size == 2 ? BitConverter.ToUInt16(bytes, at) : (int)BitConverter.ToUInt32(bytes, at)) + sub.baseVertex);
                        }
                    }
                }
                for (var i = 0; i < points.Length; i++) points[i] = renderer.transform.TransformPoint(points[i]);
                _meshes.Add((points, indices.ToArray()));
            }
            catch (Exception e) { Debug.LogWarning("[NOVR] Dashboard clearance mesh unavailable: " + renderer.name + ": " + e.Message); }
        }
    }
    internal bool Intersects(Vector3 center, Quaternion rotation, float scale, float aspect = 1)
    {
        var inverse = Quaternion.Inverse(rotation);
        // Full faceplate/chassis plus a small safety margin. Native moving grips
        // are excluded above because NOVR replaces them with repositionable ones.
        var half = new Vector3(.39f * aspect, .185f, .048f) * scale;
        foreach (var mesh in _meshes)
        {
            var points = Array.ConvertAll(mesh.points, p => inverse * (p - center));
            for (var i = 0; i + 2 < mesh.indices.Length; i += 3)
            {
                var x = mesh.indices[i]; var y = mesh.indices[i + 1]; var z = mesh.indices[i + 2];
                if (x < 0 || y < 0 || z < 0 || x >= points.Length || y >= points.Length || z >= points.Length) continue;
                if (Triangle(points[x], points[y], points[z], half)) return true;
            }
        }
        return false;
    }
    private static bool Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 half)
    {
        if (Separate(Vector3.right, a, b, c, half) || Separate(Vector3.up, a, b, c, half) || Separate(Vector3.forward, a, b, c, half)) return false;
        var ab = b - a; var bc = c - b; var ca = a - c;
        if (Separate(Vector3.Cross(ab, bc), a, b, c, half)) return false;
        return !Edge(ab, a, b, c, half) && !Edge(bc, a, b, c, half) && !Edge(ca, a, b, c, half);
    }
    private static bool Edge(Vector3 edge, Vector3 a, Vector3 b, Vector3 c, Vector3 half) =>
        Separate(Vector3.Cross(edge, Vector3.right), a, b, c, half) || Separate(Vector3.Cross(edge, Vector3.up), a, b, c, half) || Separate(Vector3.Cross(edge, Vector3.forward), a, b, c, half);
    private static bool Separate(Vector3 axis, Vector3 a, Vector3 b, Vector3 c, Vector3 half)
    {
        var x = Vector3.Dot(axis, a); var y = Vector3.Dot(axis, b); var z = Vector3.Dot(axis, c);
        var r = Vector3.Dot(new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)), half);
        return Mathf.Min(x, Mathf.Min(y, z)) > r || Mathf.Max(x, Mathf.Max(y, z)) < -r;
    }
    public void Dispose() => _meshes.Clear();
}
