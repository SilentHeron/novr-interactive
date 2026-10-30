using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NOVR.MotionControls;

// Cache only a fitted plane; native geometry stays in the game and is not cloned
// or exported. Some aircraft bake several angled displays into one screen mesh.
internal sealed class DashboardSurface
{
    internal readonly Renderer Renderer;
    private Vector3 _center;
    private readonly Vector3 _normal, _up;
    private float _width, _height;
    private bool _fascia;
    internal float Aspect => Mathf.Clamp(_width / (.77f * Mathf.Clamp(_height / .36f, .55f, 1.35f)), .85f, 1.75f);
    private float _clearance = .012f, _fitMultiplier = 1f, _aircraftScale = 1f;
    internal bool ClearanceLimited { get; private set; }
    private DashboardSurface(Renderer renderer, Vector3 center, Vector3 normal, Vector3 up, float width, float height)
    { Renderer = renderer; _center = center; _normal = normal; _up = up; _width = width; _height = height; }

    internal static DashboardSurface? Find(Renderer renderer, Transform eye)
    {
        var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null || mesh.vertexCount < 3 || mesh.vertexCount > 65536) return null;
        try
        {
            var positions = Read(mesh, VertexAttribute.Position); var normals = Read(mesh, VertexAttribute.Normal);
            if (positions == null || normals == null) return null;
            var groups = new List<(Vector3 normal, List<Vector3> points)>();
            for (var i = 0; i < positions.Length; i++)
            {
                if (!TrackedControllers.Finite(positions[i]) || !TrackedControllers.Finite(normals[i]) || normals[i].sqrMagnitude < .5f) continue;
                var normal = normals[i].normalized; var group = -1;
                for (var g = 0; g < groups.Count; g++) if (Vector3.Dot(groups[g].normal, normal) > .985f) { group = g; break; }
                if (group < 0)
                {
                    if (groups.Count >= 64) continue;
                    groups.Add((normal, new List<Vector3>())); group = groups.Count - 1;
                }
                groups[group].points.Add(positions[i]);
            }
            DashboardSurface? best = null; var bestArea = 0f;
            foreach (var group in groups)
            {
                if (group.points.Count < 3) continue;
                var normal = group.normal;
                var worldNormal = renderer.transform.worldToLocalMatrix.transpose.MultiplyVector(normal).normalized;
                var mean = Vector3.zero; foreach (var p in group.points) mean += p; mean /= group.points.Count;
                var world = renderer.transform.TransformPoint(mean); var relative = Quaternion.Inverse(eye.rotation) * (world - eye.position);
                if (relative.z < .12f || relative.z > 1.8f || Mathf.Abs(relative.x) > 1.4f || relative.y > .45f || relative.y < -.75f) continue;
                if (Vector3.Dot(worldNormal, world - eye.position) < 0) { normal = -normal; worldNormal = -worldNormal; }
                if (Vector3.Dot(worldNormal, eye.forward) < .6f) continue;
                var up = Vector3.ProjectOnPlane(renderer.transform.InverseTransformDirection(eye.up), normal).normalized;
                if (up.sqrMagnitude < .5f) continue;
                var right = Vector3.Cross(up, normal).normalized;
                var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue); var max = -min;
                foreach (var p in group.points)
                {
                    var q = new Vector3(Vector3.Dot(p, right), Vector3.Dot(p, up), Vector3.Dot(p, normal));
                    min = Vector3.Min(min, q); max = Vector3.Max(max, q);
                }
                var width = (max.x - min.x) * renderer.transform.TransformVector(right).magnitude;
                var height = (max.y - min.y) * renderer.transform.TransformVector(up).magnitude;
                var thickness = (max.z - min.z) * renderer.transform.TransformVector(normal).magnitude;
                if (width < .08f || height < .08f || width > 2.5f || height > 1.5f || thickness > .035f) continue;
                var area = width * height;
                if (area <= bestArea) continue;
                // Use the nearest plane in the cluster to avoid intersecting a
                // slightly raised display on the same dashboard.
                // Wide shared meshes can cover both pilots. Select the part in
                // front of this seat instead of the middle of the entire cockpit.
                var eyeLocal = renderer.transform.InverseTransformPoint(eye.position);
                var desiredX = Vector3.Dot(eyeLocal, right);
                var halfWidth = Mathf.Min((max.x - min.x) * .5f, .385f * Mathf.Clamp(Mathf.Min(width / .77f, height / .36f), .55f, 1.2f) / renderer.transform.TransformVector(right).magnitude);
                var x = Mathf.Clamp(desiredX, min.x + halfWidth, max.x - halfWidth);
                var center = right * x + up * ((min.y + max.y) * .5f) + normal * min.z;
                best = new DashboardSurface(renderer, center, normal, up, width, height); bestArea = area;
            }
            return best;
        }
        catch (Exception e) { Debug.LogWarning($"[NOVR] Dashboard fit unavailable for {renderer.name}: {e.Message}"); return null; }
    }

    internal void Pose(out Vector3 center, out Quaternion rotation, out float scale)
    {
        var forward = Renderer.transform.worldToLocalMatrix.transpose.MultiplyVector(_normal).normalized;
        var up = Vector3.ProjectOnPlane(Renderer.transform.TransformVector(_up), forward).normalized;
        rotation = Quaternion.LookRotation(forward, up);
        // Fit the same model to both dashboard dimensions. A minimum readable
        // size may extend over a small native screen; manual placement remains.
        scale = Mathf.Clamp(_height / .36f, .55f, 1.35f) * ModConfiguration.Instance.CockpitPanelScale.Value * _fitMultiplier * _aircraftScale;
        center = Renderer.transform.TransformPoint(_center) - forward * (.0395f * scale + _clearance);
    }

    // Extend the screen seed to nearby, coplanar dashboard skin. Bounds are
    // deliberately limited: no canopy, side consoles, or another pilot's half.
    private void ExpandToFascia(Aircraft aircraft)
    {
        var source = Renderer.transform;
        var center = source.TransformPoint(_center);
        var normal = source.worldToLocalMatrix.transpose.MultiplyVector(_normal).normalized;
        var up = Vector3.ProjectOnPlane(source.TransformVector(_up), normal).normalized;
        var right = Vector3.Cross(up, normal).normalized;
        var min = new Vector2(-_width * .5f, -_height * .5f); var max = -min;
        var limit = new Vector2(Mathf.Min(.65f, _width * .5f + .20f), Mathf.Min(.30f, _height * .5f + .065f));
        var budget = 350000; var visited = new HashSet<Renderer>();
        foreach (var root in new[] { aircraft.transform, aircraft.cockpit.transform })
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer == Renderer || !visited.Add(renderer) || !renderer.enabled || renderer.gameObject.layer != (int)LayerHelper.Layers.Cockpit) continue;
            var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null || mesh.vertexCount > budget || Vector3.Distance(renderer.bounds.ClosestPoint(center), center) > .8f) continue;
            budget -= mesh.vertexCount;
            try
            {
                var points = Read(mesh, VertexAttribute.Position); var normals = Read(mesh, VertexAttribute.Normal);
                if (points == null || normals == null) continue;
                for (var i = 0; i < points.Length; i++)
                {
                    var p = renderer.transform.TransformPoint(points[i]) - center;
                    if (Mathf.Abs(Vector3.Dot(p, normal)) > .028f) continue;
                    var n = renderer.transform.worldToLocalMatrix.transpose.MultiplyVector(normals[i]).normalized;
                    if (Mathf.Abs(Vector3.Dot(n, normal)) < .94f) continue;
                    var q = new Vector2(Vector3.Dot(p, right), Vector3.Dot(p, up));
                    if (Mathf.Abs(q.x) > limit.x || Mathf.Abs(q.y) > limit.y) continue;
                    min = Vector2.Min(min, q); max = Vector2.Max(max, q);
                }
            }
            catch (Exception e) { Debug.LogWarning("[NOVR] Dashboard skin unavailable: " + renderer.name + ": " + e.Message); }
        }
        // Add a small seam overlap so luminous native screen edges cannot leak.
        _fascia = max.x - min.x > _width + .025f;
        _width = Mathf.Min(1.30f, max.x - min.x + .012f);
        _height = Mathf.Min(.60f, max.y - min.y + .008f);
        _center = source.InverseTransformPoint(center + right * ((min.x + max.x) * .5f) + up * ((min.y + max.y) * .5f));
        Debug.Log($"[NOVR] Dashboard face fit {_width:0.000} x {_height:0.000} m; aspect {Aspect:0.00}; fascia={_fascia}.");
    }

    internal void FitClearance(Aircraft aircraft)
    {
        ExpandToFascia(aircraft);
        _aircraftScale = !_fascia && CockpitPanelPlacement.IsVt7(aircraft) ? 1.25f : 1f;
        using var geometry = new DashboardClearance(aircraft);
        foreach (var multiplier in new[] { 1f, .9f })
        {
            _fitMultiplier = multiplier;
            for (var step = 0; step <= 6; step++)
            {
                _clearance = .012f + .02f * step;
                Pose(out var center, out var rotation, out var scale);
                if (!geometry.Intersects(center, rotation, scale, Aspect)) return;
            }
        }
        ClearanceLimited = true;
        Debug.LogWarning("[NOVR] Dashboard has tight surrounding geometry on " + aircraft.name + "; use View > Edit layout to refine panel placement.");
    }

    internal static Vector3[]? Read(Mesh mesh, VertexAttribute attribute)
    {
        if (!mesh.HasVertexAttribute(attribute) || (mesh.GetVertexAttributeFormat(attribute) != VertexAttributeFormat.Float32 && mesh.GetVertexAttributeFormat(attribute) != VertexAttributeFormat.Float16) ||
            mesh.GetVertexAttributeDimension(attribute) < 3) return null;
        if (mesh.isReadable) return attribute == VertexAttribute.Position ? mesh.vertices : mesh.normals;
        // Imported cockpit meshes discard their CPU copy. A bounded read-only
        // GPU readback is done once on cockpit entry, never in the render loop.
        var stream = mesh.GetVertexAttributeStream(attribute); var stride = mesh.GetVertexBufferStride(stream);
        var offset = mesh.GetVertexAttributeOffset(attribute);
        var half = mesh.GetVertexAttributeFormat(attribute) == VertexAttributeFormat.Float16; var size = half ? 2 : 4;
        using var buffer = mesh.GetVertexBuffer(stream);
        if (buffer == null || (long)buffer.count * buffer.stride > 8 * 1024 * 1024) return null;
        var data = new byte[buffer.count * buffer.stride]; buffer.GetData(data);
        if ((long)(mesh.vertexCount - 1) * stride + offset + size * 3 > data.Length) return null;
        var values = new Vector3[mesh.vertexCount];
        for (var i = 0; i < values.Length; i++)
        {
            var p = i * stride + offset;
            values[i] = half ? new Vector3(Mathf.HalfToFloat(BitConverter.ToUInt16(data, p)), Mathf.HalfToFloat(BitConverter.ToUInt16(data, p+2)), Mathf.HalfToFloat(BitConverter.ToUInt16(data, p+4)))
                : new Vector3(BitConverter.ToSingle(data, p), BitConverter.ToSingle(data, p + 4), BitConverter.ToSingle(data, p + 8));
        }
        return values;
    }
}
