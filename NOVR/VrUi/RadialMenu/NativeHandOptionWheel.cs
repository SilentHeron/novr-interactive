using HarmonyLib;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.MotionControls;

// Runs before flight grips and button dispatch so the opening click belongs to
// the wheel for the entire frame. The other hand can keep flying normally.
[DefaultExecutionOrder(-1250)]
internal sealed class NativeHandOptionWheel : MonoBehaviour
{
    private static NativeHandOptionWheel? _instance;
    private readonly HandOptionWheelGesture[] _gestures = { new(), new() };
    private readonly HandOptionWheelOwnership _ownership = new();
    private readonly EjectConfirmationGesture _eject = new();
    private static readonly System.Reflection.FieldInfo? ActionIcon = AccessTools.Field(typeof(RadialMenuAction), "iconSprite");
    private NativeRadialMenuLease? _lease;
    private HandMenuWheelGraphic? _menus;
    private bool _menuOnly;
    private Aircraft? _aircraft;
    private RadialMenuAction? _pendingEject;
    private int Owner => _ownership.Owner;
    private GameObject? _view;
    private Canvas? _canvas;
    private Text? _title;
    private Text? _guidance;
    private EjectConfirmationGraphic? _ejectGraphic;
    private InputDevice _ejectHapticDevice;
    private bool _ejectHapticActive;
    private float _nextEjectHaptic;
    private bool _reportedFailure;
    internal static bool IsOpen => _instance != null && _instance.Owner >= 0;
    internal static bool OwnsHand(bool left) => _instance?._ownership.Owns(left ? 0 : 1, Time.frameCount) == true;
    internal static bool OwnsNative(RadialMenuMain menu) => _instance?._lease?.Native == menu;
    private void Awake() => _instance = this;

    private void Update()
    {
        var native = SceneSingleton<RadialMenuMain>.i;
        var flags = CursorManager.GetFlags();
        // Map/selection camera UI is normal while dead or waiting to spawn.
        // Keep menu access there without taking input from chat/loading/dialogs.
        var allowedFlags = CursorFlags.Map | CursorFlags.SelectionMenu | CursorFlags.CameraControlUI;
        var ready = TrackedControllers.Available && !GameplayUI.GameIsPaused &&
            (GameManager.gameState == GameState.SinglePlayer || GameManager.gameState == GameState.Multiplayer) &&
            !WristMenu.IsOpen && !CockpitLayout.Active && !CockpitHudWidgets.Editing &&
            (flags & ~allowedFlags) == CursorFlags.None &&
            (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen) &&
            APIBus.CockpitHudCamera != null;
        var usableAircraft = GameManager.GetLocalAircraft(out var aircraft) && !aircraft.disabled &&
            !aircraft.HasEjected() && aircraft.cockpit != null && !aircraft.cockpit.IsDetached();
        var flight = usableAircraft && CameraStateManager.cameraMode == CameraMode.cockpit &&
            Time.timeScale > 0 && GameManager.flightControlsEnabled && !DynamicMap.mapMaximized && flags == CursorFlags.None;
        if (!ready || flight && (native == null || !native.isActiveAndEnabled) ||
            Owner >= 0 && (_menuOnly == flight || _lease != null && (!_lease.Valid || _lease.Native != native || aircraft != _aircraft)))
        {
            Cancel();
            return;
        }
        // Do not hijack a wheel already opened through the game's desktop input.
        if (_lease == null && NativeRadialMenuState.IsBlockingInput)
        {
            Cancel();
            return;
        }

        if (Owner >= 0)
        {
            if (_menuOnly)
                _menus!.RefreshMapState(DynamicMap.mapMaximized, DynamicMap.mapMaximized || DynamicMap.AllowedToOpen);
            var side = Owner;
            var hand = TrackedControllers.Read(side == 0);
            if (_eject.Active)
            {
                UpdateEjectConfirmation(hand, aircraft!);
                return;
            }
            var gesture = _gestures[side];
            var change = gesture.Update(HandAvailable(side, hand, flight), hand.StickClick, hand.Trigger,
                hand.Axis.x, hand.Axis.y, _lease?.Count ?? HandMenuWheelGraphic.Count, !_menuOnly);
            if (change == HandOptionWheelEvent.Cancel)
            {
                if (hand.Valid && hand.StickClick)
                    InteractionHaptics.Pulse(hand.Device, .14f, .02f, InteractionCue.Release);
                Close();
                return;
            }
            if (change == HandOptionWheelEvent.Commit)
            {
                var menu = _lease != null ? _lease.MenuAt(gesture.Committed) : HandMenuWheelGraphic.MenuAt(gesture.Committed);
                if (menu != NativeWheelMenuAction.None)
                {
                    if (NativeWheelMenuActions.Open(menu, Close))
                        InteractionHaptics.Pulse(hand.Device, .22f, .025f);
                    return;
                }
                var action = _lease?.ActionAt(gesture.Committed);
                if (action != null && action.GetActionType() == RadialMenuAction.ActionType.Eject)
                {
                    if (aircraft == null || !action.AllowedOnAircraft(aircraft))
                    {
                        Close();
                        return;
                    }
                    // Selecting Eject only arms the confirmation. Keep ownership
                    // of this hand and the borrowed native wheel throughout it.
                    _pendingEject = action;
                    _eject.Begin();
                    _lease!.Hover(-1);
                    _ejectGraphic!.Show(ActionIcon?.GetValue(action) as Sprite);
                    _title!.text = "EJECT?";
                    _guidance!.text = "Centre + hold stick click: eject\nRelease: reset  ·  Tilt stick / trigger: cancel";
                    InteractionHaptics.Pulse(hand.Device, .20f, .035f, InteractionCue.Guard);
                    return;
                }
                Close();
                // Release the borrowed UI before calling a native action.
                if (action != null && aircraft != null && !aircraft.disabled && action.AllowedOnAircraft(aircraft))
                {
                    InteractionHaptics.Pulse(hand.Device, .35f, .035f);
                    action.TriggerAction(aircraft);
                }
                return;
            }
            if (change == HandOptionWheelEvent.ChangePage)
            {
                _lease!.ChangePage();
                UpdateTitle();
                InteractionHaptics.Pulse(hand.Device, .18f, .025f);
            }
            if (_lease != null ? _lease.Hover(gesture.Hovered) : _menus!.Hover(gesture.Hovered))
                InteractionHaptics.PulseOnly(hand.Device, .10f, .015f);
            return;
        }

        for (var side = 0; side < 2; side++)
        {
            var hand = TrackedControllers.Read(side == 0);
            var change = _gestures[side].Update(HandAvailable(side, hand, flight), hand.StickClick, hand.Trigger,
                hand.Axis.x, hand.Axis.y, 0);
            if (change != HandOptionWheelEvent.Open)
                continue;
            try
            {
                EnsureView();
                _ownership.Open(side, Time.frameCount);
                _menuOnly = !flight;
                _aircraft = flight ? aircraft : null;
                if (flight)
                    _lease = new NativeRadialMenuLease(native!, (RectTransform)_view!.transform);
                else
                {
                    EnsureMenuOnlyView();
                    _menus!.RefreshMapState(DynamicMap.mapMaximized, DynamicMap.mapMaximized || DynamicMap.AllowedToOpen);
                    _menus!.Hover(-1);
                    _menus.gameObject.SetActive(true);
                }
                _gestures[1 - side].Reset();
                _view!.SetActive(true);
                UpdateTitle();
                _ejectGraphic!.transform.SetAsLastSibling();
                InteractionHaptics.Pulse(hand.Device, .22f, .025f);
                _reportedFailure = false;
            }
            catch (System.Exception error)
            {
                Cancel();
                if (!_reportedFailure)
                    Debug.LogWarning("[NOVR] Native hand option wheel unavailable: " + error);
                _reportedFailure = true;
            }
            break;
        }
    }

