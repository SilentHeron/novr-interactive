using System;
using System.Collections.Generic;
using System.IO;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;
using Object=UnityEngine.Object;
namespace NOVR.MotionControls;

// Own only material/texture copies. Installed pilot atlases are sampled in memory,
// never altered, written to disk, or distributed with the mod.
internal sealed class PilotHandMaterials : IDisposable
{
    private readonly List<Material> _materials=new();
    private readonly List<Texture2D> _textures=new();
    private sealed class Enhancement
    {
        internal readonly Texture Source;
        internal readonly List<Material> Targets = new();
        internal Texture2D? Ready;
        internal int Size;
        internal double SnapshotMilliseconds;
        internal Enhancement(Texture source) => Source = source;
    }
    private readonly Dictionary<Texture,Enhancement> _enhanced=new();
    private readonly Queue<Enhancement> _pending=new();
    private Enhancement? _processing;
    private DeferredTextureJob? _work;
    private byte[]? _fabricRed;
    private bool _disposed;
    private readonly Texture2D _fabric, _gloveColor, _gloveNormal;
    internal PilotHandMaterials()
    {
        _fabric=Load("PilotFabricDetail.png",false,false);
        _gloveColor=Load("GloveColor.jpg",false);
        _gloveNormal=Load("GloveNormal.png",true,false);
        // Unity's desktop normal-map unpack expects X in alpha, Y in green.
        var pixels=_gloveNormal.GetPixels32();for(var i=0;i<pixels.Length;i++)pixels[i]=new Color32(255,pixels[i].g,255,pixels[i].r);
        _gloveNormal.SetPixels32(pixels);_gloveNormal.Apply(true,true);
    }
    private Texture2D Load(string file,bool linear,bool discard=true)
    {
        using var stream=typeof(PilotHandMaterials).Assembly.GetManifestResourceStream("NOVR."+file) ?? throw new InvalidDataException("Missing hand texture "+file);
        using var memory=new MemoryStream();stream.CopyTo(memory);
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,true,linear){name="NOVR "+file,filterMode=FilterMode.Trilinear,anisoLevel=4,wrapMode=TextureWrapMode.Repeat};_textures.Add(texture);
        if(!texture.LoadImage(memory.ToArray(),discard))throw new InvalidDataException("Unreadable hand texture "+file);
        return texture;
    }
    internal Material Sleeve(Material native)
    {
        var material=new Material(native){name="NOVR detailed "+native.name};_materials.Add(material);
        var source=native.HasProperty("_BaseMap")?native.GetTexture("_BaseMap"):native.mainTexture;
        if(source!=null)
        {
            if(!_enhanced.TryGetValue(source,out var enhancement))
            {
                enhancement=new Enhancement(source);_enhanced.Add(source,enhancement);_pending.Enqueue(enhancement);
            }
            enhancement.Targets.Add(material);
            // Render with the original atlas until the copied detail texture is ready.
            if(enhancement.Ready!=null)
            { SetTexture(material,"_BaseMap",enhancement.Ready);SetTexture(material,"_MainTex",enhancement.Ready); }
        }
        // Several stripped game shader variants ignore detail keywords. Bake a
        // restrained weave/local-contrast pass into our own albedo copy in the background.
        material.DisableKeyword("_DETAIL_MULX2");material.DisableKeyword("_DETAIL_SCALED");
        if(material.HasProperty("_BumpScale"))material.SetFloat("_BumpScale",.55f);
        if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.25f);
        return material;
    }
    internal void Tick()
    {
        if(_disposed)return;
        if(_work!=null)
        {
            if(!_work.TryTake(out var result))return;
            _work.Dispose();_work=null;
            var enhancement=_processing!;_processing=null;
            if(result.Error!=null)Debug.LogWarning("[NOVR] Sleeve detail preparation skipped; original atlas retained: "+result.Error);
            else if(result.Pixels!=null && enhancement.Source!=null)
            {
                try
                {
                    var timer=Stopwatch.StartNew();
                    var size=enhancement.Size;
                    var texture=new Texture2D(size,size,TextureFormat.RGBA32,true){name="NOVR enhanced pilot albedo",filterMode=FilterMode.Trilinear,anisoLevel=4};_textures.Add(texture);
                    texture.SetPixelData(result.Pixels,0);texture.Apply(true,true);enhancement.Ready=texture;
                    foreach(var material in enhancement.Targets)if(material!=null)
                    { SetTexture(material,"_BaseMap",texture);SetTexture(material,"_MainTex",texture); }
                    Debug.Log($"[NOVR] Pilot sleeve detail ready at {size}x{size}; snapshot {enhancement.SnapshotMilliseconds:F1} ms, background filter {result.Milliseconds:F1} ms, upload {timer.Elapsed.TotalMilliseconds:F1} ms. Native atlas preserved.");
                }
                catch(Exception e){Debug.LogWarning("[NOVR] Sleeve detail upload unavailable: "+e.Message);}
            }
            return; // Never upload and capture another atlas in the same frame.
        }
        if(_pending.Count==0)return;
        var next=_pending.Dequeue();if(next.Source==null)return;
        try
        {
            var timer=Stopwatch.StartNew();
            var size=Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(next.Source.width,next.Source.height))*2,1024,2048);
            var input=Snapshot(next.Source,size);
            var fabric=_fabricRed ??= Array.ConvertAll(_fabric.GetPixels32(),pixel=>pixel.r);
            var width=_fabric.width;var height=_fabric.height;
            next.Size=size;next.SnapshotMilliseconds=timer.Elapsed.TotalMilliseconds;_processing=next;
            // Capture arrays/scalars only. Texture creation, reads and uploads stay on Unity's thread.
            _work=new DeferredTextureJob(token=>PilotTextureEnhancement.Apply(input,size,fabric,width,height,token));
        }
        catch(Exception e){_processing=null;Debug.LogWarning("[NOVR] Sleeve detail capture skipped; original atlas retained: "+e.Message);}
    }
    private static byte[] Snapshot(Texture source,int size)
    {
        var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(size,size,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);Texture2D? texture=null;
        try
        {
            Graphics.Blit(source,rt);RenderTexture.active=rt;
            texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.ReadPixels(new Rect(0,0,size,size),0,0,false);
            // ReadPixels populated CPU data; an intermediate Apply/GPU upload is unnecessary.
            return texture.GetRawTextureData();
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(texture!=null)Object.Destroy(texture);}
    }
    internal Material Glove(Material native,Rect patch)
    {
        var material=new Material(native){name="NOVR pilot-matched glove"};_materials.Add(material);
        var color=PilotColor(native,patch);var mean=Mathf.Max(.03f,(color.r+color.g+color.b)/3);
        var tint=new Color(Mathf.Clamp(color.r/mean*1.1f,.6f,1.3f),Mathf.Clamp(color.g/mean,.6f,1.3f),Mathf.Clamp(color.b/mean*.78f,.5f,1.3f),1);
        SetColor(material,"_BaseColor",tint);SetColor(material,"_Color",tint);
        SetTexture(material,"_BaseMap",_gloveColor);SetTexture(material,"_MainTex",_gloveColor);SetTexture(material,"_BumpMap",_gloveNormal);
        foreach(var property in new[]{"_MetallicGlossMap","_OcclusionMap","_EmissionMap","_DetailAlbedoMap","_DetailNormalMap","_DetailMask"})SetTexture(material,property,null);
        foreach(var keyword in new[]{"_METALLICSPECGLOSSMAP","_OCCLUSIONMAP","_EMISSION","_DETAIL_MULX2","_DETAIL_SCALED"})material.DisableKeyword(keyword);
        if(material.HasProperty("_BumpScale"))material.SetFloat("_BumpScale",.55f);
        if(material.HasProperty("_Metallic"))material.SetFloat("_Metallic",0);
        if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.22f);
        if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.22f);
        material.EnableKeyword("_NORMALMAP");return material;
    }
    private static void SetColor(Material m,string name,Color c){if(m.HasProperty(name))m.SetColor(name,c);}
    private static void SetTexture(Material m,string name,Texture? t){if(m.HasProperty(name)){m.SetTexture(name,t);m.SetTextureScale(name,Vector2.one);m.SetTextureOffset(name,Vector2.zero);}}
    private static Color PilotColor(Material native,Rect patch)
    {
        var source=native.HasProperty("_BaseMap")?native.GetTexture("_BaseMap"):native.mainTexture;
        if(source==null)return new Color(.8f,.9f,.8f);
        var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(64,64,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);Texture2D? image=null;
        try
        {
            Graphics.Blit(source,rt);RenderTexture.active=rt;image=new Texture2D(64,64,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,64,64),0,0);image.Apply();
            var sum=Color.black;var count=0;
            for(var y=0;y<8;y++)for(var x=0;x<8;x++){var pixel=image.GetPixelBilinear(patch.x+patch.width*(x+.5f)/8,patch.y+patch.height*(y+.5f)/8);if(pixel.maxColorComponent<.05f)continue;sum+=pixel;count++;}
            return count>0?sum/count:Color.white;
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(image!=null)Object.Destroy(image);}
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        _work?.Dispose();_work=null;_processing=null;_pending.Clear();_enhanced.Clear();_fabricRed=null;
        foreach(var m in _materials)Object.Destroy(m);foreach(var t in _textures)Object.Destroy(t);_materials.Clear();_textures.Clear();
    }
}
