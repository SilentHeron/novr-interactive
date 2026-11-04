using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

// Direct fingertip controls in the same rigid cockpit/UI space as the widget
// editor. No desktop cursor, controller ray or snapped hand drives contacts.
[DefaultExecutionOrder(-1240)]
internal sealed class CockpitMapInteraction : MonoBehaviour
{
    private static CockpitMapInteraction? _instance;
    private readonly CockpitMapInputState _input = new();
    private readonly CockpitTouchGate[,] _touch = { { new(), new(), new() }, { new(), new(), new() } };
    private readonly bool[] _pointing = new bool[2];
    private readonly int[] _hover = { -1, -1 };
    private CockpitMapNavigation? _navigation;
    private RectTransform? _viewport, _view;
    private readonly Image[] _border = new Image[4], _crosshair = new Image[4], _buttons = new Image[2];
    private Vector2 _size;
    private float _nextTouch;

    internal static bool Pointing(bool left) => _instance?._pointing[left ? 0 : 1] == true;
    internal static bool ReservesThumbstick(bool left)
    {
        if (left || _instance == null) return false;
        // The opposite hand's wheel can leave flight controls active. Retain
        // an outstanding neutral handoff even while map interaction pauses.
        if (_instance._input.Reserved) return true;
        var controls = MotionFlightController.Instance;
        return _instance.ContextAvailable() && _instance._navigation?.Selected == true &&
            controls != null && controls.FlightActive && controls.StickHeld;
    }
    internal static bool ReservesButton(bool left, ControllerButton button, bool frame) =>
        ControllerButtonNames.Resolve(button, frame) == ControllerButton.StickClick && ReservesThumbstick(left);
    private void Awake() => _instance = this;

    private bool ContextAvailable() => CockpitMapWidget.Available && _navigation != null && ControllerContextAvailable();
    private static bool ControllerContextAvailable() =>
        TrackedControllers.Available && Time.timeScale > 0 && GameManager.flightControlsEnabled &&
        CameraStateManager.cameraMode == CameraMode.cockpit &&
        !CockpitHudWidgets.Editing && !CockpitLayout.Active && !WristMenu.IsOpen &&
        !DynamicMap.mapMaximized && !Cursor.visible && CursorManager.GetFlags() == CursorFlags.None &&
        (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen) &&
        (SceneSingleton<RadialMenuMain>.i == null || !RadialMenuMain.IsInUse());

    private void Update()
    {
        var navigation = CockpitMapWidget.Navigation; var viewport = CockpitMapWidget.Viewport;
        if (_navigation != navigation || _viewport != viewport)
        {
            Release(); _navigation = navigation; _viewport = viewport;
            if (navigation != null && viewport != null) CreateView(viewport);
        }
        var available = ContextAvailable();
        if (!available)
        {
            _navigation?.Deactivate(); _input.Update(false, false, false, 0, 0, false); ResetTouches();
            if (_view != null) _view.gameObject.SetActive(false);
            return;
        }
        // Lost map-controller tracking cancels selection. Reacquire it
        // deliberately instead of inheriting stale pan or selection input.
        var right = TrackedControllers.Read(false);
        if (!right.Valid || !right.HasAxis)
        { _navigation!.Deactivate(); _input.Update(false, false, false, 0, 0, false); }
        if (_view == null || _viewport == null) return;
        _view.gameObject.SetActive(true); _view.SetAsLastSibling();
        Layout(_viewport.rect.size);
        for (var side = 0; side < 2; side++) UpdateHand(side);
        RefreshVisuals();
    }

    // Called by button dispatch after current-frame grip acquisition/release.
    // This lets flight movement and button dispatch share one ownership rule.
    internal static void UpdateNavigation()
    {
        var self = _instance; if (self == null) return;
        var hand = TrackedControllers.Read(false);
        var controls = MotionFlightController.Instance;
        // A hidden map still needs a neutral handoff. Gate ordinary controls
        // independently from map availability until that handoff completes.
        var available = ControllerContextAvailable() && hand.Valid && hand.HasAxis;
        self._input.Update(available, self.ContextAvailable() && self._navigation?.Selected == true,
            controls != null && controls.FlightActive && controls.StickHeld, hand.Axis.x, hand.Axis.y, hand.StickClick);
        if (!available || !self.ContextAvailable()) return;
        self._navigation!.Pan(new Vector2(self._input.PanX, self._input.PanY), Time.unscaledDeltaTime);
        if (self._input.SelectPressed)
        {
            var selected = self._navigation.SelectCenter();
            if (selected) InteractionHaptics.Pulse(hand.Device, InteractionCue.Click);
            else InteractionHaptics.PulseOnly(hand.Device, .10f, .02f);
        }
    }

