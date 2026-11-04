using NOVR;
using NOVR.MotionControls;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
void Near(float actual, float expected, string message) => Check(Math.Abs(actual - expected) < .0001f, $"{message}: {actual} != {expected}");

// All coordinates are observed through the real ControllerUiPointer dispatch.
// Controller and camera origins differ, making wrong-depth screen projection
// measurably wrong rather than accidentally passing a centre-origin fixture.
{
    var s = new Scene();
    s.Frame(0); s.Frame(1);
    Check(s.Slider.Downs == 1 && s.Slider.Begins == 1, "A trigger press captures the actual UGUI slider");
    s.Aim(false, new(.2f, .8f, 1)); s.Frame(1);
    Near(s.Slider.Value, .7f, "A different-depth background does not shift the captured slider value");
    Check(s.Pointer.HitPosition.z == 1, "Captured drag laser terminates at its original control plane");
    s.Background.gameObject.activeSelf = false;
    s.Aim(false, new(.1f, .8f, 1)); s.Frame(1);
    Near(s.Slider.Value, .6f, "Dragging into empty space uses the control plane rather than the 5m fallback");
    s.Right.AimRotation = Quaternion.Forward(new(1, 0, 0)); s.Frame(1);
    Near(s.Slider.Value, .6f, "Parallel rays retain the last usable surface point");
    s.Right.AimRotation = Quaternion.Forward(new(0, 0, -1)); s.Frame(1);
    Near(s.Slider.Value, .6f, "Backward rays retain the last usable surface point");
    s.Slider.transform.position = new(.3f, 0, 1.4f);
    s.Aim(false, new(.5f, .8f, 1.4f)); s.Frame(1);
    Near(s.Slider.Value, .7f, "A repositioned menu keeps its captured local surface coordinates");
    s.Frame(0);
    Check(s.Slider.Ends == 1 && !s.Pointer.Held, "Release ends drag once");
}

foreach (var loss in new[] { "hidden", "disabled", "noninteractable", "destroyed" })
{
    var s = new Scene(); s.Frame(0); s.Frame(1);
    var drags = s.Slider.Drags;
    if (loss == "hidden") s.Slider.gameObject.activeSelf = false;
    if (loss == "disabled") s.Slider.enabled = false;
    if (loss == "noninteractable") s.Slider.interactable = false;
    if (loss == "destroyed") s.Slider.gameObject.Destroyed = true;
    s.Frame(1);
    Check(!s.Pointer.Held && s.Slider.Drags == drags, $"{loss} control cancels capture");
    s.Slider.gameObject.activeSelf = s.Slider.enabled = s.Slider.interactable = true;
    s.Slider.gameObject.Destroyed = false;
    s.Frame(1);
    Check(!s.Pointer.Held && s.Slider.Downs == 1, $"{loss} control cannot reacquire a still-held trigger");
    s.Frame(0); s.Frame(1);
    Check(s.Slider.Downs == 2, $"{loss} control can be pressed after release");
}

{
    var s = new Scene(); s.Frame(0); s.Frame(1);
    EventSystem.current = new GameObject("replacement event system").AddComponent<EventSystem>();
    s.Frame(1);
    Check(!s.Pointer.Held && s.Slider.Ends == 1, "Changing EventSystem cancels the old gesture");
    s.Frame(1); Check(s.Slider.Downs == 1, "New EventSystem cannot replay a held trigger");
    s.Frame(0); s.Frame(1); Check(s.Slider.Downs == 2, "New EventSystem accepts a fresh press");
}

{
    var s = new Scene();
    s.Slider.Destroyed = true;
    var toggle = s.Slider.gameObject.AddComponent<TestToggle>();
    s.Frame(0); s.Frame(1); s.Frame(1); s.Frame(0);
    Check(toggle.Clicks == 1 && toggle.Value, "Toggle fires one click on release");
    s.Frame(1); s.Pointer.Cancel(); s.Frame(1); s.Frame(0);
    Check(toggle.Clicks == 1, "Focus cancellation never releases a click");
}

// Native full map owns its own pointer/world coordinate behavior. Its drag is
// deliberately not forced onto a UGUI slider plane by this fix.
{
    var s = new Scene();
    var map = s.Slider.gameObject.AddComponent<DynamicMap>();
    map.mapBackground = s.Slider.gameObject.GetComponent<Image>();
    SceneSingleton<DynamicMap>.i = map; DynamicMap.mapMaximized = true;
    s.Frame(0); s.Frame(1); s.Aim(false, new(.2f, .8f, 1)); s.Frame(1);
    Check(s.Slider.Drags >= 2 && s.Pointer.HitPosition.z > 2, "Native maximized map keeps its existing drag coordinate path");
}

