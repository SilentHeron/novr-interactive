using HarmonyLib;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.MotionControls;

// Runs before flight grips and button dispatch so the opening click belongs to
// the wheel for the entire frame. The other hand can keep flying normally.
[DefaultExecutionOrder(-1250)]
internal sealed class NativeHandOptionWheel : MonoBehaviour
{
    private static NativeHandOptionWheel? _instance;
    private readonly HandOptionWheelGesture[] _gestures = { new(), new() };
    private readonly HandOptionWheelOwnership _ownership = new();
    private NativeRadialMenuLease? _lease;
    private Aircraft? _aircraft;
    private int Owner => _ownership.Owner;
    private GameObject? _view;
    private Canvas? _canvas;
    private Text? _title;
    private bool _reportedFailure;
    internal static bool IsOpen => _instance?._lease != null;
    internal static bool OwnsHand(bool left) => _instance?._ownership.Owns(left ? 0 : 1, Time.frameCount) == true;
    internal static bool OwnsNative(RadialMenuMain menu) => _instance?._lease?.Native == menu;
    private void Awake() => _instance = this;

    private void Update()
    {
        var native = SceneSingleton<RadialMenuMain>.i;
        var ready = TrackedControllers.Available && Time.timeScale > 0 && GameManager.flightControlsEnabled &&
            CameraStateManager.cameraMode == CameraMode.cockpit && !DynamicMap.mapMaximized &&
            !WristMenu.IsOpen && !CockpitLayout.Active && !CockpitHudWidgets.Editing &&
            CursorManager.GetFlags() == CursorFlags.None &&
            (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen) &&
            APIBus.CockpitHudCamera != null && native != null && native.isActiveAndEnabled;
        if (!GameManager.GetLocalAircraft(out var aircraft) || aircraft.disabled || aircraft.cockpit == null || aircraft.cockpit.IsDetached()) ready = false;
        if (!ready || _lease != null && (!_lease.Valid || _lease.Native != native || aircraft != _aircraft))
        { Cancel(); return; }
        // Do not hijack a wheel already opened through the game's desktop input.
        if (_lease == null && RadialMenuMain.IsInUse()) { ResetGestures(); return; }

        if (Owner >= 0)
        {
            var side = Owner;
            var hand = TrackedControllers.Read(side == 0);
            var gesture = _gestures[side];
            var change = gesture.Update(HandAvailable(side, hand), hand.StickClick, hand.Trigger,
                hand.Axis.x, hand.Axis.y, _lease!.Count);
            if (change == HandOptionWheelEvent.Cancel)
            {
                if (hand.Valid && hand.StickClick) InteractionHaptics.Pulse(hand.Device, .14f, .02f, InteractionCue.Release);
                Close(); return;
            }
            if (change == HandOptionWheelEvent.Commit)
            {
                var action = _lease.ActionAt(gesture.Committed);
                Close();
                // Close before callbacks: eject and other native actions may
                // replace the aircraft/HUD immediately.
                if (action != null && aircraft != null && !aircraft.disabled && action.AllowedOnAircraft(aircraft))
                {
                    InteractionHaptics.Pulse(hand.Device, .35f, .035f);
                    action.TriggerAction(aircraft);
                }
                return;
            }
            if (change == HandOptionWheelEvent.ChangePage)
            {
                _lease.ChangePage(); UpdateTitle();
                InteractionHaptics.Pulse(hand.Device, .18f, .025f);
            }
            if (_lease.Hover(gesture.Hovered)) InteractionHaptics.PulseOnly(hand.Device, .10f, .015f);
            return;
        }

        for (var side = 0; side < 2; side++)
        {
            var hand = TrackedControllers.Read(side == 0);
            var change = _gestures[side].Update(HandAvailable(side, hand), hand.StickClick, hand.Trigger,
                hand.Axis.x, hand.Axis.y, 0);
            if (change != HandOptionWheelEvent.Open) continue;
            try
            {
                EnsureView();
                _ownership.Open(side, Time.frameCount); _aircraft = aircraft;
                _lease = new NativeRadialMenuLease(native!, (RectTransform)_view!.transform);
                _gestures[1 - side].Reset(); _view.SetActive(true); UpdateTitle();
                InteractionHaptics.Pulse(hand.Device, .22f, .025f);
                _reportedFailure = false;
            }
            catch (System.Exception error)
            {
                Cancel();
                if (!_reportedFailure) Debug.LogWarning("[NOVR] Native hand option wheel unavailable: " + error);
                _reportedFailure = true;
            }
            break;
        }
    }

