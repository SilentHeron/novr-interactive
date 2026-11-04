using System;
using System.Collections.Generic;
using System.Linq;
using NOVR.MotionControls;
using NOVR.VrUi.Native;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NOVR.VrUi;

[DefaultExecutionOrder(-1100)]
internal sealed partial class WristMenu : MonoBehaviour
{
    // A dedicated bit leaves every game-owned cursor reason untouched on close.
    private const CursorFlags WristCursorFlag = (CursorFlags)(1 << 28);
    private static WristMenu? _instance;
    private readonly ControllerButtonGate _toggle = new();
    private GameObject? _panel;
    private Text? _status;
    private Font? _font;
    private GameObject? _alertsPage;
    private GameObject? _flightPage, _ejectPage, _controlsPage, _throttlePage, _helpPage, _bindingsPage, _handsPage, _resetPage, _homePage, _seatPage, _menuOptionsPage, _displayPage, _feedbackPage, _aimingPage, _lookCuePage;
    private readonly Dictionary<GameObject, string> _pageTitles = new();
    private GameObject? _performancePage, _handPositionPage, _handAnglesPage;
    private bool _fittingLeftHand = true;
    private MenuHistory<GameObject>? _history;
    private Button? _backButton;
    private readonly List<(Text label, Func<string> text)> _liveLabels = new();
    private Text? _bindingHelp, _throttleHelp, _helpFooter;
    private float _nextHelpRefresh;
    private ControllerBindingProfile _menuProfile;
    private bool _menuLeft;
    private Aircraft? _ejectAircraft, _openAircraft, _resetAircraft;
    private bool _resetLeftHanded, _followHand;
    private Vector3 _menuPosition;
    private Quaternion _menuRotation;
    private Button? _hoverButton, _assistButton;
    private bool _anchored;
    private string _pageTitle = "VIEW";
    internal static bool IsOpen => _instance?._panel?.activeSelf == true;
    internal static bool IsPerformancePage => IsOpen && _instance?._performancePage?.activeSelf == true;
    private static bool CollectiveControls => GameManager.GetLocalAircraft(out var aircraft) && MotionFlightController.UsesLiftCollective(aircraft);
    private void Awake() => _instance = this;

