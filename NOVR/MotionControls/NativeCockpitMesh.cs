using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

// Detach the existing small switch islands from this aircraft's combined mesh.
// Work on an instance only, retaining original UVs, materials and normals. The
// original mesh is restored on exit; no native asset or other aircraft changes.
internal sealed class NativeCockpitMesh : IDisposable
{
    private readonly MeshFilter _filter;
    private readonly Mesh _original, _body;
    private readonly List<Mesh> _pieces = new();
    private readonly List<GameObject> _objects = new();
    internal readonly Dictionary<string, Transform> Parts = new();
    private bool _visible;

    internal NativeCockpitMesh(MeshFilter filter, Transform parent)
    {
        _filter = filter; _original = filter.sharedMesh; _body = ReadableCopy(_original);
        try
        {
            var vertices = _body.vertices; var normals = _body.normals; var uv = _body.uv; var tangents = _body.tangents;
            var colors = _body.colors32; var uv2 = _body.uv2;
            var sourceRenderer = filter.GetComponent<MeshRenderer>();
            var remaining = new List<int>[_body.subMeshCount];
            var slices = new Dictionary<string, List<int>[]>();
            foreach (var control in Fs20ControlMap.Controls)
            {
                var groups = new List<int>[_body.subMeshCount];
                for (var s = 0; s < groups.Length; s++) groups[s] = new List<int>();
                slices.Add(control.Id, groups);
            }
            for (var s = 0; s < _body.subMeshCount; s++)
            {
                if (_body.GetTopology(s) != MeshTopology.Triangles) throw new InvalidOperationException("Unexpected cockpit topology.");
                remaining[s] = new List<int>(); var indices = _body.GetTriangles(s);
                for (var i = 0; i < indices.Length; i += 3)
                {
                    var target = remaining[s];
                    foreach (var control in Fs20ControlMap.Controls)
                        if (control.Contains(vertices[indices[i]],vertices[indices[i+1]],vertices[indices[i+2]]))
                        { target = slices[control.Id][s]; break; }
                    target.Add(indices[i]); target.Add(indices[i+1]); target.Add(indices[i+2]);
                }
            }
            foreach (var control in Fs20ControlMap.Controls)
            {
                var groups = slices[control.Id]; var count = 0;
                foreach (var group in groups) count += group.Count;
                // Refuse a mismatched game revision rather than cutting holes in the dash.
                if (count < 12 || count > 6000) throw new InvalidOperationException("Control mesh does not match: " + control.Id);
                var piece = new Mesh { name = "NOVR native " + control.Id }; _pieces.Add(piece);
                var pivot = control.Region.center;
                if (control.Animation == Fs20ControlMap.Motion.Toggle)
                {
                    // Hinge at the rear end of the actual lever, not its visual
                    // midpoint. The surrounding nut/faceplate stays in the body.
                    var normal = new Vector3(0,.205f,-.979f); var back = float.MaxValue;
                    foreach (var group in groups) foreach (var i in group) back = Mathf.Min(back,Vector3.Dot(vertices[i],normal));
                    var sum = Vector3.zero; var n = 0;
                    foreach (var group in groups) foreach (var i in group)
                        if (Vector3.Dot(vertices[i],normal) < back + .001f) { sum += vertices[i]; n++; }
                    if (n > 0) pivot = sum / n;
                }
                var map = new Dictionary<int,int>(); var pv = new List<Vector3>(); var pn = new List<Vector3>();
                var pu = new List<Vector2>(); var pt = new List<Vector4>();
                var pc = new List<Color32>(); var pu2 = new List<Vector2>();
                foreach (var group in groups)
                    foreach (var old in group)
                        if (!map.ContainsKey(old))
                        {
                            map.Add(old, pv.Count); pv.Add(vertices[old] - pivot);
                            if (normals.Length == vertices.Length) pn.Add(normals[old]);
                            if (uv.Length == vertices.Length) pu.Add(uv[old]);
                            if (tangents.Length == vertices.Length) pt.Add(tangents[old]);
                            if (colors.Length == vertices.Length) pc.Add(colors[old]);
                            if (uv2.Length == vertices.Length) pu2.Add(uv2[old]);
                        }
                piece.SetVertices(pv); if (pn.Count > 0) piece.SetNormals(pn); if (pu.Count > 0) piece.SetUVs(0,pu); if (pt.Count > 0) piece.SetTangents(pt);
                if (pc.Count > 0) piece.SetColors(pc); if (pu2.Count > 0) piece.SetUVs(1,pu2);
                piece.subMeshCount = groups.Length;
                for (var s = 0; s < groups.Length; s++)
                {
                    var indices = groups[s].ToArray(); for (var i = 0; i < indices.Length; i++) indices[i] = map[indices[i]];
                    piece.SetTriangles(indices,s,false);
                }
                piece.RecalculateBounds();
                var go = new GameObject("Native " + control.Label, typeof(MeshFilter), typeof(MeshRenderer)); _objects.Add(go);
                go.transform.SetParent(parent,false); go.transform.localPosition = pivot; go.layer = (int)LayerHelper.Layers.Cockpit;
                go.GetComponent<MeshFilter>().sharedMesh = piece;
                var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = sourceRenderer.receiveShadows;
                renderer.lightProbeUsage = sourceRenderer.lightProbeUsage; renderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
                var block = new MaterialPropertyBlock(); sourceRenderer.GetPropertyBlock(block); renderer.SetPropertyBlock(block);
                Parts.Add(control.Id,go.transform); go.SetActive(false);
            }
            for (var s = 0; s < remaining.Length; s++) _body.SetTriangles(remaining[s],s,false);
            _body.bounds = _original.bounds;
        }
        catch { Dispose(); throw; }
    }

