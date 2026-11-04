using System.Reflection;
using NOVR;
using NOVR.VrUi;
using NOVR.VrCamera;
using UnityEngine;
using UnityEngine.Rendering.Universal;

var checks = 0;
void Check([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool value, string message) { if (!value) throw new Exception(message); checks++; }
void Invoke(object component, string name) => component.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(component, null);
APIBus Create()
{
    var bus = (APIBus)Activator.CreateInstance(typeof(APIBus), nonPublic: true)!;
    Invoke(bus, "Awake");
    Invoke(bus, "OnEnable");
    return bus;
}
void Reset()
{
    APIBus.OnMainCameraChanged = null;
    typeof(APIBus).GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, null);
    typeof(APIBus).GetField("_previousMainCamera", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, null);
    Camera.main = null;
    Debug.Logs = Debug.Warnings = Debug.Errors = Debug.Exceptions = 0;
}
void Disable(APIBus bus) { bus.enabled = false; Invoke(bus, "OnDisable"); }
void Destroy(APIBus bus) { Disable(bus); Invoke(bus, "OnDestroy"); bus.Destroyed = true; }
Reset();
var owner = Create();
var first = new Camera();
Camera.main = first;
owner.Update();
Check(ReferenceEquals(APIBus.MainCamera, first), "First camera is cached with no subscribers and no exception");
for (var i = 0; i < 5; i++) owner.Update();
Check(ReferenceEquals(APIBus.MainCamera, first), "Stable camera requires no subscriber");
var changes = new List<(Camera? Previous, Camera? Next)>();
void Record(Camera? oldCamera, Camera? camera) { Check(ReferenceEquals(APIBus.MainCamera, camera), "Subscribers observe already-published current camera"); changes.Add((oldCamera, camera)); }
APIBus.OnMainCameraChanged += Record;
var second = new Camera();
Camera.main = second;
owner.Update();
owner.Update();
Check(changes.Count == 1 && ReferenceEquals(changes[0].Previous, first) && ReferenceEquals(changes[0].Next, second), "A live-to-live change dispatches once");
second.Destroyed = true;
Camera.main = null;
owner.Update();
owner.Update();
Check(changes.Count == 2 && changes[1].Next is null && APIBus.MainCamera is null, "Destroyed-to-null transition publishes genuine CLR null exactly once");
var third = new Camera();
Camera.main = third;
owner.Update();
Check(changes.Count == 3 && changes[2].Previous is null && ReferenceEquals(changes[2].Next, third), "Camera recovery dispatches after loss");
APIBus.OnMainCameraChanged -= Record;
Camera.main = new Camera();
owner.Update();
Check(changes.Count == 3, "Unsubscribe is respected");

Reset();
owner = Create();
first = new Camera();
Camera.main = first;
owner.Update();
var duplicate = Create();
var notifications = 0;
APIBus.OnMainCameraChanged += (a, b) => notifications++;
second = new Camera();
Camera.main = second;
duplicate.Update();
Check(ReferenceEquals(APIBus.MainCamera, first) && notifications == 0, "A second live dispatcher does not steal ownership");
owner.Update();
Check(ReferenceEquals(APIBus.MainCamera, second) && notifications == 1, "Original owner remains functional after duplicate appears");
var diagnostics = Debug.Warnings + Debug.Errors;
for (var i = 0; i < 10; i++) { duplicate.Update(); owner.Update(); }
Check(diagnostics <= 1 && Debug.Warnings + Debug.Errors == diagnostics, "Waiting duplicate does not log each frame");
Disable(owner);
duplicate.Update();
Camera.main = third = new Camera();
duplicate.Update();
Check(ReferenceEquals(APIBus.MainCamera, third), "Waiting dispatcher takes over when owner is disabled");
owner.enabled = true;
Invoke(owner, "OnEnable");
Camera.main = first;
owner.Update();
Check(ReferenceEquals(APIBus.MainCamera, third), "Reactivated previous owner cannot steal a live replacement");
duplicate.Update();
Check(ReferenceEquals(APIBus.MainCamera, first), "Replacement continues publishing after old owner reactivates");
Destroy(owner);
Camera.main = second;
duplicate.Update();
Check(ReferenceEquals(APIBus.MainCamera, second), "Destroying non-owner cannot clear replacement state");
Destroy(duplicate);
var replacement = Create();
replacement.Update();
Check(ReferenceEquals(APIBus.MainCamera, second), "Destroyed owner can be replaced without invalid-state exception");

