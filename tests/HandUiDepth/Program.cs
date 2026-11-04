using System.Reflection;
using NOVR;
using NOVR.MotionControls;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

var count = 0;
void Check(bool value, string why) { if (!value) throw new Exception(why); count++; }
void Near(Vector3 a, Vector3 b, string why) => Check((a - b).sqrMagnitude < .000001f, why);
SkinnedMeshRenderer[] Proxies() => Object.Created.OfType<SkinnedMeshRenderer>().Where(r => !r.Dead && r.gameObject.name == "NOVR hand UI depth").ToArray();
bool Visible(SkinnedMeshRenderer r) => r.enabled && r.gameObject.activeInHierarchy;
void Invoke(PilotHandUiDepth component, string name) => typeof(PilotHandUiDepth).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(component, null);
using (var f = new Fixture())
{
    f.Source(0, 100);
    f.Source(1, 1);
    f.Source(2, 100);
    f.Source(3, 100);
    f.Tick();
    var proxies = Proxies();
    Check(proxies.Length == 4 && proxies.All(Visible), "Displayed local gloves and sleeves each receive one live depth copy");
    for (var i = 0; i < 4; i++)
    {
        var source = PilotHandVisuals.Renderers[i]!;
        var proxy = proxies[i];
        Check(proxy.sharedMesh == source.sharedMesh, "Depth copy shares native displayed mesh, without duplicating geometry");
        Check(proxy.gameObject.layer == 30, "Only UI camera layer receives occlusion copy");
        Check(proxy.sharedMaterials.Length == source.sharedMesh.subMeshCount, "Every native submesh receives depth material");
        Check(proxy.localBounds == source.localBounds, "Depth copy retains displayed skinned bounds");
        Check(!proxy.receiveShadows && proxy.shadowCastingMode == ShadowCastingMode.Off && proxy.lightProbeUsage == LightProbeUsage.Off && proxy.reflectionProbeUsage == ReflectionProbeUsage.Off, "Depth geometry does not affect scene lighting");
        Check(proxy.updateWhenOffscreen && !proxy.allowOcclusionWhenDynamic, "UI depth does not inherit main-camera occlusion or stopped skinning");
        foreach (var vertex in new[] { Vector3.zero, new Vector3(.002f, .003f, -.001f) })
            Near(proxy.transform.TransformPoint(vertex), f.Ui.TransformPoint(f.Game.InverseTransformPoint(source.transform.TransformPoint(vertex))), "Renderer coordinates match tracking-to-UI mapping including 100x model scale");
        for (var bone = 0; bone < source.bones.Length; bone++)
        {
            var original = source.bones[bone];
            var copy = proxy.bones[bone];
            Near(copy.position, f.Ui.TransformPoint(f.Game.InverseTransformPoint(original.position)), "Copied bone position maps exactly into UI rig");
            foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
                Near(copy.TransformVector(axis * .001f), f.Ui.TransformVector(f.Game.InverseTransformVector(original.TransformVector(axis * .001f))), "Copied bone orientation and scale preserve skinned-space basis");
        }
        Check(proxy.rootBone == proxy.bones[0], "Root-bone reference points to owned copy");
    }
    var depth = proxies[0].sharedMaterials[0];
    Check(proxies.All(p => p.sharedMaterials.All(m => m == depth)), "All copies share one owned depth material");
    Check(depth.renderQueue == (int)RenderQueue.Geometry && depth.GetFloat("_Surface") == 0 && depth.GetFloat("_ZWrite") == 1, "Depth renders in opaque phase before transparent widgets");
    Check(depth.GetFloat("_SrcBlend") == 0 && depth.GetFloat("_DstBlend") == 1 && depth.GetFloat("_SrcBlendAlpha") == 0 && depth.GetFloat("_DstBlendAlpha") == 1, "Source contributes neither color nor alpha; destination is preserved exactly");
    Check(depth.GetFloat("_AlphaClip") == 0 && depth.GetFloat("_AlphaToMask") == 0 && !depth.Keywords.Contains("_ALPHATEST_ON") && !depth.Keywords.Contains("_SURFACE_TYPE_TRANSPARENT"), "Opaque occlusion cannot inherit alpha clipping or transparency");
    Check(depth.Colors["_BaseColor"] == Color.white && depth.Textures["_BaseMap"] == Texture2D.whiteTexture, "Depth material has fully opaque texture coverage");
    Check(PilotHandVisuals.Renderers.All(r => r!.sharedMaterials.All(m => m != depth && m.GetFloat("_ZWrite") == 0)), "Visible glove and sleeve materials remain untouched");

    var left = PilotHandVisuals.Renderers[0]!;
    left.bones[1].localPosition += new Vector3(.003f, -.002f, .001f);
    left.bones[1].localRotation = Quaternion.Euler(13, 24, -45);
    f.Game.localPosition += new Vector3(-200, 500, 800);
    f.Game.localRotation = Quaternion.Euler(4, -55, 11);
    f.Ui.localPosition = new(3, -8, 5);
    f.Ui.localRotation = Quaternion.Euler(-8, 47, -13);
    f.Tick();
    Near(proxies[0].bones[1].position, f.Ui.TransformPoint(f.Game.InverseTransformPoint(left.bones[1].position)), "Animated finger and changing world/UI rigs update copied bone every frame");
    // The cached renderer basis is per-update, never retained across frames or
    // shared between independently posed glove/sleeve renderers.
    foreach (var scale in new[] { 75f, 1f, 120f })
    {
        left.transform.localPosition += new Vector3(.02f, -.01f, .03f);
        left.transform.localRotation = Quaternion.Euler(scale * .3f, scale * .2f, -scale * .1f);
        left.transform.localScale = Vector3.one * scale;
        f.Tick();
        for (var bone = 0; bone < left.bones.Length; bone++)
        {
            Near(proxies[0].bones[bone].position, f.Ui.TransformPoint(f.Game.InverseTransformPoint(left.bones[bone].position)), "Renderer basis refreshes before all animated bones after scale/rotation changes");
            foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
                Near(proxies[0].bones[bone].TransformVector(axis * .001f), f.Ui.TransformVector(f.Game.InverseTransformVector(left.bones[bone].TransformVector(axis * .001f))), "Fresh per-frame source basis preserves skinning orientation and scale");
        }
    }
    var leftCopy = proxies[0];
    left.enabled = false;
    f.Tick();
    Check(!Visible(leftCopy), "Disabled displayed glove cannot leave invisible UI hole");
    left.enabled = true;
    left.gameObject.SetActive(false);
    f.Tick();
    Check(!Visible(leftCopy), "Inactive tracked hand hides its occlusion geometry");
    left.gameObject.SetActive(true);
    f.Tick();
    Check(Visible(leftCopy), "Restored hand visibility resumes depth copy");
    var sleeve = PilotHandVisuals.Renderers[2]!;
    sleeve.sharedMaterials[0].SetFloat("_Surface", 1);
    f.Tick();
    Check(!Visible(proxies[2]), "Fading transparent sleeve does not cut an opaque hole");
    sleeve.sharedMaterials[0].SetFloat("_Surface", 0);
    f.Tick();
    Check(Visible(proxies[2]), "Fully opaque sleeve resumes matching occlusion");
    var transparent = new Material(new Shader());
    transparent.SetFloat("_Surface", 1);
    sleeve.sharedMaterials = new[] { transparent };
    f.Tick();
    Check(Proxies().Where(p => p.sharedMesh == sleeve.sharedMesh).All(p => !Visible(p)), "Replacing source material slots also notices transparent sleeve");
    sleeve.sharedMaterials = new[] { new Material(new Shader()) };
    f.Tick();
    var replacementBone = new GameObject("replacement bone").transform;
    replacementBone.SetParent(left.transform, false);
    replacementBone.localPosition = new(.001f, .005f, 0);
    // Production hand renderers assign their bones once at construction. A
    // replaced root/rig must still rebuild the cached bone mapping safely.
    left.bones = new[] { left.bones[0], replacementBone };
    left.rootBone = replacementBone;
    f.Tick();
    var current = Proxies().Single(p => p.sharedMesh == left.sharedMesh);
    Near(current.bones[1].position, f.Ui.TransformPoint(f.Game.InverseTransformPoint(replacementBone.position)), "Same-mesh root/rig replacement does not retain stale poses");
    var oldCopy = current;
    var oldMesh = left.sharedMesh;
    left.sharedMesh = new Mesh { subMeshCount = 3 };
    f.Tick();
    current = Proxies().Single(p => p.sharedMesh == left.sharedMesh);
    Check(oldCopy == null && current.sharedMaterials.Length == 3 && oldMesh != null, "Mesh replacement rebuilds only owned depth copy and keeps source mesh alive");
    left.sharedMesh = null!;
    f.Tick();
    Check(current == null && Proxies().Length == 3, "Null source mesh removes stale copy without construction failure");
    left.sharedMesh = oldMesh!;
    f.Tick();
    Check(Proxies().Length == 4, "Source mesh recovery recreates one copy");
    var missing = PilotHandVisuals.Renderers[1];
    PilotHandVisuals.Renderers[1] = null;
    f.Tick();
    Check(Proxies().Length == 3 && missing != null, "Missing source releases owned copy without destroying visible renderer");
    PilotHandVisuals.Renderers[1] = missing;
    f.Tick();
    CockpitHudWidgets.Instance!.Available = false;
    f.Tick();
    Check(Proxies().All(p => !Visible(p)), "Unavailable cockpit widgets hide all depth copies");
    CockpitHudWidgets.Instance.Available = true;
    f.Tick();
    UnityEngine.XR.XRSettings.enabled = false;
    f.Tick();
    Check(Proxies().All(p => !Visible(p)), "Leaving VR hides depth copies");
    UnityEngine.XR.XRSettings.enabled = true;
    CameraStateManager.cameraMode = CameraMode.external;
    f.Tick();
    Check(Proxies().All(p => !Visible(p)), "External view hides local cockpit occlusion");
    CameraStateManager.cameraMode = CameraMode.cockpit;
    var camera = APIBus.MainCamera;
    APIBus.MainCamera = null;
    f.Tick();
    Check(Proxies().All(p => !Visible(p)), "Lost game camera hides stale geometry");
    APIBus.MainCamera = camera;
    f.Tick();
    Invoke(f.Component, "OnDisable");
    Check(Proxies().All(p => !Visible(p)), "Component disable hides every copy");
    f.Tick();
    Check(Proxies().All(Visible), "Reenable/update restores valid displayed geometry");
    var oldUi = f.Ui;
    f.ReplaceUi();
    f.Tick();
    Check(Proxies().All(p => p.transform.parent == f.Ui), "UI rig replacement reparents existing valid copies");
    Check(oldUi.Children.Count == 1, "Old UI rig no longer retains depth copies");
    var live = Proxies();
    Invoke(f.Component, "OnDestroy");
    Check(live.All(p => p == null) && depth == null, "Destroy releases every owned proxy and its shared depth material");
    Check(PilotHandVisuals.Renderers.All(r => r != null && r.sharedMesh != null && r.sharedMaterials.All(m => m != null)), "Cleanup preserves original displayed geometry and materials");
}
foreach (var failure in new[] { "shader", "ForwardLit", "_SrcBlendAlpha", "_DstBlendAlpha", "_ZWrite", "_Surface" })
{
    using var f = new Fixture();
    f.Source(0, 100);
    if (failure == "shader")
        Shader.Available = false;
    else if (failure == "ForwardLit")
        Shader.Forward = false;
    else
        Shader.MissingProperties.Add(failure);
    var warnings = Debug.Warnings;
    f.Tick();
    f.Tick();
    Check(Proxies().Length == 0, "Missing " + failure + " leaves original rendering unchanged");
    Check(Debug.Warnings == warnings + 1, "Unavailable depth support reports once, without retry churn");
    Check(!Object.Created.OfType<Material>().Any(m => !m.Dead && m.name == "NOVR hand UI depth only"), "Unsupported material candidate is cleaned up");
}
Console.WriteLine($"{count} source-linked hand/UI depth checks passed; GPU rendering requires in-headset confirmation.");

