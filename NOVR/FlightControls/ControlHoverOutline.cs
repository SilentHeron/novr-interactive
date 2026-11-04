using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using N = System.Numerics;

namespace NOVR.MotionControls;

// A cached inverted hull: the original handle supplies the depth and retains
// its native appearance. Only the expanded back faces form the white border.
internal sealed class ControlHoverOutline : IDisposable
{
    private readonly List<Mesh> _meshes = new();
    private readonly List<GameObject> _objects = new();
    private readonly List<(Renderer source, MeshRenderer outline)> _renderers = new();
    private Material? _material;

    internal static ControlHoverOutline? TryCreate(Transform handle, IEnumerable<Renderer> sources)
    {
        var result = new ControlHoverOutline();
        try
        {
            result._material = CockpitMaterials.Opaque("NOVR white handle outline", Color.white);
            if (!result._material.shader.name.Contains("Unlit") || !result._material.HasProperty("_Cull") ||
                !result._material.HasProperty("_ZWrite"))
                throw new InvalidOperationException("No compatible unlit outline shader is available.");
            result._material.SetFloat("_Cull", (float)CullMode.Front);
            result._material.SetFloat("_ZWrite", 0);
            result._material.SetShaderPassEnabled("DepthOnly", false);
            result._material.SetShaderPassEnabled("DepthNormalsOnly", false);
            result._material.SetShaderPassEnabled("ShadowCaster", false);
            // Draw after opaque depth is complete, before the faded shaft.
            // A transparent queue also avoids URP's opaque depth-priming Equal override.
            result._material.renderQueue = (int)RenderQueue.Transparent - 10;
            foreach (var source in sources)
                result.Add(handle, source);
            return result;
        }
        catch (Exception exception)
        {
            result.Dispose();
            Debug.LogWarning("[NOVR] Handle outline unavailable; original appearance retained: " + exception.Message);
            return null;
        }
    }

    private void Add(Transform handle, Renderer source)
    {
        var original = source.GetComponent<MeshFilter>()?.sharedMesh;
        if (original == null)
            return;
        var mesh = NativeCockpitMesh.ReadableCopy(original);
        _meshes.Add(mesh);
        mesh.name = "NOVR outline " + original.name;
        var matrix = handle.worldToLocalMatrix * source.transform.localToWorldMatrix;
        var localToHandle = new N.Matrix4x4(
            matrix.m00, matrix.m10, matrix.m20, matrix.m30,
            matrix.m01, matrix.m11, matrix.m21, matrix.m31,
            matrix.m02, matrix.m12, matrix.m22, matrix.m32,
            matrix.m03, matrix.m13, matrix.m23, matrix.m33);
        if (mesh.normals.Length == 0)
            mesh.RecalculateNormals();
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var positions = new N.Vector3[vertices.Length];
        var directions = new N.Vector3[normals.Length];
        for (var i = 0; i < vertices.Length; i++)
            positions[i] = new N.Vector3(vertices[i].x, vertices[i].y, vertices[i].z);
        for (var i = 0; i < normals.Length; i++)
            directions[i] = new N.Vector3(normals[i].x, normals[i].y, normals[i].z);
        var expanded = ControlOutlineGeometry.Expand(positions, directions, localToHandle, .002f);
        var colors = new Color32[vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] = new Vector3(expanded[i].X, expanded[i].Y, expanded[i].Z);
            colors[i] = new Color32(255, 255, 255, 255);
        }
        var triangles = new List<int>();
        for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
        {
            if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                throw new InvalidOperationException("Control outline source is not triangulated.");
            triangles.AddRange(mesh.GetTriangles(submesh));
        }
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.subMeshCount = 1;
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        var go = new GameObject("White grab outline", typeof(MeshFilter), typeof(MeshRenderer));
        _objects.Add(go);
        go.layer = source.gameObject.layer;
        go.transform.SetParent(source.transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.enabled = false;
        renderer.sharedMaterial = _material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _renderers.Add((source, renderer));
    }

    internal void SetVisible(bool visible)
    {
        foreach (var item in _renderers)
        {
            if (item.outline == null)
                continue;
            var enabled = visible && item.source != null && item.source.enabled && !item.source.forceRenderingOff;
            if (item.outline.enabled != enabled)
                item.outline.enabled = enabled;
        }
    }

    public void Dispose()
    {
        SetVisible(false);
        foreach (var go in _objects)
            if (go != null)
            {
                // Destruction is deferred by Unity. Detach immediately so a
                // failed acquisition cannot enter the subsequent mirror scan.
                go.SetActive(false);
                go.transform.SetParent(null, false);
                Object.Destroy(go);
            }
        foreach (var mesh in _meshes)
            Object.Destroy(mesh);
        Object.Destroy(_material);
        _material = null;
        _objects.Clear();
        _meshes.Clear();
        _renderers.Clear();
    }
}