Reset();
owner = Create();
Camera.main = first;
owner.Update();
// Reproduce a Unity-null owner without its cleanup callback, as can occur when
// stale static fields outlive native scene objects.
owner.Destroyed = true;
replacement = Create();
Camera.main = second;
replacement.Update();
Check(ReferenceEquals(APIBus.MainCamera, second), "Unity-null stale owner is claimable even when its CLR reference survives");
var calls = 0;
Action<Camera?, Camera?> failing = (a, b) => { calls++; throw new InvalidOperationException("subscriber failure"); };
APIBus.OnMainCameraChanged += failing;
Camera.main = third;
try { replacement.Update(); } catch (InvalidOperationException) { }
Check(ReferenceEquals(APIBus.MainCamera, third), "Subscriber exception cannot prevent caching the new camera");
replacement.Update();
Check(calls == 1, "Subscriber exception is not replayed every stable frame");
APIBus.OnMainCameraChanged -= failing;
for (var i = 0; i < 1000; i++) replacement.Update();
var before = GC.GetAllocatedBytesForCurrentThread();
for (var i = 0; i < 10000; i++) replacement.Update();
var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
Check(allocated == 0, "Stable dispatch path has no managed allocation");
Reset();
Camera SceneCamera(string name)
{
    var go = new GameObject(name);
    go.AddComponent<UniversalAdditionalCameraData>();
    return go.AddComponent<Camera>();
}
NOUIManager CreateUi()
{
    var ui = new GameObject("UI manager").AddComponent<NOUIManager>();
    Invoke(ui, "Awake");
    Invoke(ui, "Start");
    return ui;
}
void DestroyUi(NOUIManager ui) { Invoke(ui, "OnDestroy"); ui.Destroyed = true; }
List<Camera> Stack(Camera camera) => camera.GetComponent<UniversalAdditionalCameraData>()!.cameraStack;
Check(APIBus.CockpitHudCamera is null && APIBus.CockpitHudReference is null, "Public HUD getters are safe before the manager exists");
owner = Create();
Camera.main = first = SceneCamera("first");
owner.Update();
var unrelated = SceneCamera("native overlay");
Stack(first).Add(unrelated);
var manager = CreateUi();
var hud = manager.CockpitHudCamera;
Check(Stack(first).Count(x => ReferenceEquals(x, hud)) == 1, "Late UI manager attaches to an already-published camera");
Check(VrCameraManager.IgnoredCameras.Contains(hud), "Owned HUD camera is excluded from world camera scanning");
APIBus.OnMainCameraChanged?.Invoke(first, first);
Check(Stack(first).Count(x => ReferenceEquals(x, hud)) == 1, "Repeated camera resync does not duplicate the overlay");
Camera.main = second = SceneCamera("second");
owner.Update();
Check(Stack(first).Count == 1 && ReferenceEquals(Stack(first)[0], unrelated), "Camera transition removes only the owned overlay from the old stack");
Check(Stack(second).Count == 1 && ReferenceEquals(Stack(second)[0], hud), "Camera transition attaches the same owned overlay to the new stack");
var lostHud = hud;
lostHud.Destroyed = true;
hud = manager.CockpitHudCamera;
Check(!ReferenceEquals(hud, lostHud) && hud != null, "A destroyed native HUD camera is recreated even while its wrapper survives");
Check(!VrCameraManager.IgnoredCameras.Contains(lostHud) && VrCameraManager.IgnoredCameras.Contains(hud), "Camera recovery replaces only its owned ignore-set entry");
Check(Stack(second).Count == 1 && ReferenceEquals(Stack(second)[0], hud), "Camera recovery replaces the stale stack entry without a main-camera event");
Check(ReferenceEquals(hud, manager.CockpitHudCamera), "Repeated HUD access reuses the recovered camera");
var lostReference = manager.CockpitHudReference;
lostReference.Destroyed = true;
Check(!ReferenceEquals(lostReference, manager.CockpitHudReference) && manager.CockpitHudReference != null, "Destroyed forward reference is recreated without retaining its Unity-null wrapper");
manager.SetStartupPresentation(true);
var startup = (Camera)typeof(NOUIManager).GetField("_startupCamera", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
Check(VrCameraManager.IgnoredCameras.Contains(startup), "Startup camera is registered independently");
var lostStartup = startup;
lostStartup.Destroyed = true;
manager.SetStartupPresentation(true);
startup = (Camera)typeof(NOUIManager).GetField("_startupCamera", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
Check(!ReferenceEquals(startup, lostStartup) && !VrCameraManager.IgnoredCameras.Contains(lostStartup) && VrCameraManager.IgnoredCameras.Contains(startup), "Startup recovery replaces the destroyed camera's ignore entry");
manager.SetStartupPresentation(false);
Check(!startup.enabled && !startup.gameObject.activeSelf, "Startup camera is hidden without being recreated");
var replacementUi = CreateUi();
var nextHud = replacementUi.CockpitHudCamera;
Check(Stack(second).Contains(nextHud), "Replacement UI manager attaches without a new main-camera event");
hud.Destroyed = true;
startup.Destroyed = true;
DestroyUi(manager);
Check(!VrCameraManager.IgnoredCameras.Contains(hud) && !VrCameraManager.IgnoredCameras.Contains(startup), "Teardown removes owned destroyed wrappers from the ignored set");
Check(ReferenceEquals(NOUIManager.I, replacementUi), "Old manager teardown cannot clear the replacement singleton");
Check(Stack(second).Count == 1 && ReferenceEquals(Stack(second)[0], nextHud), "Old manager teardown removes only its own stack entry");
Camera.main = third = SceneCamera("third");
owner.Update();
Check(Stack(third).Count == 1 && ReferenceEquals(Stack(third)[0], nextHud), "Destroyed manager unsubscribes instead of reattaching its old camera");
DestroyUi(replacementUi);
Check(Stack(third).Count == 0 && VrCameraManager.IgnoredCameras.Count == 0, "Final teardown releases all owned stack and ignore references");
Check(NOUIManager.I is null && APIBus.CockpitHudCamera is null && APIBus.CockpitHudReference is null, "Final teardown clears HUD access without reviving destroyed objects");

VrCamera Stereo(Camera camera)
{
    var vr = camera.gameObject.AddComponent<VrCamera>();
    Invoke(vr, "Awake");
    Invoke(vr, "OnEnable");
    return vr;
}
var missingCamera = new GameObject("no camera").AddComponent<VrCamera>();
Invoke(missingCamera, "Awake");
Invoke(missingCamera, "Update");
Check(VrCamera.HighestDepthVrCamera is null, "A VR component without a camera cannot become the active reference");
var lowCamera = SceneCamera("low depth");
lowCamera.depth = 1;
var low = Stereo(lowCamera);
Invoke(low, "Update");
Check(ReferenceEquals(low.ParentCamera, lowCamera) && ReferenceEquals(low.CameraInUse, lowCamera), "StereoCamera uses the existing camera instead of creating a duplicate");
Check(ReferenceEquals(VrCamera.HighestDepthVrCamera, low), "First available camera becomes the reference");
var highCamera = SceneCamera("high depth");
highCamera.depth = 10;
var high = Stereo(highCamera);
highCamera.enabled = false;
Invoke(high, "Update");
Check(ReferenceEquals(VrCamera.HighestDepthVrCamera, low), "Disabled higher-depth cameras cannot displace the live reference");
highCamera.enabled = true;
Invoke(high, "Update");
Invoke(low, "Update");
Check(ReferenceEquals(VrCamera.HighestDepthVrCamera, high), "An enabled higher-depth camera takes precedence and is stable");
highCamera.enabled = false;
Invoke(low, "Update");
Check(ReferenceEquals(VrCamera.HighestDepthVrCamera, low), "Disabling only the selected Camera refreshes the reference on the next live camera update");
var renderSubscribers = Application.Subscribers;
var configSubscribers = ModConfiguration.Instance.Config.Subscribers;
highCamera.enabled = true;
Invoke(high, "Update");
high.enabled = false;
Invoke(high, "OnDisable");
Check(VrCamera.HighestDepthVrCamera is null, "Disabling the selected VR behaviour clears its static reference");
Check(Application.Subscribers == renderSubscribers - 1 && ModConfiguration.Instance.Config.Subscribers == configSubscribers - 1, "VR behaviour disable releases its real inherited render/config subscriptions");
Invoke(low, "Update");
Check(ReferenceEquals(VrCamera.HighestDepthVrCamera, low), "The remaining camera becomes reference after selected behaviour disable");
var inactiveCamera = SceneCamera("inactive camera");
inactiveCamera.depth = 100;
inactiveCamera.activeInHierarchy = false;
var inactive = Stereo(inactiveCamera);
Invoke(inactive, "Update");
Check(ReferenceEquals(VrCamera.HighestDepthVrCamera, low), "Inactive camera objects cannot become reference");
lowCamera.Destroyed = true;
var recoveryCamera = SceneCamera("camera recovery");
recoveryCamera.depth = 0;
var recovery = Stereo(recoveryCamera);
Invoke(recovery, "Update");
Check(ReferenceEquals(VrCamera.HighestDepthVrCamera, recovery), "Destroyed selected cameras do not block lower-depth recovery");
renderSubscribers = Application.Subscribers;
configSubscribers = ModConfiguration.Instance.Config.Subscribers;
Invoke(low, "OnDisable");
Check(ReferenceEquals(VrCamera.HighestDepthVrCamera, recovery) && Application.Subscribers == renderSubscribers - 1 && ModConfiguration.Instance.Config.Subscribers == configSubscribers - 1, "Nonselected camera cleanup releases callbacks and leaves the replacement reference intact");
Invoke(recovery, "OnDisable");
Invoke(inactive, "OnDisable");
Reset();
// Exercise the real canvas component against real manager recreation rather
// than assuming its first Awake always runs after the HUD camera exists.
var uiObject = new GameObject("late camera canvas");
var canvas = uiObject.AddComponent<Canvas>();
uiObject.transform.localPosition = new Vector3(3, 4, 9);
var canvasBehavior = uiObject.AddComponent<NOVR.VrUi.SpecialBehavior.UIRenderedCanvasBehavior>();
canvasBehavior.Awake();
canvasBehavior.OnEnable();
Check(canvas.worldCamera is null && canvas.renderMode == RenderMode.WorldSpace, "Canvas can initialize before its camera exists");
Check(uiObject.transform.localPosition == new Vector3(3, 4, 0), "One-time initialization preserves horizontal/vertical layout");
owner = Create();
Camera.main = SceneCamera("late menu");
owner.Update();
manager = CreateUi();
hud = manager.CockpitHudCamera;
Invoke(canvasBehavior, "Update");
Check(ReferenceEquals(canvas.worldCamera, hud), "Already-initialized canvas binds a late UI camera");
uiObject.transform.localPosition = new Vector3(6, 7, 8);
var layerWrites = LayerHelper.LayerAssignments;
var cameraWrites = canvas.CameraAssignments;
for (var i = 0; i < 1000; i++) Invoke(canvasBehavior, "Update");
Check(canvas.CameraAssignments == cameraWrites && LayerHelper.LayerAssignments == layerWrites, "Stable camera binding avoids repeated property and layer writes");
Check(uiObject.transform.localPosition == new Vector3(6, 7, 8), "Camera maintenance does not reset relocated canvas geometry");
hud.Destroyed = true;
Invoke(canvasBehavior, "Update");
Check(canvas.worldCamera != null && !ReferenceEquals(canvas.worldCamera, hud), "Destroyed UI camera is rebound without re-enabling the canvas");
DestroyUi(manager);
Invoke(canvasBehavior, "Update");
Check(canvas.worldCamera is null, "Manager loss clears the stale camera reference");
manager = CreateUi();
Invoke(canvasBehavior, "Update");
Check(ReferenceEquals(canvas.worldCamera, manager.CockpitHudCamera), "Replacement UI manager rebinds existing canvas");
canvasBehavior.OnEnable();
Check(uiObject.transform.localPosition == new Vector3(6, 7, 8), "Re-enabling canvas retains user placement");
var noCanvas = new GameObject("non-canvas subtree").AddComponent<NOVR.VrUi.SpecialBehavior.UIRenderedCanvasBehavior>();
noCanvas.Awake();
Invoke(noCanvas, "Update");
Check(noCanvas.GetComponent<Canvas>() == null, "Non-canvas subtree does not create unnecessary canvas components");
foreach (var type in new[] { typeof(NOVR.VrUi.SpecialBehavior.NOVRGameplayUIBehaviour), typeof(NOVR.VrUi.SpecialBehavior.NoVrHudBehavior) })
{
    var go = new GameObject(type.Name);
    var nativeCanvas = go.AddComponent<Canvas>();
    var behavior = (NOVR.VrUi.SpecialBehavior.UIRenderedCanvasBehavior)Activator.CreateInstance(type)!;
    behavior.gameObject = go;
    behavior.Awake();
    manager.CockpitHudCamera.Destroyed = true;
    Invoke(behavior, "Update");
    Check(ReferenceEquals(nativeCanvas.worldCamera, manager.CockpitHudCamera), "Derived position update preserves camera rebinding: " + type.Name);
}
DestroyUi(manager);
Reset();
Console.WriteLine($"{checks} source-linked camera bus and UI lifecycle checks passed.");
HeadsetChecks.Run();
