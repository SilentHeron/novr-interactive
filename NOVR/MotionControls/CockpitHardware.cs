using System.Collections.Generic;
using UnityEngine;

namespace NOVR.MotionControls;

// Small manufactured keycaps: chamfered corners, an inset front face and
// individual face normals so cockpit lighting reveals their actual depth.
internal static class CockpitHardware
{
    internal static Mesh Keycap(Vector3 size)
    {
        var vertices = new List<Vector3>(); var indices = new List<int>();
        var bevel = Mathf.Min(.0025f, size.z * .22f);
        Vector3[] Ring(float inset, float z)
        {
            var x = size.x * .5f - inset; var y = size.y * .5f - inset;
            var c = Mathf.Min(.004f, Mathf.Min(x,y) * .25f);
            return new[] { new Vector3(-x+c,-y,z), new Vector3(x-c,-y,z), new Vector3(x,-y+c,z), new Vector3(x,y-c,z),
                new Vector3(x-c,y,z), new Vector3(-x+c,y,z), new Vector3(-x,y-c,z), new Vector3(-x,-y+c,z) };
        }
        void Face(Vector3 a, Vector3 b, Vector3 c)
        { var start=vertices.Count; vertices.Add(a);vertices.Add(b);vertices.Add(c); indices.Add(start);indices.Add(start+1);indices.Add(start+2); }
        var front=Ring(bevel,-size.z*.5f);var shoulder=Ring(0,-size.z*.5f+bevel);var back=Ring(0,size.z*.5f);
        for(var i=0;i<8;i++)
        {
            var j=(i+1)%8;
            Face(new Vector3(0,0,-size.z*.5f),front[j],front[i]);
            Face(front[i],front[j],shoulder[j]);Face(front[i],shoulder[j],shoulder[i]);
            Face(shoulder[i],shoulder[j],back[j]);Face(shoulder[i],back[j],back[i]);
            Face(new Vector3(0,0,size.z*.5f),back[i],back[j]);
        }
        var uv = new List<Vector2>(vertices.Count);
        foreach (var vertex in vertices) uv.Add(new Vector2(vertex.x/size.x+.5f,vertex.y/size.y+.5f));
        var mesh=new Mesh { name="Chamfered cockpit keycap" };mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);
        mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }
}
