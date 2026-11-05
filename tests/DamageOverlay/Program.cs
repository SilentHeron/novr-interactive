using System.Reflection;
using NOVR;
using NOVR.MotionControls;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR;
using Object = UnityEngine.Object;

var checks = 0;
void Check(bool value, string message)
{
    if (!value)
        throw new InvalidOperationException(message);
    checks++;
}
bool Near(float a, float b) => MathF.Abs(a - b) < .00003f;
Camera NewCamera(float cant = 0, Vector3? translation = null, Quaternion? rotation = null)
{
    var camera = new GameObject("VR camera", typeof(Camera)).GetComponent<Camera>();
    camera.transform.localPosition = translation ?? Vector3.zero;
    camera.transform.localRotation = rotation ?? Quaternion.identity;
    camera.Projection[0] = Matrix4x4.Perspective(-.012f, .008f, -.010f, .011f);
    camera.Projection[1] = Matrix4x4.Perspective(-.008f, .012f, -.010f, .011f);
    for (var i = 0; i < 2; i++)
    {
        var side = i == 0 ? -1 : 1;
        camera.View[i] = (camera.transform.localToWorldMatrix * Matrix4x4.TRS(new(side * .032f, 0, 0),
            Quaternion.Euler(0, side * cant, 0), new(1, 1, -1))).inverse;
    }
    return camera;
}
Image Source(string name, float alpha)
{
    var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
    image.sprite = new Sprite();
    image.material = new Material();
    image.material.SetInt("_NativeBlend", 17);
    image.color = new(.9f, .8f, .7f, alpha);
    image.canvas = new GameObject(name + " native canvas", typeof(Canvas)).GetComponent<Canvas>();
    image.fillAmount = 1;
    image.fillClockwise = true;
    image.fillOrigin = 2;
    return image;
}
static void Invoke(VrDamageOverlay overlay, string name) => typeof(VrDamageOverlay)
    .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(overlay, null);
static Image Copy() => Object.Created.OfType<Image>().Last(image => !image.Dead && image.gameObject.name == "NOVR full-view damage");

var camera = NewCamera();
Check(StereoOverlayGeometry.TryFit(camera, .15f, out var center, out var size), "Valid stereo projections produce overlay coverage.");
Check(Near(center.x, 0) && Near(center.y, .0075f), "Asymmetric vertical lenses retain their actual center offset.");
Check(Near(size.x, .424f * 1.04f) && Near(size.y, .315f * 1.04f),
    "Coverage contains both eyes including their 64 mm separation, with four percent overscan.");
var canted = NewCamera(8);
Check(StereoOverlayGeometry.TryFit(canted, .15f, out var cantedCenter, out var cantedSize) && cantedSize.x > size.x,
    "Canted eye views widen the union instead of using the mono field of view.");
var moved = NewCamera(0, new(2, -3, 4), Quaternion.Euler(12, 45, -8));
Check(StereoOverlayGeometry.TryFit(moved, .15f, out var movedCenter, out var movedSize) &&
    Near(movedCenter.x, center.x) && Near(movedCenter.y, center.y) && Near(movedSize.x, size.x) && Near(movedSize.y, size.y),
    "World movement/rotation cancels out; the coverage stays headset-local.");
camera.stereoEnabled = false;
Check(!StereoOverlayGeometry.TryFit(camera, .15f, out _, out _), "A mono camera does not claim stereo coverage.");
camera.stereoEnabled = true;
Check(!StereoOverlayGeometry.TryFit(camera, 0, out _, out _) && !StereoOverlayGeometry.TryFit(camera, float.NaN, out _, out _),
    "Non-forward or invalid placements fail open.");
camera.Projection[1] = default;
Check(!StereoOverlayGeometry.TryFit(camera, .15f, out _, out _), "An invalid second eye cannot leave a one-eye-only replacement.");
camera = NewCamera();
StereoOverlayGeometry.TryFit(camera, 1f, out _, out var fullViewSize);