    private void Update()
    {
        var config = ModConfiguration.Instance;
        var menuBinding = config.ControllerBindings.Get(ControllerActionId.WristMenu);
        var menuLeft = ControllerBindings.ResolveLeft(menuBinding.Hand, config.MotionControls.LeftHanded.Value);
        var hand = TrackedControllers.Read(menuLeft);
        var bindingVersion = config.ControllerBindings.ChangeVersion;
        if (_bindingVersion != bindingVersion || _menuProfile != config.ControllerBindings.EffectiveProfile || _menuLeft != menuLeft)
        {
            _toggle.Reset();
            _bindingVersion = bindingVersion;
            _menuProfile = config.ControllerBindings.EffectiveProfile;
            _menuLeft = menuLeft;
        }
        var cockpit = CameraStateManager.cameraMode == CameraMode.cockpit && GameManager.GetLocalAircraft(out var viewedAircraft) && !viewedAircraft.disabled &&
                      Time.timeScale > 0f && (SceneSingleton<RadialMenuMain>.i == null || !RadialMenuMain.IsInUse()) &&
                      (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen);
        var recoveryMap = !cockpit && ControllerActions.MapAccessAvailable;
        _toggle.Update(config.ControllerBindings.PointerEnabled.Value && hand.Valid && !NativeHandOptionWheel.OwnsHand(menuLeft) &&
            !CockpitMapInteraction.ReservesButton(menuLeft, menuBinding.Button, hand.NativeFrame) && (cockpit || recoveryMap),
            ControllerButtonNames.Resolve(menuBinding.Button, hand.NativeFrame) == ControllerButton.Trigger ? 0 : ControllerBindings.Read(hand, menuBinding.Button));
        if (_toggle.Pressed || ((cockpit || recoveryMap) && TrackedControllers.Available && Keyboard.current?.f9Key.wasPressedThisFrame == true))
        {
            // Outside a usable cockpit, View offers the recovery map instead
            // of a cockpit-only setup menu which cannot stay open there.
            if (recoveryMap)
            {
                ControllerActions.ToggleMap();
                return;
            }
            if (IsOpen)
                Close();
            else
                Open();
        }
        if (!IsOpen)
            return;
        if (_backButton != null)
            _backButton.interactable = _history?.CanBack == true;
        foreach (var setting in _settingRefresh)
            if (setting.page.activeInHierarchy)
                setting.refresh();
        foreach (var item in _liveLabels)
            if (item.label != null && item.label.gameObject.activeInHierarchy)
            {
                var text = item.text();
                if (item.label.text != text)
                    item.label.text = text;
            }
        if (_hoverButton != null && _flightPage?.activeSelf == true)
            _hoverButton.interactable = GameManager.GetLocalAircraft(out var hoverAircraft) &&
                HoverAssist.UnavailableReason(hoverAircraft) == null;
        if (_assistButton != null && _flightPage?.activeSelf == true)
            _assistButton.interactable = GameManager.GetLocalAircraft(out var assistAircraft) &&
                assistAircraft.GetControlsFilter()?.HasFlightAssist() == true;
        if (Time.unscaledTime >= _nextHelpRefresh)
        {
            if (_bindingHelp != null && _helpPage?.activeSelf == true)
            {
                _bindingHelp.text = HandHelp(true);
                _throttleHelp!.text = HandHelp(false);
                _helpFooter!.text = "Hold the assigned handle for flight actions.\n" +
                    "Map: " + DescribeBinding(Bindings.Get(ControllerActionId.Map)) +
                    " · View: " + DescribeBinding(Bindings.Get(ControllerActionId.WristMenu)) +
                    "\nFree hand: click thumbstick for the option wheel.";
            }
            RefreshBindingEditor();
            RefreshHudLayoutEditor();
            _nextHelpRefresh = Time.unscaledTime + .25f;
        }
        if (_ejectPage?.activeSelf == true &&
            (!GameManager.GetLocalAircraft(out var confirmAircraft) || confirmAircraft != _ejectAircraft))
        {
            Close();
            return;
        }
        if (!cockpit || !GameManager.GetLocalAircraft(out var currentAircraft) || currentAircraft != _openAircraft || DynamicMap.mapMaximized || CursorManager.GetFlag(CursorFlags.GameMenu))
        {
            Close();
            return;
        }
        var camera = APIBus.CockpitHudCamera;
        if (camera == null)
        {
            Close();
            return;
        }
        var trackingRoot = camera.transform.parent;
        var followHand = config.MotionControls.ViewMenuFollowsHand.Value;
        if (_followHand != followHand)
        {
            _followHand = followHand;
            _anchored = false;
        }
        if (!_anchored || followHand && hand.Valid && VrUiCursor.I?.PrimaryHeld != true)
        {
            Vector3 position;
            Quaternion rotation;
            if (followHand && hand.Valid)
            {
                position = hand.Position + new Vector3(0, .19f, .16f);
                if (trackingRoot != null)
                    position = trackingRoot.TransformPoint(position);
                var forward = position - camera.transform.position;
                rotation = forward.sqrMagnitude > .01f ? Quaternion.LookRotation(forward, camera.transform.up) : camera.transform.rotation;
            }
            else
            {
                var up = trackingRoot != null ? trackingRoot.up : Vector3.up;
                var forward = Vector3.ProjectOnPlane(camera.transform.forward, up);
                if (forward.sqrMagnitude < .01f)
                    forward = trackingRoot != null ? trackingRoot.forward : Vector3.forward;
                forward.Normalize();
                position = camera.transform.position + forward * .72f - up * .03f;
                rotation = Quaternion.LookRotation(forward, up);
            }
            var blend = _anchored ? FlightControlMath.SmoothingWeight(Time.unscaledDeltaTime, 14) : 1;
            position = Vector3.Lerp(_panel!.transform.position, position, blend);
            rotation = Quaternion.Slerp(_panel.transform.rotation, rotation, blend);
            _menuPosition = trackingRoot != null ? trackingRoot.InverseTransformPoint(position) : position;
            _menuRotation = trackingRoot != null ? Quaternion.Inverse(trackingRoot.rotation) * rotation : rotation;
            _anchored = true;
        }
        _panel!.transform.SetPositionAndRotation(trackingRoot != null ? trackingRoot.TransformPoint(_menuPosition) : _menuPosition,
            trackingRoot != null ? trackingRoot.rotation * _menuRotation : _menuRotation);
        if (_status != null)
        {
            var status = _pageTitle + "\n" + (CockpitProfiles.Instance?.AircraftName ?? "Cockpit") +
                " · Motion " + (config.MotionControls.Enabled.Value ? "ON" : "OFF") + "\n" +
                (CockpitHudWidgets.Editing ? "Layout preview · Save or cancel when finished" : "Either hand: point + trigger · View closes this menu");
            if (_status.text != status)
                _status.text = status;
        }
    }

