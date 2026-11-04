using NOVR.MotionControls;
internal static class BindingChecks
{
    internal static void Run()
    {
        var native=new UnityEngine.Texture();var other=new UnityEngine.Texture();var replacement=new UnityEngine.Texture();
        var material=new UnityEngine.Material();
        material.Maps["_BumpMap"]=native;material.Maps["_BaseMap"]=native;material.Maps["_MainTex"]=other;
        material.Maps["_DetailNormalMap"]=other;material.Maps["_OcclusionMap"]=other;
        PilotTextureBindings.Replace(material,native,replacement,true);
        if(material.Maps["_BumpMap"]!=replacement || material.Maps["_BaseMap"]!=native || material.Maps["_DetailNormalMap"]!=other)throw new Exception("Normal job changed another texture slot");
        PilotTextureBindings.Replace(material,native,replacement,false);
        if(material.Maps["_BaseMap"]!=replacement || material.Maps["_MainTex"]!=other || material.Maps["_OcclusionMap"]!=other)throw new Exception("Albedo job overwrote a distinct native map");
        PilotTextureBindings.Replace(material,native,other,true);
        if(material.Maps["_BumpMap"]!=replacement)throw new Exception("Late result overwrote a replaced normal slot");
        if(material.NormalStrength!=.65f || material.Scale!=(2f,3f) || material.Offset!=(.3f,.1f) || !material.Keyword)throw new Exception("Texture replacement changed native sampling or normal strength");
        PilotTextureBindings.Replace(new UnityEngine.Material(),native,replacement,true);
        Console.WriteLine("PASS Material replacement preserves sampler transforms, normal strength, distinct maps and missing slots");
    }
}
namespace UnityEngine
{
    internal sealed class Texture { }
    internal sealed class Material
    {
        internal readonly Dictionary<string,Texture> Maps=new();
        internal readonly float NormalStrength=.65f;internal readonly (float,float) Scale=(2f,3f),Offset=(.3f,.1f);internal readonly bool Keyword=true;
        internal bool HasProperty(string name)=>Maps.ContainsKey(name);
        internal Texture GetTexture(string name)=>Maps[name];
        internal void SetTexture(string name,Texture texture)=>Maps[name]=texture;
    }
}