    private void UpdateEjectConfirmation(ControllerSample hand, Aircraft aircraft)
    {
        var action = _pendingEject;
        var result = _eject.Update(HandAvailable(Owner, hand, true) && action != null &&
            action.GetActionType() == RadialMenuAction.ActionType.Eject && action.AllowedOnAircraft(aircraft),
            hand.StickClick, hand.Trigger, hand.Axis.x, hand.Axis.y, Time.unscaledDeltaTime);
        if (result == EjectConfirmationEvent.Cancel)
        {
            Close();
            if (hand.Valid)
                InteractionHaptics.Pulse(hand.Device, .14f, .02f, InteractionCue.Release);
            return;
        }
        if (result == EjectConfirmationEvent.Confirm)
        {
            // Dispose the native UI and consume this frame before the callback
            // can detach the cockpit or switch the player to the map.
            Close();
            if (action != null && !aircraft.disabled && !aircraft.HasEjected() && action.AllowedOnAircraft(aircraft))
            {
                InteractionHaptics.Pulse(hand.Device, .65f, .065f, InteractionCue.Eject);
                action.TriggerAction(aircraft);
            }
            return;
        }
        _ejectGraphic!.Progress = _eject.Progress;
        if (!_eject.Holding)
        {
            StopEjectHaptics();
            return;
        }
        if (!_ejectHapticActive || Time.unscaledTime >= _nextEjectHaptic)
        {
            _ejectHapticDevice = hand.Device;
            _ejectHapticActive = true;
            _nextEjectHaptic = Time.unscaledTime + .025f;
            // Short overlapping impulses feel like one rising vibration. They
            // expire quickly on lost tracking and never play repeated audio.
            InteractionHaptics.PulseOnly(hand.Device, Mathf.Lerp(.08f, .48f, _eject.Progress), .045f);
        }
    }

    private void StopEjectHaptics()
    {
        if (_ejectHapticActive && _ejectHapticDevice.isValid)
            _ejectHapticDevice.StopHaptics();
        _ejectHapticActive = false;
        _nextEjectHaptic = 0;
    }

