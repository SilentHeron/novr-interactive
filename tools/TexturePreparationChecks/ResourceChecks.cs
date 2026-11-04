using NOVR.MotionControls;
using UnityEngine;
using U = UnityEngine.Object;

internal static class ResourceChecks
{
    private static int _assertions;
    private static void Assert(bool value,string message)
    { _assertions++; if(!value)throw new InvalidOperationException(message); }
    internal static void Run()
    {
        foreach(var failure in new[]
        {
            ("Fabric", "decode"), ("GloveColor", "decode"), ("GloveColor", "read"),
            ("Bdf glove", "upload"), ("Bdf glove", "apply"),
            ("GloveNormal", "decode"), ("GloveNormal", "pixels"), ("GloveNormal", "apply")
        })
        {
            U.Created.Clear();
            Texture2D.Fail=(texture,operation)=>texture.name.Contains(failure.Item1)&&operation==failure.Item2;
            var failed=false;
            try { using var unexpected=new PilotHandMaterials(new Faction{factionTag="BDF"}); }
            catch(Exception e) when(e is InvalidDataException or InvalidOperationException) { failed=true; }
            finally { Texture2D.Fail=null; }
            Assert(failed,"Fault was not exercised: "+failure);
            Assert(U.Created.OfType<Texture2D>().Any(),"Failure never allocated a texture");
            Assert(U.Created.OfType<Texture2D>().All(t=>t.DestroyCount==1),"Partial constructor leaked or double-destroyed textures: "+failure);
        }
        Console.WriteLine("PASS Material-owner construction rolls back all owned textures across eight decode/read/upload failures");

        foreach(var team in new[]{"BDF","PALA"})
        {
            U.Created.Clear();
            var owner=new PilotHandMaterials(new Faction{factionTag=team});
            var original=U.Created.OfType<Texture2D>().Single(t=>t.name=="NOVR GloveColor.jpg");
            var fabric=U.Created.OfType<Texture2D>().Single(t=>t.name=="NOVR PilotFabricDetail.png");
            var normal=U.Created.OfType<Texture2D>().Single(t=>t.name=="NOVR GloveNormal.png");
            Assert(original.DestroyCount==1,"Superseded team glove atlas remained resident");
            Assert(fabric.DestroyCount==0,"Fabric was removed before its pixels were captured");
            Assert(normal.Linear && normal.Pixels[0]==255 && normal.Pixels[1]==128 && normal.Pixels[3]==96,"Packed glove normal conversion changed");

            var native=new Material{name="Native pilot"};
            var color=new Texture{name="Native albedo",width=8,height=4};
            var bump=new Texture{name="Native normal",width=8,height=4,wrapModeU=TextureWrapMode.Clamp,wrapModeV=TextureWrapMode.Repeat};
            native.Maps["_BaseMap"]=color;native.Maps["_BumpMap"]=bump;native.Floats["_BumpScale"]=.65f;
            var first=owner.Sleeve(native);var second=owner.Sleeve(native);
            owner.Tick();
            Assert(fabric.DestroyCount==1,"Fabric texture was retained after its red-channel data was captured");
            Assert(SpinWait.SpinUntil(()=>
            {
                owner.Tick();
                return first.Maps["_BaseMap"]!=color && first.Maps["_BumpMap"]!=bump;
            },TimeSpan.FromSeconds(10)),"Deferred enhancements did not finish after fabric texture release");
            var enhanced=(Texture2D)first.Maps["_BaseMap"]!;
            var enhancedNormal=(Texture2D)first.Maps["_BumpMap"]!;
            Assert(enhanced.width==1024&&enhanced.height==512,"Source atlas aspect changed");
            Assert(enhancedNormal.Linear && enhancedNormal.wrapModeU==bump.wrapModeU && enhancedNormal.wrapModeV==bump.wrapModeV,"Normal linear/sampler settings changed");
            Assert(second.Maps["_BaseMap"]==enhanced&&second.Maps["_BumpMap"]==enhancedNormal,"Sleeves did not reuse the prepared atlas");
            var allocated=U.Created.Count;
            for(var i=0;i<40;i++)owner.Tick();
            Assert(U.Created.Count==allocated,"Unchanged steady state allocated more Unity resources");
            Assert(native.Maps["_BaseMap"]==color&&native.Maps["_BumpMap"]==bump,"Native material was changed");
            owner.Dispose();owner.Dispose();
            Assert(U.Created.OfType<Texture2D>().All(t=>t.DestroyCount==1),"Owner disposal leaked or double-destroyed a texture");
            Assert(first.DestroyCount==1&&second.DestroyCount==1,"Owned materials were not released once");
            Assert(native.DestroyCount==0&&color.DestroyCount==0&&bump.DestroyCount==0,"Borrowed native resources were destroyed");
        }
        Console.WriteLine("PASS Team source atlases release early, deferred jobs reuse dimensions/data, and steady-state resources stay stable");

        U.Created.Clear();
        var unrecognized=new PilotHandMaterials(new Faction{factionName="Custom faction"});
        var nativeGlove=U.Created.OfType<Texture2D>().Single(t=>t.name=="NOVR GloveColor.jpg");
        Assert(nativeGlove.DestroyCount==0,"Unchanged native-colored glove texture was released too soon");
        unrecognized.Dispose();
        Assert(U.Created.OfType<Texture2D>().All(t=>t.DestroyCount==1),"Unused fabric must still release if no sleeve is created");
        Console.WriteLine($"{_assertions} material resource lifetime assertions passed.");
    }
}