{
    var s = new Scene(); s.EnableLeft(); s.Frame(0);
    Check(s.Pointer.Ray.origin.x > 0, "Pointer initially belongs to the right hand");
    for (var i = 0; i < 60; i++)
    {
        s.Aim(false, new(i % 2 == 0 ? 2 : 0, 0, 1));
        s.Aim(true, new(i % 2 == 0 ? 0 : -2, 0, 1));
        s.Left.Axis = new(.9f, .8f); s.Right.Axis = new(-.9f, -.8f); s.Frame(0);
        Check(s.Pointer.Ray.origin.x > 0, "Hover, distance and noisy axes never transfer ownership");
    }
    s.Left.Primary = true; s.Frame(0);
    Check(s.Pointer.Ray.origin.x < 0 && s.Slider.Downs == 0, "Other-hand face button chooses pointer without producing a UI click");
    s.Right.Secondary = true; s.Frame(0);
    Check(s.Pointer.Ray.origin.x > 0, "Fresh other-hand button switches back");
    for (var i = 0; i < 10; i++) { s.Frame(0); Check(s.Pointer.Ray.origin.x > 0, "A held face button cannot switch ownership repeatedly"); }
    s.Left.Primary = false; s.Frame(0); s.Left.Primary = true; s.Frame(0);
    Check(s.Pointer.Ray.origin.x < 0, "Release and press explicitly changes ownership again");
    s.Pointer.Cancel(); s.Frame(0);
    Check(s.Pointer.Ray.origin.x < 0, "Menu/focus cancellation retains the last deliberately selected hand");
}

// Every sampled physical button participates, including Frame's extra face
// buttons; contact-only sensors and thumbstick motion are intentionally absent.
foreach (var button in new[] { "Primary", "Secondary", "Menu", "Shoulder", "Top", "Outside", "StickClick", "Grip", "Trigger" })
{
    var s = new Scene(); s.EnableLeft(); s.Frame(0);
    object boxed = s.Left;
    typeof(ControllerSample).GetField(button)!.SetValue(boxed, button is "Grip" or "Trigger" ? (object)1f : true);
    s.Left = (ControllerSample)boxed; s.Frame(0);
    Check(s.Pointer.Ray.origin.x < 0, $"Fresh {button} selects the other hand");
}

{
    var s = new Scene(); s.EnableLeft(); s.Frame(0); s.Frame(1);
    s.Left.Primary = true; s.Left.Trigger = 1; s.Frame(1);
    Check(s.Pointer.Ray.origin.x > 0 && s.Slider.Downs == 1, "Other-hand input cannot steal an active drag");
    s.Left.Secondary = true; s.Frame(0);
    Check(s.Pointer.Ray.origin.x > 0 && s.Slider.Ends == 1, "The release frame remains owned by the drag hand");
    s.Frame(0); Check(s.Pointer.Ray.origin.x > 0, "Ignored input during capture does not queue a later hand switch");
    s.Left.Trigger = 0; s.Left.Primary = s.Left.Secondary = false; s.Frame(0);
    s.Left.Trigger = 1; s.Frame(0);
    Check(s.Pointer.Ray.origin.x < 0 && s.Slider.Downs == 2, "A fresh trigger both selects the other hand and presses once");
}

{
    var s = new Scene(); s.EnableLeft(); s.Frame(0); s.Frame(1);
    s.Right.Valid = false; Check(!s.Frame(1), "Losing the owner hides its pointer rather than choosing a hovering hand");
    Check(s.Slider.Ends == 1 && !s.Pointer.Held, "Tracking loss cancels the gesture without clicking");
    for (var i = 0; i < 5; i++) Check(!s.Frame(1), "Available idle hand never takes over on its own");
    s.Left.Primary = true; Check(s.Frame(1) && s.Pointer.Ray.origin.x < 0, "Deliberate other-hand input works while owner tracking is lost");
    s.Right.Valid = true; s.Frame(1);
    Check(s.Pointer.Ray.origin.x < 0 && !s.Pointer.Pressed, "Returning tracking with a held trigger cannot reclaim the pointer");
    s.Right.Trigger = 0; s.Frame(0); s.Frame(1);
    Check(s.Pointer.Ray.origin.x > 0 && s.Pointer.Pressed, "Returned hand can take over after release and fresh press");
}