    internal static Mesh ReadableCopy(Mesh original)
    {
        if (original.isReadable) return Object.Instantiate(original);
        var copy = new Mesh { name = original.name + " (interactive instance)", indexFormat = original.indexFormat };
        try
        {
            copy.SetVertexBufferParams(original.vertexCount,original.GetVertexAttributes());
            for (var stream = 0; stream < original.vertexBufferCount; stream++)
            {
                using var buffer = original.GetVertexBuffer(stream);
                if (buffer == null || (long)buffer.count * buffer.stride > 16 * 1024 * 1024) throw new InvalidOperationException("Cockpit vertex buffer unavailable.");
                var bytes = new byte[buffer.count * buffer.stride]; buffer.GetData(bytes);
                copy.SetVertexBufferData(bytes,0,0,bytes.Length,stream,MeshUpdateFlags.DontRecalculateBounds);
            }
            using var indices = original.GetIndexBuffer();
            if (indices == null || (long)indices.count * indices.stride > 16 * 1024 * 1024) throw new InvalidOperationException("Cockpit index buffer unavailable.");
            var data = new byte[indices.count * indices.stride]; indices.GetData(data);
            copy.SetIndexBufferParams(data.Length / (original.indexFormat == IndexFormat.UInt16 ? 2 : 4),original.indexFormat);
            copy.SetIndexBufferData(data,0,0,data.Length,MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            copy.subMeshCount = original.subMeshCount;
            for (var s = 0; s < original.subMeshCount; s++) copy.SetSubMesh(s,original.GetSubMesh(s),MeshUpdateFlags.DontRecalculateBounds);
            copy.bounds = original.bounds; return copy;
        }
        catch { Object.Destroy(copy); throw; }
    }
    internal void SetVisible(bool visible)
    {
        if (_visible == visible) return;
        _visible = visible;
        if (_filter != null && (_filter.sharedMesh == _original || _filter.sharedMesh == _body)) _filter.sharedMesh = visible ? _body : _original;
        foreach (var go in _objects) if (go != null) go.SetActive(visible);
    }
    public void Dispose()
    {
        SetVisible(false);
        foreach (var go in _objects) if (go != null) Object.Destroy(go);
        foreach (var mesh in _pieces) Object.Destroy(mesh);
        if (_body != null) Object.Destroy(_body);
        Parts.Clear(); _objects.Clear(); _pieces.Clear();
    }
}
