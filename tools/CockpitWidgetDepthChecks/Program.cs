using System.Reflection;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

var count=0;
void Check(bool ok,string why){if(!ok)throw new Exception(why);count++;}
void Call(object target,string method)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,null);
var ui=new Shader("UI/Default");var font=new Shader("TextMeshPro/Mobile/Distance Field");
var additive=new Shader("TextMeshPro/Mobile/Distance Field Additive");var overlay=new Shader("TextMeshPro/Distance Field Overlay");
var imageShader=new Shader("UI/AdditiveMasked");var custom=new Shader("Modded/Custom avionic display");
var owner=new GameObject("depth owner").AddComponent<CockpitWidgetDepthMaterials>();
var roots=new List<GameObject>();var graphics=new List<Graphic>();
Graphic MakeGraphic(Transform parent,Shader shader)
{
    var go=new GameObject("native live graphic");go.transform.SetParent(parent);var graphic=go.AddComponent<Graphic>();graphic.material=new Material(shader){renderQueue=4000};return graphic;
}
foreach(var id in HudWidgetProfiles.Ids)
{
    var root=new GameObject(id);root.AddComponent<Canvas>().renderMode=RenderMode.WorldSpace;roots.Add(root);
    var content=new GameObject("content");content.transform.SetParent(root.transform);CockpitHudWidgets.Slots[id]=content.transform;
    graphics.Add(MakeGraphic(content.transform,additive));
}
Call(owner,"LateUpdate");
foreach(var graphic in graphics)
{
    var modifier=graphic.GetComponent<CockpitWidgetDepthMaterial>();
    Check(modifier!=null,"All nine widget types get scoped depth support");
    Check(modifier!.GetModifiedMaterial(graphic.material).shader==font,"Known additive text gets depth-respecting native font shader");
    Check(graphic.material.shader==additive,"Native shared material is never assigned or changed");
}
var target=graphics[0];var adapter=target.GetComponent<CockpitWidgetDepthMaterial>()!;var original=target.material;
var atlas=new object();original.Properties["_MainTex"]=atlas;original.Properties["_GradientScale"]=5f;original.Properties["_FaceDilate"]=.07f;
original.Properties["_Stencil"]=3;original.Properties["_StencilComp"]=8;original.Properties["_ClipRect"]="native rect";original.Properties["_UnderlaySoftness"]=.4f;
original.Keywords=new[]{"UNDERLAY_ON","UNITY_UI_CLIP_RECT"};
var copy=adapter.GetModifiedMaterial(original);
Check(copy!=original&&copy.renderQueue==3000,"Only the owned copy uses transparent UI queue");
Check(original.Properties.All(p=>copy.Properties.TryGetValue(p.Key,out var v)&&Equals(v,p.Value)),"Atlas, SDF, stencil and clip properties survive shader substitution");
Check(copy.Keywords.SequenceEqual(original.Keywords),"Font and masking keywords are preserved");
Check(ReferenceEquals(copy,adapter.GetModifiedMaterial(original)),"Repeated material rebuild reuses its owned copy");
original.Properties["_Stencil"]=7;adapter.GetModifiedMaterial(original);
Check(Equals(copy.Properties["_Stencil"],7),"In-place native stencil/material changes refresh the cached copy");
var masked=new Material(original);masked.Properties["_Stencil"]=11;
var newCopy=adapter.GetModifiedMaterial(masked);
Check(copy==null&&newCopy!=masked&&Equals(newCopy.Properties["_Stencil"],11),"A new masking base retires old copy and preserves new stencil");
var unknown=new Material(custom);
Check(adapter.GetModifiedMaterial(unknown)==unknown&&newCopy==null,"Unknown mod shader retains its authored rendering and releases unused copy");
var art=new Material(imageShader);art.Properties["_MainTex"]=atlas;art.Properties["_ColorMask"]=15;
art.Properties["_Stencil"]=4;art.Properties["_ClipRect"]="map viewport";art.Keywords=new[]{"UNITY_UI_CLIP_RECT"};
Check(ReferenceEquals(adapter.GetModifiedMaterial(art),art),"Native additive map-grid material is retained exactly, not converted to alpha blending over terrain");
Check(art.shader==imageShader&&art.Properties["_MainTex"]==atlas&&Equals(art.Properties["_Stencil"],4)&&
    Equals(art.Properties["_ClipRect"],"map viewport")&&art.Keywords.SequenceEqual(new[]{"UNITY_UI_CLIP_RECT"}),
    "Grid sprite, authored blending, stencil and rectangular clipping remain native");