foreach (var wheel in new[] { false, true })
{
    var s = new Scene(); s.EnableLeft(); s.Frame(0);
    if (wheel) NativeHandOptionWheel.Left = true; else HelmetNightVision.Left = true;
    s.Left.Primary = true; s.Frame(0);
    Check(s.Pointer.Ray.origin.x > 0, "A hand reserved by another interaction cannot steal the pointer");
    NativeHandOptionWheel.Left = HelmetNightVision.Left = false; s.Frame(0);
    Check(s.Pointer.Ray.origin.x > 0, "Returning a reserved hand with a held button is not a fresh input");
    s.Left.Primary = false; s.Frame(0); s.Left.Primary = true; s.Frame(0);
    Check(s.Pointer.Ray.origin.x < 0, "A released reserved hand can deliberately acquire the pointer");
}

Console.WriteLine($"Controller pointer: {checks} assertions passed (production dispatch + hand ownership; no headset simulation).");

sealed class Scene
{
    public readonly ControllerUiPointer Pointer = new();
    public readonly Camera Camera;
    public readonly TestSlider Slider;
    public readonly Image Background;
    public ControllerSample Left, Right;
    public Scene()
    {
        RaycasterManager.All.Clear(); GraphicRegistry.All.Clear(); SceneSingleton<DynamicMap>.i = null; DynamicMap.mapMaximized = false;
        HelmetNightVision.Left = HelmetNightVision.Right = NativeHandOptionWheel.Left = NativeHandOptionWheel.Right = false;
        EventSystem.current = new GameObject("EventSystem").AddComponent<EventSystem>();
        Camera = new GameObject("camera").AddComponent<Camera>();
        var canvasGo = new GameObject("canvas");
        var canvas = canvasGo.AddComponent<Canvas>(); var raycaster = canvasGo.AddComponent<GraphicRaycaster>(); raycaster.eventCamera = Camera;
        RaycasterManager.All.Add(raycaster); GraphicRegistry.All[canvas] = new();
        var slider = new GameObject("slider"); slider.transform.parent = canvasGo.transform; slider.transform.position = new(0, 0, 1);
        Slider = slider.AddComponent<TestSlider>(); GraphicRegistry.All[canvas].Add(slider.AddComponent<Image>());
        var background = new GameObject("background"); background.transform.parent = canvasGo.transform; background.transform.position = new(0, 0, 3);
        ((RectTransform)background.transform).rect = new(-3, -3, 6, 6); Background = background.AddComponent<Image>(); GraphicRegistry.All[canvas].Add(Background);
        Right = new ControllerSample { Valid = true, AimPosition = new(.25f, 0, 0) }; Aim(false, new(0, 0, 1));
        ModConfiguration.Instance.ControllerBindings.PointerEnabled.Value = true;
    }
    public void EnableLeft() { Left = new ControllerSample { Valid = true, AimPosition = new(-.25f, 0, 0) }; Aim(true, new(0, 0, 1)); }
    public void Aim(bool left, Vector3 world)
    {
        ref var sample = ref (left ? ref Left : ref Right);
        sample.AimRotation = Quaternion.Forward(world - sample.AimPosition);
    }
    public bool Frame(float rightTrigger)
    {
        Right.Trigger = rightTrigger; TrackedControllers.Left = Left; TrackedControllers.Right = Right;
        Time.unscaledTime += Time.unscaledDeltaTime;
        return Pointer.Update(Camera);
    }
}

sealed class TestSlider : Selectable, IPointerDownHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler, IInitializePotentialDragHandler
{
    public int Downs, Ups, Drags, Begins, Ends;
    public float Value;
    public void OnPointerDown(PointerEventData d) { Downs++; Set(d); }
    public void OnPointerUp(PointerEventData d) => Ups++;
    public void OnInitializePotentialDrag(PointerEventData d) => d.useDragThreshold = false;
    public void OnBeginDrag(PointerEventData d) => Begins++;
    public void OnEndDrag(PointerEventData d) => Ends++;
    public void OnDrag(PointerEventData d) { Drags++; Set(d); }
    private void Set(PointerEventData d)
    {
        if (!interactable) return;
        var ray = d.pressEventCamera!.ScreenRay(d.position);
        if (new Plane(transform.forward, transform.position).Raycast(ray, out var distance))
            Value = Math.Clamp(transform.InverseTransformPoint(ray.GetPoint(distance)).x + .5f, 0, 1);
    }
}
sealed class TestToggle : Selectable, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    public int Clicks; public bool Value;
    public void OnPointerDown(PointerEventData d) { }
    public void OnPointerUp(PointerEventData d) { }
    public void OnPointerClick(PointerEventData d) { Clicks++; Value = !Value; }
}
