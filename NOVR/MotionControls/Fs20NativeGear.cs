using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

// The installed cockpit wheel and white stem are static islands in cockpit_int.
// Retain their exact geometry/material, adding only a small state-driven hinge
// animation. There is no native cockpit gear-lever animation to reuse.
internal sealed class Fs20NativeGear : IDisposable
{
    internal const int TriangleCount = 194;
    internal static readonly Vector3 NativePoint = new(-.23004267f,.28262943f,.48968200f);
    private static readonly Vector3 Pivot = new(-.23003417f,.27203812f,.55128582f);
    private static readonly Bounds Wheel = Region(new(-.239357606f,.258742511f,.465798825f),new(-.220727727f,.306516349f,.513565183f));
    private static readonly Bounds Stem = Region(new(-.242522180f,.239975467f,.495313853f),new(-.217546150f,.288943946f,.551286757f));
    private readonly Fs20ControlCover _cover;
    private readonly Mesh _mesh;
    private readonly GameObject _object;
    private bool _initialized, _disposed;
    internal Transform Contact { get; }
    internal Vector3 Center => Contact.position;
    internal Pose Pose => new(Center,Contact.rotation);

    private static Bounds Region(Vector3 min,Vector3 max) => new((min+max)*.5f,max-min+Vector3.one*.00004f);
    internal static bool Contains(Vector3 a,Vector3 b,Vector3 c) =>
        Wheel.Contains(a)&&Wheel.Contains(b)&&Wheel.Contains(c) || Stem.Contains(a)&&Stem.Contains(b)&&Stem.Contains(c);

    // Parent must share the native cockpit mesh's coordinates (the integrated
    // controls root is an identity child of cockpit_int). Read the cover's saved
    // original so construction also works after the cover is already visible.
    internal Fs20NativeGear(Fs20ControlCover cover,MeshFilter nativeFilter,Transform parent)
    {
        _cover=cover; _mesh=NativeCockpitMesh.ReadableCopy(cover.OriginalMesh);
        _object=new GameObject("FS-20 original gear wheel and stem",typeof(MeshFilter),typeof(MeshRenderer));
        _object.SetActive(false);
        Contact=new GameObject("Native gear grip center").transform;
        Contact.SetParent(_object.transform,false);
        try
        {
            var vertices=_mesh.vertices;
            if(vertices.Length!=12647 || _mesh.subMeshCount!=1 || _mesh.GetTopology(0)!=MeshTopology.Triangles)
                throw new InvalidOperationException("FS-20 native gear source does not match.");
            var source=_mesh.GetTriangles(0);
            if(source.Length!=13292*3) throw new InvalidOperationException("FS-20 native gear topology changed.");
            var indices=new List<int>(TriangleCount*3);
            var bounds=new Bounds(NativePoint-Pivot,Vector3.zero);
            for(var i=0;i<source.Length;i+=3)
            {
                if(!Contains(vertices[source[i]],vertices[source[i+1]],vertices[source[i+2]])) continue;
                for(var j=0;j<3;j++) { indices.Add(source[i+j]); bounds.Encapsulate(vertices[source[i+j]]-Pivot); }
            }
            if(indices.Count!=TriangleCount*3) throw new InvalidOperationException("FS-20 native gear islands changed.");
            // Keep all original vertex streams (including native UV/material
            // attributes). Only these 194 triangles are drawn by this renderer.
            for(var i=0;i<vertices.Length;i++) vertices[i]-=Pivot;
            _mesh.vertices=vertices; _mesh.SetTriangles(indices,0,false); _mesh.bounds=bounds;
            _mesh.name="NOVR original FS-20 gear wheel and stem";
            _object.transform.SetParent(parent,false); _object.transform.localPosition=Pivot;
            _object.layer=(int)LayerHelper.Layers.Cockpit;
            Contact.localPosition=NativePoint-Pivot; Contact.localRotation=Fs20PanelLayout.Face;
            _object.GetComponent<MeshFilter>().sharedMesh=_mesh;
            var original=nativeFilter.GetComponent<MeshRenderer>();
            var renderer=_object.GetComponent<MeshRenderer>(); renderer.sharedMaterials=original.sharedMaterials;
            renderer.shadowCastingMode=original.shadowCastingMode; renderer.receiveShadows=original.receiveShadows;
            renderer.lightProbeUsage=original.lightProbeUsage; renderer.reflectionProbeUsage=original.reflectionProbeUsage;
            var properties=new MaterialPropertyBlock(); original.GetPropertyBlock(properties); renderer.SetPropertyBlock(properties);
            _cover.SetGearDetached(true);
            _object.SetActive(true);
        }
        catch { Dispose(); throw; }
    }

    internal void Update(bool gearUp,float deltaTime)
    {
        if(_disposed) return;
        var target=Quaternion.Euler(gearUp ? 25f : 0f,0,0);
        _object.transform.localRotation=_initialized
            ? Quaternion.Slerp(_object.transform.localRotation,target,FlightControlMath.SmoothingWeight(deltaTime,18f))
            : target;
        _initialized=true;
    }

    public void Dispose()
    {
        if(_disposed) return;
        _disposed=true; _cover.SetGearDetached(false);
        if(_object!=null) { _object.SetActive(false); Object.Destroy(_object); }
        if(_mesh!=null) Object.Destroy(_mesh);
    }
}
