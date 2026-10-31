using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

// Hide only measured decorative islands beneath the replacement FS-20 controls.
// This is a private index-buffer copy: no renderer, material, seat, display or
// cockpit object is hidden. Turning off the integrated controls restores it.
internal sealed class Fs20ControlCover : IDisposable
{
    private readonly MeshFilter _filter;
    private readonly Mesh _original, _covered;
    private Mesh? _withoutGear;
    private bool _visible, _gearDetached;
    internal Mesh OriginalMesh => _original;
    private readonly struct Region
    {
        internal readonly Bounds Bounds;
        internal readonly int Triangles;
        internal Region(Vector3 min, Vector3 max, int triangles)
        { Bounds = new Bounds((min+max)*.5f,max-min+Vector3.one*.00004f); Triangles = triangles; }
    }
    // Native Nuclear Option 0.34.3 SmallFighter1 cockpit. Bounds contain only
    // redundant caps, stems and fasteners that intersect the added housings.
    private static readonly Region[] Regions =
    {
        new(new(0.16200116f,0.19433820f,0.51513952f), new(0.18482304f,0.20790878f,0.52293235f), 30), // aots
        new(new(-0.02549472f,0.20805314f,0.49345097f), new(-0.00349333f,0.23132102f,0.51945156f), 82), // brake
        new(new(0.00349333f,0.20805314f,0.49345103f), new(0.02549472f,0.23132102f,0.51945156f), 82), // radar
        new(new(-0.02549472f,0.17151071f,0.48580012f), new(-0.00349333f,0.19477858f,0.51180071f), 82), // brake
        new(new(0.00349333f,0.17151070f,0.48580015f), new(0.02549472f,0.19477855f,0.51180077f), 82), // radar
        new(new(0.13764651f,0.19433679f,0.51513904f), new(0.16046838f,0.20790736f,0.52293193f), 30), // aots
        new(new(0.11341430f,0.19433536f,0.51513904f), new(0.13623621f,0.20790593f,0.52293187f), 30), // aots
        new(new(-0.08791168f,0.19433253f,0.51513857f), new(-0.06508981f,0.20790309f,0.52293146f), 30), // brake
        new(new(0.06508979f,0.19433253f,0.51513851f), new(0.08791168f,0.20790309f,0.52293140f), 30), // radar
        new(new(-0.06355703f,0.19433110f,0.51513809f), new(-0.04073515f,0.20790166f,0.52293098f), 30), // brake
        new(new(0.04073517f,0.19433111f,0.51513809f), new(0.06355704f,0.20790166f,0.52293092f), 30), // radar
        new(new(0.02649481f,0.16731220f,0.50515890f), new(0.03312320f,0.17314178f,0.50748354f), 28), // radar
        new(new(-0.03312321f,0.16731218f,0.50515890f), new(-0.02649481f,0.17314176f,0.50748354f), 28), // brake
        new(new(0.04164381f,0.21396184f,0.51288331f), new(0.04827221f,0.21979144f,0.51520783f), 28), // radar
        new(new(-0.04827221f,0.21396187f,0.51288325f), new(-0.04164381f,0.21979147f,0.51520771f), 28), // brake
        new(new(0.17694351f,0.21361306f,0.51280981f), new(0.18357187f,0.21944265f,0.51513439f), 28), // aots
    };
    internal Fs20ControlCover(MeshFilter filter)
    {
        _filter=filter; _original=filter.sharedMesh; _covered=NativeCockpitMesh.ReadableCopy(_original);
        _covered.name="NOVR FS-20 covered native controls";
        try
        {
            var vertices=_covered.vertices;
            if(vertices.Length!=12647 || _covered.subMeshCount!=1 || _covered.GetTopology(0)!=MeshTopology.Triangles)
                throw new InvalidOperationException("FS-20 cockpit geometry does not match the measured control cover.");
            var triangles=_covered.GetTriangles(0);
            if(triangles.Length!=13292*3) throw new InvalidOperationException("FS-20 cockpit topology changed.");
            var counts=new int[Regions.Length]; var retained=new List<int>(triangles.Length); var removed=0;
            for(var i=0;i<triangles.Length;i+=3)
            {
                var hide=false;
                for(var r=0;r<Regions.Length;r++)
                {
                    var bounds=Regions[r].Bounds;
                    if(!bounds.Contains(vertices[triangles[i]]) || !bounds.Contains(vertices[triangles[i+1]]) || !bounds.Contains(vertices[triangles[i+2]])) continue;
                    counts[r]++; hide=true;
                }
                if(hide) { removed++; continue; }
                retained.Add(triangles[i]); retained.Add(triangles[i+1]); retained.Add(triangles[i+2]);
            }
            for(var r=0;r<Regions.Length;r++)
                if(counts[r]!=Regions[r].Triangles) throw new InvalidOperationException("FS-20 decorative control island changed: "+r);
            if(removed!=678) throw new InvalidOperationException("Unexpected FS-20 control cover size.");
            _covered.SetTriangles(retained,0,false); _covered.bounds=_original.bounds;
            Debug.Log("[NOVR] FS-20 native control cover: 16 measured switch cover regions, 678/13292 triangles hidden; native displays, trim, gear knob and seat retained.");
        }
        catch { Object.Destroy(_covered); throw; }
    }
    internal void SetVisible(bool visible)
    {
        _visible=visible;
        if(_filter!=null && (_filter.sharedMesh==_original || _filter.sharedMesh==_covered || _filter.sharedMesh==_withoutGear))
            _filter.sharedMesh=visible ? _gearDetached ? _withoutGear! : _covered : _original;
    }
    // Detach only after the replacement using the exact native triangles exists.
    // If extraction fails, the ordinary covered mesh keeps the original lever.
    internal void SetGearDetached(bool detached)
    {
        if(detached && _withoutGear==null)
        {
            var mesh=NativeCockpitMesh.ReadableCopy(_covered);
            try
            {
                var vertices=mesh.vertices; var triangles=mesh.GetTriangles(0);
                var retained=new List<int>(triangles.Length); var removed=0;
                for(var i=0;i<triangles.Length;i+=3)
                {
                    if(Fs20NativeGear.Contains(vertices[triangles[i]],vertices[triangles[i+1]],vertices[triangles[i+2]]))
                    { removed++; continue; }
                    retained.Add(triangles[i]); retained.Add(triangles[i+1]); retained.Add(triangles[i+2]);
                }
                if(removed!=Fs20NativeGear.TriangleCount) throw new InvalidOperationException("FS-20 native gear geometry changed.");
                mesh.SetTriangles(retained,0,false); mesh.bounds=_original.bounds;
                mesh.name="NOVR FS-20 covered controls with detached native gear";
                _withoutGear=mesh;
            }
            catch { Object.Destroy(mesh); throw; }
        }
        _gearDetached=detached; SetVisible(_visible);
    }
    public void Dispose()
    {
        SetVisible(false);
        if(_withoutGear!=null) Object.Destroy(_withoutGear);
        if(_covered!=null) Object.Destroy(_covered);
    }
}
