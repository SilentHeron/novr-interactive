using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

// Spatial fade at the free shaft end. Only a 4.5 cm strip is transparent;
// the grip/head keeps its original opaque material and depth behavior.
// Native meshes/textures are never changed. Geometry is prepared once per handle.
internal sealed class NativeHandleFade : IDisposable
{
    private readonly List<Mesh> _meshes = new();
    private readonly List<Material> _materials = new();
    private readonly List<GameObject> _objects = new();
    private readonly List<(MeshFilter filter, Mesh original)> _originals = new();
    private readonly List<(MeshRenderer solid, MeshRenderer ghost)> _renderers = new();
    private readonly MaterialPropertyBlock _block = new();
    private struct Vertex
    {
        internal Vector3 Position, Normal;
        internal Vector4 Tangent;
        internal Vector2 Uv;
        internal Color Color;
        internal float Height;
        internal static Vertex Mix(Vertex a, Vertex b, float t) => new()
        {
            Position = Vector3.LerpUnclamped(a.Position,b.Position,t), Normal = Vector3.LerpUnclamped(a.Normal,b.Normal,t).normalized,
            Tangent = Vector4.LerpUnclamped(a.Tangent,b.Tangent,t), Uv = Vector2.LerpUnclamped(a.Uv,b.Uv,t),
            Color = Color.LerpUnclamped(a.Color,b.Color,t), Height = Mathf.LerpUnclamped(a.Height,b.Height,t)
        };
    }
    private sealed class Geometry
    {
        private readonly List<Vector3> _positions = new(), _normals = new();
        private readonly List<Vector4> _tangents = new();
        private readonly List<Vector2> _uv = new();
        private readonly List<Color> _colors = new();
        private readonly List<int>[] _triangles;
        internal Geometry(int groups) { _triangles = new List<int>[groups]; for (var i=0;i<groups;i++) _triangles[i]=new(); }
        internal void Add(List<Vertex> polygon, int group, bool fade, float bottom, float length)
        {
            for (var i=1;i+1<polygon.Count;i++)
            {
                if (Vector3.Cross(polygon[i].Position-polygon[0].Position,polygon[i+1].Position-polygon[0].Position).sqrMagnitude < 1e-16f) continue;
                foreach (var index in new[] { 0,i,i+1 })
                {
                    var v=polygon[index]; var color=v.Color;
                    if (fade)
                    {
                        var t = Mathf.Clamp01((v.Height-bottom)/length);
                        // Blend onto the original lit metal over the upper 1/3,
                        // then dissolve the lower 2/3. The overlap avoids a hard
                        // lighting seam between native Lit and Particles/Lit.
                        color.a *= t <= 2f/3 ? Mathf.SmoothStep(0,1,t*1.5f) : Mathf.SmoothStep(1,0,(t-2f/3)*3);
                    }
                    _triangles[group].Add(_positions.Count);
                    // A tiny outward bias prevents coplanar flicker in the overlap.
                    _positions.Add(v.Position + (fade ? v.Normal*.00002f : Vector3.zero)); _normals.Add(v.Normal);
                    _tangents.Add(v.Tangent); _uv.Add(v.Uv); _colors.Add(color);
                }
            }
        }
        internal Mesh Make(string name)
        {
            var mesh=new Mesh { name=name, indexFormat=_positions.Count>65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(_positions); mesh.SetNormals(_normals); mesh.SetTangents(_tangents); mesh.SetUVs(0,_uv); mesh.SetColors(_colors);
            mesh.subMeshCount=_triangles.Length;
            for(var i=0;i<_triangles.Length;i++) mesh.SetTriangles(_triangles[i],i,false);
            mesh.RecalculateBounds(); return mesh;
        }
    }
    private static List<Vertex> Clip(List<Vertex> polygon, float height, bool above)
    {
        var result=new List<Vertex>(); if (polygon.Count==0) return result;
        var previous=polygon[polygon.Count-1]; var previousInside=above ? previous.Height>=height : previous.Height<=height;
        foreach(var current in polygon)
        {
            var inside=above ? current.Height>=height : current.Height<=height;
            if (inside!=previousInside) result.Add(Vertex.Mix(previous,current,(height-previous.Height)/(current.Height-previous.Height)));
            if (inside) result.Add(current);
            previous=current; previousInside=inside;
        }
        return result;
    }
    internal NativeHandleFade(Transform root, IEnumerable<MeshRenderer> renderers)
    {
        var shader=Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (shader==null) return; // Keep the native opaque shaft if the shader is unavailable.
        const float length=.045f;
        var sources = new List<(MeshRenderer renderer, MeshFilter filter, Mesh original, Mesh source)>();
        try
        {
            // A rotated native bounds box extends below the actual throttle arm.
            // Measure the rendered vertices, so its tip really reaches zero alpha.
            var bottom = float.MaxValue;
            foreach(var renderer in renderers)
            {
                var filter=renderer.GetComponent<MeshFilter>(); var original=filter.sharedMesh;
                var source=NativeCockpitMesh.ReadableCopy(original);
                sources.Add((renderer,filter,original,source));
                var positions=source.vertices;
                for(var group=0;group<source.subMeshCount;group++)
                {
                    if(source.GetTopology(group)!=MeshTopology.Triangles) throw new InvalidOperationException("Unexpected handle topology.");
                    foreach(var index in source.GetTriangles(group))
                        bottom=Mathf.Min(bottom,root.InverseTransformPoint(renderer.transform.TransformPoint(positions[index])).y);
                }
            }
            foreach(var item in sources)
            {
                var renderer=item.renderer; var filter=item.filter; var original=item.original; var source=item.source;
                var positions=source.vertices; var normals=source.normals; var tangents=source.tangents; var uv=source.uv; var colors=source.colors;
                var vertices=new Vertex[positions.Length]; var hasEnd=false;
                for(var i=0;i<vertices.Length;i++)
                {
                    var height=root.InverseTransformPoint(renderer.transform.TransformPoint(positions[i])).y;
                    hasEnd |= height<bottom+length;
                    vertices[i]=new Vertex { Position=positions[i], Height=height, Normal=normals.Length==positions.Length?normals[i]:Vector3.up,
                        Tangent=tangents.Length==positions.Length?tangents[i]:new Vector4(1,0,0,1), Uv=uv.Length==positions.Length?uv[i]:Vector2.zero,
                        Color=colors.Length==positions.Length?colors[i]:Color.white };
                }
                if (!hasEnd) continue;
                var solid=new Geometry(source.subMeshCount); var ghost=new Geometry(source.subMeshCount);
                for(var group=0;group<source.subMeshCount;group++)
                {
                    if(source.GetTopology(group)!=MeshTopology.Triangles) throw new InvalidOperationException("Unexpected handle topology.");
                    var triangles=source.GetTriangles(group);
                    for(var i=0;i<triangles.Length;i+=3)
                    {
                        var polygon=new List<Vertex> { vertices[triangles[i]],vertices[triangles[i+1]],vertices[triangles[i+2]] };
                            solid.Add(Clip(polygon,bottom+length*2/3,true),group,false,bottom,length);
                        // Subdivide long shaft triangles so the fade stays in
                        // this short strip and eases smoothly to both endpoints.
                            for(var band=0;band<6;band++)
                                ghost.Add(Clip(Clip(polygon,bottom+length*band/6,true),bottom+length*(band+1)/6,false),group,true,bottom,length);
                    }
                }
                var top=solid.Make("NOVR opaque handle"); _meshes.Add(top);
                var end=ghost.Make("NOVR fading shaft end"); _meshes.Add(end);
                var materials=new Material[renderer.sharedMaterials.Length];
                for(var i=0;i<materials.Length;i++)
                {
                    var material=new Material(renderer.sharedMaterials[i]) { shader=shader, name="NOVR shaft fade", renderQueue=(int)RenderQueue.Transparent };
                    _materials.Add(material); materials[i]=material;
                    material.SetOverrideTag("RenderType","Transparent");
                    material.SetFloat("_Surface",1); material.SetFloat("_Blend",0);
                    material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite",0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    foreach(var keyword in new[] { "_ALPHAPREMULTIPLY_ON","_ALPHATEST_ON","_SOFTPARTICLES_ON","_FADING_ON","_DISTORTION_ON" }) material.DisableKeyword(keyword);
                    material.SetShaderPassEnabled("ShadowCaster",false);
                }
                var go=new GameObject("NOVR ghost shaft end",typeof(MeshFilter),typeof(MeshRenderer)); _objects.Add(go);
                go.transform.SetParent(renderer.transform,false); go.layer=renderer.gameObject.layer;
                go.GetComponent<MeshFilter>().sharedMesh=end;
                var ghostRenderer=go.GetComponent<MeshRenderer>(); ghostRenderer.sharedMaterials=materials;
                ghostRenderer.shadowCastingMode=ShadowCastingMode.Off; ghostRenderer.receiveShadows=renderer.receiveShadows;
                ghostRenderer.lightProbeUsage=renderer.lightProbeUsage; ghostRenderer.reflectionProbeUsage=renderer.reflectionProbeUsage;
                _renderers.Add((renderer,ghostRenderer));
                _originals.Add((filter,original)); filter.sharedMesh=top;
            }
            SyncPropertyBlocks();
        }
        catch { Dispose(); throw; }
        finally { foreach(var item in sources) Object.Destroy(item.source); }
    }
    internal void SyncPropertyBlocks()
    {
        // Keep native tint and the hover/grab highlight continuous across the seam.
        foreach(var item in _renderers)
        {
            item.solid.GetPropertyBlock(_block);
            item.ghost.SetPropertyBlock(_block);
        }
    }
    public void Dispose()
    {
        foreach(var item in _originals) if(item.filter!=null) item.filter.sharedMesh=item.original;
        foreach(var go in _objects) if(go!=null) Object.Destroy(go);
        foreach(var mesh in _meshes) if(mesh!=null) Object.Destroy(mesh);
        foreach(var material in _materials) if(material!=null) Object.Destroy(material);
        _originals.Clear(); _objects.Clear(); _meshes.Clear(); _materials.Clear(); _renderers.Clear();
    }
}