var source = Source("native hurt", .7f);
var sibling = Source("unrelated native UI", .4f);
SceneSingleton<GameplayUI>.i = new() { hurt = source };
APIBus.MainCamera = camera;
APIBus.CockpitHudCamera = camera;
var runtime = new VrDamageOverlay();
Invoke(runtime, "OnEnable");
try
{
    XRSettings.enabled = false;
    Invoke(runtime, "LateUpdate");
    Check(HudGraphicVisibilityLease.All.Count == 0, "Non-VR startup leaves native damage presentation untouched.");
    XRSettings.enabled = true;
    Invoke(runtime, "LateUpdate");
    var copy = Copy();
    var lease = HudGraphicVisibilityLease.All.Single();
    var rect = (RectTransform)copy.transform;
    Check(lease.Hidden && lease.Source == source && sibling.enabled && sibling.gameObject.activeSelf,
        "Only the native hurt graphic is suppressed; sibling UI remains native.");
    Check(copy.sprite == source.sprite && copy.material != source.material && copy.material.shader == source.material.shader && copy.color == source.color,
        "Native sprite, shader and exact RGBA intensity are preserved using an owned material copy.");
    Check(copy.material.Values["_NativeBlend"] == 17 && copy.material.Values["unity_GUIZTestMode"] == (int)CompareFunction.Always &&
        !source.material.Values.ContainsKey("unity_GUIZTestMode"), "Foreground depth is requested only on the owned material, preserving source properties.");
    Check(copy.fillAmount == source.fillAmount && copy.fillOrigin == source.fillOrigin && copy.fillClockwise == source.fillClockwise,
        "Native image fill behavior remains available.");
    Check(rect.parent == camera.transform && Near(rect.localPosition.z, 1f) && rect.localRotation == Quaternion.identity && rect.localScale == Vector3.one,
        "The overlay follows the headset directly, with no smoothed or aircraft-facing anchor.");
    Check(Near(rect.sizeDelta.x, fullViewSize.x) && Near(rect.sizeDelta.y, fullViewSize.y) && !copy.preserveAspect && !copy.raycastTarget && !copy.maskable,
        "The damage sprite stretches across both eye bounds without blocking pointer input or inheriting a native mask.");
    var canvas = copy.gameObject.GetComponent<Canvas>();
    Check(canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == camera && canvas.overrideSorting && canvas.sortingOrder == 32700 && LayerHelper.Assignments == 1,
        "The existing stereo HUD camera draws the isolated overlay after ordinary UI; no extra camera is created.");
    source.color = new(.8f, .7f, .6f, .23f);
    RenderPipelineManager.Render(NewCamera());
    Check(copy.color != source.color, "Unrelated camera callbacks cannot refresh or take ownership of the damage overlay.");
    source.material.SetInt("_NativeBlend", 18);
    RenderPipelineManager.Render(camera);
    Check(copy.color == source.color && source.color.a == .23f,
        "Late native alpha/color changes propagate without modifying damage timing.");
    Check(copy.material.Values["_NativeBlend"] == 18, "Late source material properties remain synchronized without mutating the source.");
    var originalMaterial = copy.material;
    source.material = new Material();
    RenderPipelineManager.Render(camera);
    Check(originalMaterial.Dead && copy.material != originalMaterial && copy.material.shader == source.material.shader && !source.material.Dead,
        "Replacing the source material disposes only the previous owned copy.");
    camera.Projection[0] = Matrix4x4.Perspective(-.020f, .008f, -.010f, .011f);
    RenderPipelineManager.Render(camera);
    Check(rect.sizeDelta.x > fullViewSize.x, "The camera render callback refits coverage from newly populated eye matrices.");
    camera.Projection[0] = Matrix4x4.Perspective(-.012f, .008f, -.010f, .011f);
    source.gameObject.SetActive(false);
    Invoke(runtime, "LateUpdate");
    Check(!copy.gameObject.activeSelf && !lease.Hidden, "Native expiry removes the VR effect and releases suppression.");
    source.gameObject.SetActive(true);
    source.canvas!.isActiveAndEnabled = false;
    Invoke(runtime, "LateUpdate");
    Check(!copy.gameObject.activeSelf && !lease.Hidden, "A hidden native gameplay canvas does not leak the effect over menus.");
    source.canvas.isActiveAndEnabled = true;
    Invoke(runtime, "LateUpdate");
    TrackedControllers.Available = false;
    RenderPipelineManager.Render(camera);
    Check(!copy.gameObject.activeSelf && !lease.Hidden, "Tracking/focus loss restores native presentation instead of freezing a panel in space.");
    TrackedControllers.Available = true;
    Invoke(runtime, "LateUpdate");
    XRSettings.enabled = false;
    Invoke(runtime, "LateUpdate");
    Check(!copy.gameObject.activeSelf && !lease.Hidden, "Disabling XR restores native presentation.");
    XRSettings.enabled = true;
    camera.Projection[1] = default;
    Invoke(runtime, "LateUpdate");
    Check(!copy.gameObject.activeSelf && !lease.Hidden, "A missing eye projection cannot suppress the only visible damage source.");
    camera = NewCamera();
    camera.stereoEnabled = false; // An overlay camera's query state can lag outside its render callback.
    APIBus.MainCamera = NewCamera(0, new(10, 3, -5), Quaternion.Euler(5, 30, 0));
    APIBus.CockpitHudCamera = camera;
    Invoke(runtime, "LateUpdate");
    Check(Near(rect.sizeDelta.x, fullViewSize.x) && Near(rect.sizeDelta.y, fullViewSize.y),
        "Coverage uses the tracked base camera and transfers only head-local bounds into the HUD space.");
    Check(copy.transform.parent == camera.transform && canvas.worldCamera == camera && lease.Hidden,
        "Replacing the HUD camera rebinds the existing effect in the new tracking space.");
    Object.Destroy(camera.gameObject);
    camera = NewCamera();
    APIBus.CockpitHudCamera = camera;
    Invoke(runtime, "LateUpdate");
    Check(copy.Dead && !lease.Hidden && Copy() != copy && HudGraphicVisibilityLease.All.Last().Hidden,
        "Destroyed camera children release their old source lease before recreation.");
    copy = Copy();
    lease = HudGraphicVisibilityLease.All.Last();
    var next = Source("replacement native hurt", .45f);
    var oldMaterial = copy.material;
    SceneSingleton<GameplayUI>.i.hurt = next;
    Invoke(runtime, "LateUpdate");
    Check(copy.Dead && oldMaterial.Dead && !lease.Hidden && Copy().sprite == next.sprite && HudGraphicVisibilityLease.All.Last().Source == next,
        "A new native GameplayUI/hurt source releases the previous clone and borrows only the new artwork.");
    copy = Copy();
    var finalMaterial = copy.material;
    Invoke(runtime, "OnDisable");
    Check(copy.Dead && finalMaterial.Dead && HudGraphicVisibilityLease.All.All(item => !item.Hidden) && RenderPipelineManager.Subscribers == 0,
        "Component disable destroys owned UI/materials, restores every source and unsubscribes the camera callback.");
    Check(!source.Dead && !next.Dead && !source.sprite!.Dead && !next.material!.Dead,
        "Native images, sprites and materials are never destroyed.");
}
finally
{
    Invoke(runtime, "OnDisable");
    Invoke(runtime, "OnDestroy");
}
Check(RenderPipelineManager.Subscribers == 0, "Repeated disable/destroy remains harmless.");
Console.WriteLine($"PASS {checks} damage-overlay checks (production stereo geometry and lifecycle; no Unity rendering).");