    private void UpdateHand(int side)
    {
        var left = side == 0; var sample = TrackedControllers.Read(left);
        var controls = MotionFlightController.Instance;
        var isStick = left == ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        var held = controls != null && (isStick ? controls.StickHeld : controls.ThrottleHeld);
        var gameRoot = APIBus.MainCamera != null ? APIBus.MainCamera.transform.parent : null;
        var uiRoot = APIBus.CockpitHudCamera != null ? APIBus.CockpitHudCamera.transform.parent : null;
        if (!sample.Valid || held || NativeHandOptionWheel.OwnsHand(left) || HelmetNightVision.OwnsHand(left) ||
            gameRoot == null || uiRoot == null || _viewport == null)
        { ResetHand(side); return; }
        var tip = PilotHandVisuals.TouchPoint(left, sample, gameRoot, true);
        var point = _viewport.InverseTransformPoint(uiRoot.TransformPoint(gameRoot.InverseTransformPoint(tip)));
        var metres = _viewport.TransformVector(Vector3.forward).magnitude;
        if (!TrackedControllers.Finite(point) || metres <= 0) { ResetHand(side); return; }
        var depth = -point.z * metres;
        var near = depth >= -.025f && depth <= .075f;
        var hit = -1;
        for (var button = 0; button < 2; button++)
        {
            var delta = (Vector2)point - ButtonCenter(button);
            if (Mathf.Abs(delta.x) <= 26 + .006f / metres && Mathf.Abs(delta.y) <= 26 + .006f / metres)
            { hit = button + 1; break; }
        }
        if (hit < 0 && Mathf.Abs(point.x) <= _size.x * .5f && Mathf.Abs(point.y) <= _size.y * .5f) hit = 0;
        var hover = near ? hit : -1;
        if (hover >= 0 && hover != _hover[side]) InteractionHaptics.Pulse(sample.Device, InteractionCue.Touch);
        _hover[side] = hover; _pointing[side] = hover >= 0;
        for (var item = 0; item < 3; item++)
        {
            if (!_touch[side, item].Update(true, hit == item, depth) || Time.unscaledTime < _nextTouch) continue;
            _nextTouch = Time.unscaledTime + .25f;
            if (item == 0)
            {
                if (_navigation!.Selected) _navigation.Deactivate(); else _navigation.Activate();
            }
            else _navigation!.Zoom(item == 1 ? 1 : -1);
            InteractionHaptics.Pulse(sample.Device, InteractionCue.Click);
            // Both fingertips must withdraw after a contact. Crossing a button
            // laterally while already inside the face is not another press.
            for (var hand = 0; hand < 2; hand++)
                for (var control = 0; control < 3; control++) _touch[hand, control].Reset();
            break;
        }
    }

    private void CreateView(RectTransform parent)
    {
        var go = new GameObject("NOVR map touch controls", typeof(RectTransform));
        _view = (RectTransform)go.transform; _view.SetParent(parent, false);
        _view.localPosition = new Vector3(0, 0, -2);
        for (var i = 0; i < 4; i++)
        {
            _border[i] = Plate("Selected map edge", _view, Vector2.zero, Vector2.one);
            _crosshair[i] = Plate("Map centre", _view, Vector2.zero, i < 2 ? new Vector2(7, 1.6f) : new Vector2(1.6f, 7));
            _crosshair[i].rectTransform.anchoredPosition = i < 2 ? new Vector2(i == 0 ? -7 : 7, 0) : new Vector2(0, i == 2 ? -7 : 7);
        }
        for (var i = 0; i < 2; i++)
        {
            _buttons[i] = Plate(i == 0 ? "Zoom in" : "Zoom out", _view, Vector2.zero, new Vector2(52, 52));
            var stroke = Plate("Minus", _buttons[i].transform, Vector2.zero, new Vector2(24, 3)); stroke.color = Color.white;
            if (i == 0) { stroke = Plate("Plus", _buttons[i].transform, Vector2.zero, new Vector2(3, 24)); stroke.color = Color.white; }
        }
        LayerHelper.SetLayerRecursive(_view, LayerHelper.GetVrUiLayer());
        Layout(parent.rect.size);
    }
    private Vector2 ButtonCenter(int index) => new(_size.x * .5f + 40, index == 0 ? 32 : -32);
    private void Layout(Vector2 size)
    {
        _size = size;
        _view!.sizeDelta = size;
        for (var i = 0; i < 4; i++)
        {
            _border[i].rectTransform.sizeDelta = i < 2 ? new Vector2(size.x, 2.5f) : new Vector2(2.5f, size.y);
            _border[i].rectTransform.anchoredPosition = i < 2 ? new Vector2(0, (i == 0 ? -1 : 1) * size.y * .5f) : new Vector2((i == 2 ? -1 : 1) * size.x * .5f, 0);
        }
        for (var i = 0; i < 2; i++) _buttons[i].rectTransform.anchoredPosition = ButtonCenter(i);
    }
    private void RefreshVisuals()
    {
        var selected = _navigation?.Selected == true;
        foreach (var edge in _border) { edge.enabled = selected; edge.color = new Color(.42f, .96f, .78f, .6f); }
        foreach (var line in _crosshair) { line.enabled = selected; line.color = new Color(.86f, 1f, .93f, .85f); }
        for (var i = 0; i < 2; i++) _buttons[i].color = _hover[0] == i + 1 || _hover[1] == i + 1
            ? new Color(.16f, .38f, .36f, .9f) : new Color(.035f, .08f, .09f, .8f);
    }
    private static Image Plate(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.raycastTarget = false;
        image.rectTransform.anchoredPosition = position; image.rectTransform.sizeDelta = size;
        return image;
    }
    private void ResetHand(int side)
    {
        _pointing[side] = false; _hover[side] = -1;
        for (var item = 0; item < 3; item++) _touch[side, item].Reset();
    }
    private void ResetTouches() { ResetHand(0); ResetHand(1); }
    private void Release()
    {
        _navigation?.Deactivate(); _navigation = null; _viewport = null;
        _input.Reset(); ResetTouches();
        if (_view != null) Destroy(_view.gameObject); _view = null;
    }
    private void OnDisable() => Release();
    private void OnDestroy() { Release(); if (_instance == this) _instance = null; }
}