foreach(var name in new[]{"UI/Additive","UI/ProceduralGrid","TextMeshPro/Distance Field","TextMeshPro/Mobile/Distance Field - Masking","TextMeshPro/Sprite","TextMeshPro/Custom Overlay"})
{
    var preserved=new Material(new Shader(name));
    Check(ReferenceEquals(adapter.GetModifiedMaterial(preserved),preserved),"Already depth-aware or unverified shaders retain authored blend/depth: "+name);
}
foreach(var name in new[]{"TextMeshPro/Distance Field Additive","TextMeshPro/Distance Field Additive with Glow"})
    Check(adapter.GetModifiedMaterial(new Material(new Shader(name))).shader==font,"Verified fixed-overlay font still receives hand depth: "+name);
Check(adapter.GetModifiedMaterial(new Material(overlay)).shader==font,"Native overlay font also respects physical hand depth");
Check(adapter.GetModifiedMaterial(new Material(ui)).shader==ui,"Already depth-compatible UI is left unchanged");
var late=MakeGraphic(CockpitHudWidgets.Slots["map"],additive);Call(owner,"LateUpdate");
Check(late.GetComponent<CockpitWidgetDepthMaterial>()!=null,"Native icons or text created after initial attachment are discovered");
var editor=MakeGraphic(roots[0].transform,imageShader);Call(owner,"LateUpdate");
Check(editor.GetComponent<CockpitWidgetDepthMaterial>()!=null,"Layout editor graphics outside the content subtree are included");
var nativeMenu=new GameObject("full map/menu");nativeMenu.AddComponent<Canvas>().renderMode=RenderMode.WorldSpace;
target.transform.SetParent(nativeMenu.transform);
Check(adapter.GetModifiedMaterial(original)==original,"A graphic leaving the widget immediately falls back to its native material");
var dirty=target.Dirties;Call(owner,"LateUpdate");
Check(target.Dirties>dirty,"Lease release marks cached renderer material dirty before presentation");
target.transform.SetParent(CockpitHudWidgets.Slots["map"]);Call(owner,"LateUpdate");
Check(adapter.GetModifiedMaterial(original)!=original,"Returning native content reacquires its scoped modifier");
roots[0].activeSelf=false;Call(owner,"LateUpdate");
Check(adapter.GetModifiedMaterial(original)==original,"Hidden or menu-suspended widget restores unmodified material");
roots[0].activeSelf=true;Call(owner,"LateUpdate");
roots[0].GetComponent<Canvas>()!.renderMode=RenderMode.ScreenSpaceOverlay;
Check(adapter.GetModifiedMaterial(original)==original,"Screen-space canvas never gets world-depth substitution");
roots[0].GetComponent<Canvas>()!.renderMode=RenderMode.WorldSpace;Call(owner,"LateUpdate");
copy=adapter.GetModifiedMaterial(original);Call(owner,"OnDisable");
Check(copy==null&&adapter.GetModifiedMaterial(original)==original,"Disabling manager releases copied materials and native state");
Call(owner,"LateUpdate");copy=adapter.GetModifiedMaterial(original);Call(owner,"OnDestroy");
Check(copy==null&&target.GetComponent<CockpitWidgetDepthMaterial>()==null,"Teardown destroys only owned modifiers and materials");
Check(original.shader==additive&&original.Properties["_MainTex"]==atlas&&original.renderQueue==4000,"Full lifecycle preserves shared atlas, shader and render queue");
Console.WriteLine($"{count} source-linked widget material scope/preservation checks passed.");
