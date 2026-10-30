using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

// Original Blender-authored geometry; shared between all aircraft. Group pivots
// remain separate for animation, while static meshes batch by material.
internal sealed class CockpitPanelModel : IDisposable
{
    internal readonly Transform Root;
    internal readonly Dictionary<string, Transform> Groups = new();
    private readonly List<Mesh> _meshes = new();
    private readonly List<Material> _materials = new();
    private readonly Dictionary<string, List<Renderer>> _renderers = new();
    private readonly MaterialPropertyBlock _colorBlock = new();
    private readonly Dictionary<string, Color> _lastColors = new();
    private readonly Dictionary<string, int> _lastHighlights = new();
    private bool? _layoutState;
    private float _aspect = 1;
    private readonly List<(Mesh mesh, Vector3[] original, float[] centers, bool[] stretch)> _widthMeshes = new();
    private readonly Dictionary<Transform, Vector3> _groupPositions = new();

    internal CockpitPanelModel(Transform parent)
    {
        using var stream = typeof(CockpitPanelModel).Assembly.GetManifestResourceStream("NOVR.CockpitPanel.json")!;
        using var reader = new StreamReader(stream);
        var model = CockpitPanelAsset.Load(reader.ReadToEnd());
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ??
                     Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
        if (shader == null) throw new InvalidOperationException("No supported opaque cockpit-console shader is available.");
        Root = new GameObject("NOVR Universal Cockpit Panel").transform;
        Root.SetParent(parent, false);
        try
        {
            foreach (var surface in model.materials)
            {
                var c = surface.color;
                var material = new Material(shader) { name = "NOVR console " + surface.name };
                var color = new Color(c[0], c[1], c[2], c[3]);
                material.color = color;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1);
                if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.One);
                if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Back);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)RenderQueue.Geometry;
                _materials.Add(material);
            }
            foreach (var group in model.groups)
            {
                var node = new GameObject(group.name).transform;
                node.SetParent(Root, false);
                node.localPosition = new Vector3(group.position[0], group.position[1], group.position[2]);
                Groups.Add(group.name, node); _groupPositions[node] = node.localPosition;
                _renderers[group.name] = new List<Renderer>();
                foreach (var batch in model.parts.Where(p => p.group == group.name).GroupBy(p => p.material))
                {
                    var vertices = new List<Vector3>();
                    var triangles = new List<int>();
                    var centers = new List<float>(); var stretch = new List<bool>();
                    foreach (var part in batch)
                    {
                        var offset = vertices.Count;
                        var xs = Enumerable.Range(0, part.vertices.Length / 3).Select(i => part.vertices[i * 3]).ToArray();
                        var center = (xs.Min() + xs.Max()) * .5f;
                        var wide = part.name.StartsWith("Chassis") || part.name.StartsWith("Face") || part.name.StartsWith("Edge rail") ||
                            part.name.Contains("MFD gasket") || part.name.Contains("MFD bezel") || part.name.Contains("MFD screen") || part.name.StartsWith("Switch bank");
                        for (var i = 0; i < part.vertices.Length; i += 3)
                        { vertices.Add(new Vector3(part.vertices[i], part.vertices[i + 1], part.vertices[i + 2])); centers.Add(center); stretch.Add(group.name == "static" && wide); }
                        triangles.AddRange(part.triangles.Select(index => index + offset));
                    }
                    var mesh = new Mesh { name = "Console " + group.name, indexFormat = IndexFormat.UInt32 };
                    mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    var light = new Vector3(-.3f, .6f, -.8f).normalized;
                    mesh.colors = mesh.normals.Select(normal =>
                    {
                        var shade = .65f + .35f * Mathf.Max(0, Vector3.Dot(normal, light));
                        return new Color(shade, shade, shade, 1);
                    }).ToArray();
                    _meshes.Add(mesh);
                    if (group.name == "static") _widthMeshes.Add((mesh, vertices.ToArray(), centers.ToArray(), stretch.ToArray()));
                    var go = new GameObject("Console surface", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(node, false);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = _materials[batch.Key];
                    _renderers[group.name].Add(renderer);
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
            }
            LayerHelper.SetLayerRecursive(Root, LayerHelper.GetVrUiLayer());
        }
        catch { Dispose(); throw; }
    }

    // Change the housing and display apertures, preserving lettering and
    // switch/handle dimensions. The MFD canvas is laid out at the same width.
    internal void SetAspect(float aspect)
    {
        aspect = Mathf.Clamp(aspect, .85f, 1.75f);
        if (Mathf.Abs(aspect - _aspect) < .001f) return;
        _aspect = aspect;
        foreach (var entry in _widthMeshes)
        {
            var vertices = (Vector3[])entry.original.Clone();
            for (var i = 0; i < vertices.Length; i++) vertices[i].x = NOVR.VrUi.SpecialBehavior.HudPresentationMath.PanelVertex(vertices[i].x, entry.centers[i], aspect, entry.stretch[i]);
            entry.mesh.vertices = vertices; entry.mesh.RecalculateBounds(); entry.mesh.RecalculateNormals();
        }
        foreach (var pair in _groupPositions)
        { var p = pair.Key.localPosition; p.x = pair.Value.x * aspect; pair.Key.localPosition = p; }
        if (_layoutOutline != null) _layoutOutline.transform.localScale = new Vector3(aspect, 1, 1);
    }

    private LineRenderer? _layoutOutline;
    internal void SetLayoutMode(bool editing)
    {
        if (_layoutState == editing) return;
        _layoutState = editing;
        foreach (var group in _renderers.Values) foreach (var renderer in group) renderer.enabled = !editing;
        if (editing && _layoutOutline == null)
        {
            var go = new GameObject("Panel layout outline", typeof(LineRenderer)); go.transform.SetParent(Root, false);
            _layoutOutline = go.GetComponent<LineRenderer>(); _layoutOutline.useWorldSpace = false; go.transform.localScale = new Vector3(_aspect, 1, 1);
            _layoutOutline.sharedMaterial = _materials[6]; _layoutOutline.widthMultiplier = .004f;
            _layoutOutline.positionCount = 5;
            _layoutOutline.SetPositions(new[] { new Vector3(-.385f,-.18f,-.05f), new Vector3(-.385f,.18f,-.05f),
                new Vector3(.385f,.18f,-.05f), new Vector3(.385f,-.18f,-.05f), new Vector3(-.385f,-.18f,-.05f) });
            go.layer = (int)LayerHelper.GetVrUiLayer();
        }
        if (_layoutOutline != null) _layoutOutline.enabled = editing;
    }

    internal void SetGroupColor(string group, Color color)
    {
        if (!_renderers.TryGetValue(group, out var renderers)) return;
        if (_lastColors.TryGetValue(group, out var previous) && previous == color) return;
        _lastColors[group] = color; _lastHighlights.Remove(group);
        _colorBlock.SetColor("_BaseColor", color); _colorBlock.SetColor("_Color", color);
        foreach (var renderer in renderers) renderer.SetPropertyBlock(_colorBlock);
    }

    internal void Highlight(string group, int state)
    {
        if (_lastHighlights.TryGetValue(group, out var previous) && previous == state) return;
        _lastHighlights[group] = state; _lastColors.Remove(group);
        foreach (var renderer in _renderers[group])
        {
            if (state == 0) { renderer.SetPropertyBlock(null); continue; }
            var accent = state == 2 ? new Color(.2f, 1f, .5f) : new Color(1f, .7f, .1f);
            var color = Color.Lerp(renderer.sharedMaterial.color, accent, .4f);
            _colorBlock.SetColor("_BaseColor", color); _colorBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(_colorBlock);
        }
    }

    public void Dispose()
    {
        if (Root != null) Object.Destroy(Root.gameObject);
        foreach (var mesh in _meshes) Object.Destroy(mesh);
        foreach (var material in _materials) Object.Destroy(material);
    }
}
