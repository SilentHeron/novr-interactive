using System.Reflection;
using NOVR.VrCamera;
using UnityEngine;
using UnityEngine.Rendering.Universal;
var checks = 0;
void Check([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool value, string name) { if (!value) throw new Exception(name); checks++; }
var manager = new GameObject("manager").AddComponent<VrCameraManager>();
var update = (Action)Delegate.CreateDelegate(typeof(Action), manager, typeof(VrCameraManager).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)!);
var handle = typeof(VrCameraManager).GetMethod("HandleChildCameras", BindingFlags.NonPublic | BindingFlags.Instance)!;
Camera Make(string name, Transform? parent = null)
{
    var go = new GameObject(name);
    if (parent != null)
        go.transform.SetParent(parent, false);
    return go.AddComponent<Camera>();
}
Camera[] Buffer() => (Camera[])typeof(VrCameraManager).GetField("_cameraBuffer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(manager)!;
var dead = Make("old menu");
dead.Destroyed = true;
var live = Make("ignored instrument");
live.enabled = false;
VrCameraManager.IgnoredCameras.Add(dead);
VrCameraManager.IgnoredCameras.Add(live);
update();
Check(!VrCameraManager.IgnoredCameras.Contains(dead), "Scene-destroyed wrapper is released");
Check(VrCameraManager.IgnoredCameras.Contains(live), "Live disabled camera ownership remains");
Check(VrCameraManager.IgnoredCameras.Count == 1, "Pruning does not reset unrelated registry ownership");
for (var i = 0; i < 100; i++) { var c = Make("old scene"); VrCameraManager.IgnoredCameras.Add(c); c.Destroyed = true; update(); }
Check(VrCameraManager.IgnoredCameras.Count == 1, "Repeated scenes cannot grow stale ignore entries");

var parent = Make("test parent");
var child = Make("child", parent.transform);
handle.Invoke(manager, new object[] { parent });
handle.Invoke(manager, new object[] { parent });
Check(child.gameObject.Count<StereoCamera>() == 1, "Repeated child discovery wraps only once");
Check(parent.gameObject.Count<StereoCamera>() == 0, "Parent is not wrapped as its own child");
var trackedChild = Make("already tracked", parent.transform);
trackedChild.gameObject.AddComponent<VrCamera>();
handle.Invoke(manager, new object[] { parent });
Check(trackedChild.gameObject.Count<StereoCamera>() == 1, "Derived VR camera already satisfies stereo wrapping");
var ignored = Make("externally ignored", parent.transform);
VrCameraManager.IgnoredCameras.Add(ignored);
handle.Invoke(manager, new object[] { parent });
Check(ignored.gameObject.Count<StereoCamera>() == 0, "Live ignored child remains untouched");
child.GetComponent<StereoCamera>()!.Destroyed = true;
handle.Invoke(manager, new object[] { parent });
Check(child.gameObject.Count<StereoCamera>() == 1, "Destroyed child wrapper can be replaced");

var main = Make("Main Camera");
main.tag = "MainCamera";
main.fieldOfView = 75;
var originalData = main.gameObject.AddComponent<UniversalAdditionalCameraData>();
originalData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
originalData.antialiasingQuality = AntialiasingQuality.Medium;
var overlay = Make("native overlay");
originalData.cameraStack.Add(overlay);
var listener = main.gameObject.AddComponent<AudioListener>();
var cockpit = Make("cockpitRenderer", main.transform);
cockpit.gameObject.AddComponent<StereoCamera>();
update();
var tracked = VrCameraManager.GetTrackedMainCamera(main.gameObject)!;
Check(tracked != null && !main.enabled && tracked.enabled, "Main root transfers rendering to one tracked camera");
Check(tracked.fieldOfView == 75 && tracked.tag == "MainCamera" && main.tag == "Untagged", "Native camera settings and main ownership preserved");
Check(tracked.GetComponent<UniversalAdditionalCameraData>()!.cameraStack.Single() == overlay, "Native overlay stack is copied intact");
var trackedData = tracked.GetComponent<UniversalAdditionalCameraData>()!;
Check(trackedData.antialiasing == AntialiasingMode.None,
    "By default the tracked view skips the native camera's costly post-process anti-aliasing");
NOVR.ModConfiguration.Instance.VrGameAntialiasing.Value = true;
update();
Check(trackedData.antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing && trackedData.antialiasingQuality == AntialiasingQuality.Medium,
    "Opting in applies the native camera's post-process anti-aliasing live (stock SMAA Medium)");
originalData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
update();
Check(trackedData.antialiasing == AntialiasingMode.FastApproximateAntialiasing, "While opted in, a native change to the base camera's anti-aliasing is followed");
NOVR.ModConfiguration.Instance.VrGameAntialiasing.Value = false;
update();
Check(trackedData.antialiasing == AntialiasingMode.None, "Opting out removes post-process anti-aliasing live");
originalData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
update();
// Dithering is the stack base camera's setting too. The game asks for none.
Check(!originalData.dithering && trackedData.dithering, "The tracked view dithers by default although the native camera does not");
NOVR.ModConfiguration.Instance.ImageQuality.DitherGradients.Value = false;
update();
Check(!trackedData.dithering && !originalData.dithering, "Switching dithering off applies live and never touches the native camera");
originalData.dithering = true;
update();
Check(trackedData.dithering, "A native camera that asks for dithering itself is followed even with the option off");
originalData.dithering = false;
NOVR.ModConfiguration.Instance.ImageQuality.DitherGradients.Value = true;
update();
Check(trackedData.dithering && trackedData.antialiasing == AntialiasingMode.None, "Dithering returns with the option and is independent of anti-aliasing");
Check(!listener.enabled && tracked.GetComponent<AudioListener>()!.enabled, "Audio listener moves to tracked view");
Check(ReferenceEquals(cockpit.transform.parent, tracked.transform), "Tracked cockpit subtree is reparented");
Check(cockpit.gameObject.Count<StereoCamera>() == 1, "Existing cockpit stereo wrapper is not duplicated by setup");
var creations = Camera.Creations;
for (var i = 0; i < 20; i++) { main.enabled = true; update(); }
Check(Camera.Creations == creations && ReferenceEquals(tracked, VrCameraManager.GetTrackedMainCamera(main.gameObject)), "Native root re-enables reuse the same tracked rig");
Check(tracked.gameObject.Count<VrCamera>() == 1, "Rig maintenance does not duplicate tracking behaviour");
Check(tracked.gameObject.Count<VrCockpitNearClip>() == 1, "Tracked main view owns exactly one cockpit near-plane lease");
Check(tracked.gameObject.Count<VrFarClip>() == 1, "Tracked main view owns exactly one far-plane lease");
Check(VrCameraManager.IgnoredCameras.Contains(main) && VrCameraManager.IgnoredCameras.Contains(tracked), "Both live main-camera owners remain registered");

var menu = Make("Menu Camera");
update();
update();
Check(menu.gameObject.Count<VrCamera>() == 1, "Stable menu camera receives one tracking wrapper");
Check(menu.gameObject.Count<VrCockpitNearClip>() == 0 && cockpit.gameObject.Count<VrCockpitNearClip>() == 0 &&
      menu.gameObject.Count<VrFarClip>() == 0 && cockpit.gameObject.Count<VrFarClip>() == 0,
    "Menu and native cockpit overlay cameras retain their original clipping behaviour");
menu.Destroyed = true;
update();
Check(!VrCameraManager.IgnoredCameras.Contains(menu), "Destroyed menu is released without a UI manager callback");
Check(!Buffer().Any(c => ReferenceEquals(c, menu)), "Shrinking camera buffer clears old scene references");
for (var i = 0; i < 1000; i++) update();
var before = GC.GetAllocatedBytesForCurrentThread();
for (var i = 0; i < 10000; i++) update();
Check(GC.GetAllocatedBytesForCurrentThread() == before, "Stable source discovery and registry pruning allocate no managed memory in boundary fixture");
checks += NearClipChecks.Run(tracked, main);
checks += FarClipChecks.Run(tracked, main);
Console.WriteLine($"{checks} source-linked camera manager lifecycle checks passed (Unity boundary doubles; no GPU claim).");