    private static bool HandAvailable(int side, ControllerSample hand)
    {
        if (!hand.Valid || !hand.HasAxis || HelmetNightVision.OwnsHand(side == 0)) return false;
        var controls = MotionFlightController.Instance;
        var stick = (side == 0) == ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        return controls == null || !(stick ? controls.StickHeld : controls.ThrottleHeld);
    }
    private void LateUpdate()
    {
        if (_lease == null || _view == null || Owner < 0) return;
        var camera = APIBus.CockpitHudCamera;
        var hand = TrackedControllers.Read(Owner == 0);
        if (camera == null || !hand.Valid) { Cancel(); return; }
        var trackingRoot = camera.transform.parent;
        var origin = trackingRoot != null ? trackingRoot.position : Vector3.zero;
        var basis = trackingRoot != null ? trackingRoot.rotation : Quaternion.identity;
        var gripPosition = origin + basis * hand.Position;
        var gripRotation = basis * hand.Rotation;
        ThumbstickSurface(Owner == 0, hand.NativeFrame, out var cap, out var normal, out var forward);
        // The wheel lies over the thumbstick's top face and rotates with the
        // physical controller. Glove-fit settings and head motion cannot tilt it.
        // UI fronts face local -Z; local +Y follows thumbstick-forward selection.
        _view.transform.SetPositionAndRotation(gripPosition + gripRotation * (cap + normal * .055f),
            gripRotation * Quaternion.LookRotation(-normal, forward));
        _canvas!.worldCamera = camera;
    }

    private static void ThumbstickSurface(bool left, bool frame, out Vector3 cap, out Vector3 normal, out Vector3 forward)
    {
        // Derived from the installed controller render models' openxr_grip and
        // thumbstick transforms, converted from OpenVR to Unity coordinates.
        // Frame's button deck is canted relative to grip-up, and mirrored in X.
        var side = left ? 1f : -1f;
        if (frame)
        {
            cap = new Vector3(side * .027826f, .0110762f, .0537274f);
            normal = new Vector3(side * .1654186f, .7774802f, .6067629f).normalized;
            forward = new Vector3(side * .1007780f, -.6253439f, .7738145f).normalized;
        }
        else
        {
            // Current Touch / Quest controllers share this grip-relative deck.
            cap = new Vector3(-side * .006413f, -.0166453f, .0537527f);
            normal = new Vector3(0, .7358035f, .6771951f).normalized;
            forward = new Vector3(0, -.6771951f, .7358035f).normalized;
        }
    }
    private void EnsureView()
    {
        if (_view != null) return;
        _view = new GameObject("NOVR Native Hand Wheel", typeof(RectTransform), typeof(Canvas));
        // Deliberately independent of scaled aircraft/helmet bones.
        _view.transform.localScale = Vector3.one * .00065f;
        ((RectTransform)_view.transform).sizeDelta = new Vector2(320, 380);
        _view.layer = (int)LayerHelper.GetVrUiLayer();
        _canvas = _view.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.overrideSorting = true; _canvas.sortingOrder = 480; _canvas.worldCamera = APIBus.CockpitHudCamera;
        _title = Label("Options", 161, 20, 30);
        Label("Trigger: change page\nCentre stick: select  ·  Click stick: cancel", -173, 13, 42);
        _view.SetActive(false);
    }
    private Text Label(string text, float y, int size, float height)
    {
        var go = new GameObject("Wheel guidance", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(_view!.transform, false); go.layer = _view.layer;
        var label = go.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.text = text; label.fontSize = size; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.rectTransform.sizeDelta = new Vector2(340, height); label.rectTransform.anchoredPosition = new Vector2(0, y);
        return label;
    }
    private void UpdateTitle() { if (_title != null) _title.text = _lease?.WeaponsPage == true ? "Weapons" : "Options"; }
    private void ResetGestures() { foreach (var gesture in _gestures) gesture.Reset(); }
    private void Close()
    {
        _ownership.Close(Time.frameCount);
        try { _lease?.Dispose(); }
        finally
        {
            _lease = null; _aircraft = null;
            if (_view != null) _view.SetActive(false);
            ResetGestures();
        }
    }
    internal static void Cancel() => _instance?.Close();
    private void OnDisable() => Close();
    private void OnDestroy() { Close(); if (_view != null) Destroy(_view); if (_instance == this) _instance = null; }
}

[HarmonyPatch(typeof(RadialMenuMain), "Update")]
internal static class NativeHandOptionWheelInputPatch
{
    private static bool Prefix(RadialMenuMain __instance) => !NativeHandOptionWheel.OwnsNative(__instance);
}