sealed class Fixture : IDisposable
{
    internal readonly Transform Game;
    internal Transform Ui;
    internal readonly PilotHandUiDepth Component;
    internal Fixture()
    {
        Array.Clear(PilotHandVisuals.Renderers);
        Shader.Available = Shader.Forward = true;
        Shader.MissingProperties.Clear();
        CockpitHudWidgets.Instance = new();
        CameraStateManager.cameraMode = CameraMode.cockpit;
        UnityEngine.XR.XRSettings.enabled = true;
        Game = new GameObject("game rig").transform;
        Game.localPosition = new(2000, 200, -1000);
        Game.localRotation = Quaternion.Euler(-10, 67, 23);
        var camera = new GameObject("game camera").AddComponent<Camera>();
        camera.transform.SetParent(Game, false);
        APIBus.MainCamera = camera;
        Ui = new GameObject("UI rig").transform;
        ReplaceUi();
        Component = new GameObject("depth component").AddComponent<PilotHandUiDepth>();
    }
    internal void ReplaceUi()
    {
        Ui = new GameObject("UI rig").transform;
        Ui.localPosition = new(4, 3, -8);
        Ui.localRotation = Quaternion.Euler(6, -32, 12);
        var camera = new GameObject("UI camera").AddComponent<Camera>();
        camera.transform.SetParent(Ui, false);
        APIBus.CockpitHudCamera = camera;
    }
    internal void Source(int index, float modelScale)
    {
        var go = new GameObject("source " + index);
        go.transform.SetParent(Game, false);
        go.transform.localPosition = new(.1f, .2f, .3f);
        go.transform.localRotation = Quaternion.Euler(12, 47, 31);
        go.transform.localScale = Vector3.one * modelScale;
        var renderer = go.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = new Mesh { subMeshCount = 2 };
        renderer.sharedMaterials = new[] { new Material(new Shader()), new Material(new Shader()) };
        var a = new GameObject("root bone").transform;
        a.SetParent(go.transform, false);
        a.localPosition = new(.001f, .002f, .003f);
        a.localRotation = Quaternion.Euler(8, 12, 4);
        var b = new GameObject("finger bone").transform;
        b.SetParent(a, false);
        b.localPosition = new(.004f, -.003f, .002f);
        b.localRotation = Quaternion.Euler(32, -12, 28);
        b.localScale = Vector3.one * .9f;
        renderer.bones = new[] { a, b };
        renderer.rootBone = a;
        renderer.localBounds = new(new(.001f, .002f, 0), new(.01f, .02f, .005f));
        PilotHandVisuals.Renderers[index] = renderer;
    }
    internal void Tick() => typeof(PilotHandUiDepth).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(Component, null);
    public void Dispose()
    {
        typeof(PilotHandUiDepth).GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(Component, null);
        Object.Destroy(Component.gameObject);
        Object.Destroy(Game.gameObject);
        Object.Destroy(Ui.gameObject);
        Array.Clear(PilotHandVisuals.Renderers);
        APIBus.MainCamera = APIBus.CockpitHudCamera = null;
    }
}