    private void Open()
    {
        if (DynamicMap.mapMaximized)
            SceneSingleton<DynamicMap>.i?.Minimize();
        if (!GameManager.GetLocalAircraft(out _openAircraft))
            return;
        EnsurePanel();
        Home();
        if (CockpitHudWidgets.Editing)
            ShowLayoutControls();
        _panel!.SetActive(true);
        _anchored = false;
        CursorManager.SetFlag(WristCursorFlag, true);
        MotionFlightController.Instance?.ResetGrips();
    }

    internal static void Close()
    {
        if (_instance?._resetPage?.activeSelf == true)
            _instance.ShowSetupPage();
        if (_instance?._ejectPage?.activeSelf == true)
            _instance.ShowFlightPage();
        if (_instance != null)
            _instance._ejectAircraft = null;
        if (_instance?._panel != null)
            _instance._panel.SetActive(false);
        CursorManager.SetFlag(WristCursorFlag, false);
        MotionFlightController.Instance?.ResetGrips();
    }

    private void ShowPage(GameObject page)
    {
        _history!.Push(page);
        RefreshPage();
    }
    private void Back()
    {
        _history!.Back();
        RefreshPage();
    }
    private void Home()
    {
        _history!.Home();
        RefreshPage();
    }
    private void RefreshPage()
    {
        foreach (var page in _pageTitles.Keys)
            page.SetActive(page == _history!.Current);
        if (_history!.Current != _ejectPage)
            _ejectAircraft = null;
        if (_history.Current != _resetPage)
            _resetAircraft = null;
        _pageTitle = _pageTitles[_history.Current];
        _nextHelpRefresh = 0;
    }
    private void ShowFlightPage()
    {
        Home();
        ShowPage(_flightPage!);
    }
    private void ShowSetupPage()
    {
        Home();
        ShowPage(_seatPage!);
    }
    private void AskReset()
    {
        CockpitHudWidgets.Instance?.Finish(false);
        if (!GameManager.GetLocalAircraft(out _resetAircraft))
            return;
        _resetLeftHanded = ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        ShowPage(_resetPage!);
    }
    private void ConfirmReset()
    {
        if (GameManager.GetLocalAircraft(out var aircraft) && aircraft == _resetAircraft &&
            _resetLeftHanded == ModConfiguration.Instance.MotionControls.LeftHanded.Value)
            CockpitProfiles.Instance?.ResetProfile();
        Back();
    }
    private void AskToEject()
    {
        if (!GameManager.GetLocalAircraft(out _ejectAircraft))
            return;
        ShowPage(_ejectPage!);
    }

    private void ConfirmEject()
    {
        if (GameManager.GetLocalAircraft(out var aircraft) && aircraft == _ejectAircraft)
            ControllerActions.Instance?.QueueWristAction(NativeControllerAction.Eject);
        Close();
    }