    private static bool HandAvailable(int side, ControllerSample hand, bool flight)
    {
        if (!hand.Valid || !hand.HasAxis)
            return false;
        // Dead/spectating input cannot stay blocked by a grip or map gesture
        // that belonged to the previous aircraft.
        if (!flight) return true;
        if (HelmetNightVision.OwnsHand(side == 0) || CockpitMapInteraction.ReservesThumbstick(side == 0)) return false;
        var controls = MotionFlightController.Instance;
        var stick = (side == 0) == ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        return controls == null || !(stick ? controls.StickHeld : controls.ThrottleHeld);
    }
    private void LateUpdate()
    {
        if (_view == null || Owner < 0)
            return;
        var camera = APIBus.CockpitHudCamera;
        var hand = TrackedControllers.Read(Owner == 0);
        if (camera == null || !hand.Valid)
        {
            Cancel();
            return;
        }
        var trackingRoot = camera.transform.parent;
        var origin = trackingRoot != null ? trackingRoot.position : Vector3.zero;
        var basis = trackingRoot != null ? trackingRoot.rotation : Quaternion.identity;
        var gripPosition = origin + basis * hand.Position;
        var gripRotation = basis * hand.Rotation;
        HandOptionWheelPose.Get(Owner == 0, hand.NativeFrame, out var offset, out var normal, out var forward);
        // The wheel is fixed above the thumbstick with a modest tilt toward the
        // wrist. It still follows only the controller, never the user's gaze.
        // UI fronts face local -Z; local +Y keeps thumbstick-forward selection.
        _view.transform.SetPositionAndRotation(gripPosition + gripRotation * offset,
            gripRotation * Quaternion.LookRotation(-normal, forward));
        _canvas!.worldCamera = camera;
    }

    private void EnsureView()
    {
        if (_view != null)
            return;
        _view = new GameObject("NOVR Native Hand Wheel", typeof(RectTransform), typeof(Canvas));
        // Deliberately independent of scaled aircraft/helmet bones.
        _view.transform.localScale = Vector3.one * .00065f;
        ((RectTransform)_view.transform).sizeDelta = new Vector2(320, 380);
        _view.layer = (int)LayerHelper.GetVrUiLayer();
        _canvas = _view.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 480;
        _canvas.worldCamera = APIBus.CockpitHudCamera;
        _title = Label("Options", 161, 20, 30);
        _guidance = Label("", -173, 13, 42);
        var confirm = new GameObject("Eject confirmation", typeof(RectTransform), typeof(EjectConfirmationGraphic));
        confirm.transform.SetParent(_view.transform, false);
        confirm.layer = _view.layer;
        ((RectTransform)confirm.transform).sizeDelta = new Vector2(128, 128);
        _ejectGraphic = confirm.GetComponent<EjectConfirmationGraphic>();
        _ejectGraphic.raycastTarget = false;
        confirm.SetActive(false);
        _view.SetActive(false);
    }
    private Text Label(string text, float y, int size, float height)
    {
        var go = new GameObject("Wheel guidance", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(_view!.transform, false);
        go.layer = _view.layer;
        var label = go.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.text = text;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        label.rectTransform.sizeDelta = new Vector2(340, height);
        label.rectTransform.anchoredPosition = new Vector2(0, y);
        return label;
    }
    private void EnsureMenuOnlyView()
    {
        if (_menus != null) return;
        var menu = new GameObject("NOVR Gameplay Menu Wheel", typeof(RectTransform), typeof(HandMenuWheelGraphic));
        menu.transform.SetParent(_view!.transform, false);
        menu.layer = _view.layer;
        _menus = menu.GetComponent<HandMenuWheelGraphic>();
        _menus.Initialize();
    }
    private void UpdateTitle()
    {
        if (_title != null)
            _title.text = _menuOnly ? "Menus" : _lease?.WeaponsPage == true ? "Weapons" : "Options";
        if (_guidance != null)
            _guidance.text = (_menuOnly ? "" : "Trigger: change page\n") + "Centre stick: select  ·  Click stick: cancel";
    }
    private void ResetGestures()
    {
        foreach (var gesture in _gestures)
            gesture.Reset();
    }
    private void Close()
    {
        StopEjectHaptics();
        _eject.Reset();
        _pendingEject = null;
        if (_ejectGraphic != null)
            _ejectGraphic.gameObject.SetActive(false);
        _ownership.Close(Time.frameCount);
        try
        {
            _lease?.Dispose();
        }
        finally
        {
            _lease = null;
            _menuOnly = false;
            _aircraft = null;
            if (_menus != null) _menus.gameObject.SetActive(false);
            if (_view != null)
                _view.SetActive(false);
            ResetGestures();
        }
    }
    internal static void Cancel() => _instance?.Close();
    private void OnDisable() => Close();
    private void OnDestroy()
    {
        Close();
        if (_view != null)
            Destroy(_view);
        if (_instance == this)
            _instance = null;
    }
}

[HarmonyPatch(typeof(RadialMenuMain), "Update")]
internal static class NativeHandOptionWheelInputPatch
{
    private static bool Prefix(RadialMenuMain __instance) => !NativeHandOptionWheel.OwnsNative(__instance);
}