    private void EnsurePanel()
    {
        if (_panel != null)
            return;
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _panel = new GameObject("NOVR Wrist Panel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        _panel.transform.SetParent(transform, false);
        _panel.transform.localScale = Vector3.one * 0.0009f;
        var rect = (RectTransform)_panel.transform;
        rect.sizeDelta = new Vector2(WristMenuNavigation.PanelWidth, WristMenuNavigation.PanelHeight);
        var canvas = _panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = APIBus.CockpitHudCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;
        var background = _panel.AddComponent<Image>();
        background.color = new Color(0.025f, 0.045f, 0.055f, 1f);
        _status = Label(rect, "", new Vector2(0, 279), new Vector2(620, 81), 19);
        ViewMenuControls.Panel(rect, "Header divider", new Vector2(0, 234), new Vector2(620, 2), ViewMenuControls.Accent);
        ViewMenuControls.Panel(rect, "Footer divider", new Vector2(0, -252), new Vector2(620, 2), ViewMenuControls.Accent);
        var home = BuildHomePage();
        BuildSeatPage();
        BuildResetPage();
        BuildFlightActionsPage();
        BuildEjectPage();
        BuildFlightControlsPage();
        BuildThrottlePage();
        CreateBindingPages();
        BuildGuidePage();
        BuildMenuOptionsPage();
        BuildHandsPage();
        BuildHandPositionPage();
        BuildHandAnglesPage();
        BuildHudPage();
        BuildAlertsPage();
        CreateHudLayoutPages();
        BuildSymbolsPage();
        BuildLookCuePage();
        BuildPerformancePage();
        BuildFeedbackPage();

        _history = new MenuHistory<GameObject>(home);
        _backButton = AddButton(rect, "Back", 0, 8, Back, 3).transform.parent.GetComponent<Button>();
        AddButton(rect, "Home", 1, 8, Home, 3);
        var closeButton = AddButton(rect, "Close", 2, 8, Close, 3).transform.parent.GetComponent<Button>();
        NativeButtonFeedback.Configure(closeButton, new Color(.19f, .25f, .28f));
        Home();
        LayerHelper.SetLayerRecursive(_panel.transform, LayerHelper.GetVrUiLayer());
    }

    private GameObject Page(string title)
    {
        var page = new GameObject(title, typeof(RectTransform));
        page.transform.SetParent(_panel!.transform, false);
        page.SetActive(false);
        _pageTitles.Add(page, title);
        return page;
    }

    private void AddLiveButton(Transform parent, Func<string> text, int column, int row, Action action) =>
        _liveLabels.Add((AddButton(parent, text(), column, row, action), text));

    private void AddFeedbackPreview(Transform parent, string text, int column, int row, InteractionCue cue)
    {
        var button = AddButton(parent, text, column, row, null).transform.parent.GetComponent<Button>();
        ((RectTransform)button.transform).anchoredPosition = new Vector2(column == 0 ? -160 : 160, row == 3 ? -32 : -82);
        ((RectTransform)button.transform).sizeDelta = new Vector2(300, 42);
        var preview = button.gameObject.AddComponent<FeedbackPreviewButton>();
        preview.Cue = cue;
        button.onClick.AddListener(preview.Play);
    }

    private Text AddButton(Transform parent, string text, int column, int row, Action? action, int columns = 2)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        var bounds = WristMenuNavigation.ButtonBounds(column, row, columns);
        rect.sizeDelta = new Vector2(bounds.Width, bounds.Height);
        rect.anchoredPosition = new Vector2(bounds.X, bounds.Y);
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        if (action != null)
            button.onClick.AddListener(() => action());
        NativeButtonFeedback.Configure(button, new Color(0.10f, 0.24f, 0.29f));
        return Label(rect, text, Vector2.zero, new Vector2(bounds.Width - 10, 45), 20);
    }

    private Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var label = go.GetComponent<Text>();
        label.font = _font;
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    private void OnDisable()
    {
        Close();
        _toggle.Reset();
    }
    private void OnDestroy()
    {
        OnDisable();
        if (_instance == this)
            _instance = null;
    }
}
