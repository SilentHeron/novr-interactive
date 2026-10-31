using System;
using System.Collections.Generic;
using System.Linq;
using NOVR.MotionControls;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NOVR.VrUi;

[DefaultExecutionOrder(-1100)]
internal sealed class WristMenu : MonoBehaviour
{
    // A dedicated bit leaves every game-owned cursor reason untouched on close.
    private const CursorFlags WristCursorFlag = (CursorFlags)(1 << 28);
    private static WristMenu? _instance;
    private readonly ControllerButtonGate _toggle = new();
    private GameObject? _panel;
    private Text? _status;
    private Font? _font;
    private GameObject? _alertsPage, _nativeCockpitPage;
    private GameObject? _setupPage, _flightPage, _ejectPage, _consolePage, _controlsPage, _helpPage, _bindingsPage, _handsPage, _resetPage, _homePage, _seatPage, _comfortPage, _alignmentPage, _displayPage, _feedbackPage, _brightnessPage, _aimingPage, _opticalPage, _zoomPage;
    private readonly Dictionary<GameObject, string> _pageTitles = new();
    private GameObject? _performancePage, _handPositionPage, _handAnglesPage;
    private bool _fittingLeftHand = true;
    private string FittedHand => _fittingLeftHand ? "LEFT HAND" : "RIGHT HAND";
    private MenuHistory<GameObject>? _history;
    private Button? _backButton;
    private readonly List<(Text label, Func<string> text)> _liveLabels = new();
    private Text? _controlsInfo, _bindingHelp, _throttleHelp, _helpFooter, _bindingsInfo;
    private float _nextHelpRefresh;
    private Aircraft? _ejectAircraft, _openAircraft, _resetAircraft;
    private bool _resetLeftHanded, _followHand;
    private Vector3 _menuPosition;
    private Quaternion _menuRotation;
    private Button? _hoverButton, _assistButton;
    private Transform? _buttonParent;
    private bool _anchored;
    private string _pageTitle = "VIEW";
    internal static bool IsOpen => _instance?._panel?.activeSelf == true;
    internal static bool IsConsoleSetup => IsOpen && _instance?._consolePage?.activeSelf == true;
    internal static bool IsPerformancePage => IsOpen && _instance?._performancePage?.activeSelf == true;
    private static bool CollectiveControls => GameManager.GetLocalAircraft(out var aircraft) && MotionFlightController.UsesLiftCollective(aircraft);
    private void Awake() => _instance = this;

    private static void ResizeHud(float amount)
    {
        var c = ModConfiguration.Instance; c.FocusedFlightHud.Value = true;
        c.FlightHudScale.Value += amount; c.MarkerScale.Value += amount;
        c.MarkerLabelScale.Value += amount; c.ReticleScale.Value += amount; c.WeaponHudScale.Value += amount;
        c.CockpitAlerts.Scale.Value += amount;
    }

    private void Update()
    {
        var config = ModConfiguration.Instance;
        var hand = TrackedControllers.Read(!config.MotionControls.LeftHanded.Value);
        var cockpit = CameraStateManager.cameraMode == CameraMode.cockpit && GameManager.GetLocalAircraft(out var viewedAircraft) && !viewedAircraft.disabled &&
                      Time.timeScale > 0f && (SceneSingleton<RadialMenuMain>.i == null || !RadialMenuMain.IsInUse()) &&
                      (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen);
        var recoveryMap = !cockpit && ControllerActions.MapAccessAvailable;
        _toggle.Update(config.ControllerBindings.PointerEnabled.Value && hand.Valid && (cockpit || recoveryMap),
            ControllerBindings.Read(hand, config.ControllerBindings.WristMenu.Value));
        if (_toggle.Pressed || ((cockpit || recoveryMap) && TrackedControllers.Available && Keyboard.current?.f9Key.wasPressedThisFrame == true))
        {
            // Outside a usable cockpit, View offers the recovery map instead
            // of a cockpit-only setup menu which cannot stay open there.
            if (recoveryMap) { ControllerActions.ToggleMap(); return; }
            if (IsOpen) Close(); else Open();
        }
        if (!IsOpen) return;
        if (_backButton != null) _backButton.interactable = _history?.CanBack == true;
        foreach (var item in _liveLabels)
            if (item.label != null && item.label.gameObject.activeInHierarchy)
            { var text = item.text(); if (item.label.text != text) item.label.text = text; }
        if (_hoverButton != null && _flightPage?.activeSelf == true)
            _hoverButton.interactable = GameManager.GetLocalAircraft(out var hoverAircraft) &&
                HoverAssist.UnavailableReason(hoverAircraft) == null;
        if (_assistButton != null && _flightPage?.activeSelf == true)
            _assistButton.interactable = GameManager.GetLocalAircraft(out var assistAircraft) &&
                assistAircraft.GetControlsFilter()?.HasFlightAssist() == true;
        if (_controlsInfo != null && _controlsPage?.activeSelf == true)
        {
            var motion = config.MotionControls;
            var travelLabel = CollectiveControls ? "Collective lift" : "Throttle travel";
            var direction = CollectiveControls ? "UP increases" : "FORWARD increases";
            _controlsInfo.text = $"Higher sensitivity = less wrist tilt; yaw unchanged.\nTwist {motion.YawAngle.Value:0}° • Deadzone {motion.Deadzone.Value * 100:0}% • {travelLabel} {motion.ThrottleTravel.Value * 100:0.#} cm\n{direction} • Pedals: turn both yaw options OFF.";
        }
        if (Time.unscaledTime >= _nextHelpRefresh)
        {
            if (_bindingHelp != null && _helpPage?.activeSelf == true)
            {
                _bindingHelp.text = HandHelp(true); _throttleHelp!.text = HandHelp(false);
                var stickLeft = config.MotionControls.LeftHanded.Value;
                var b = config.ControllerBindings;
                _helpFooter!.text = "Combat buttons need a held or latched handle.\n" +
                    "Map (free hand): " + ControllerButtonNames.Describe(b.Map.Value, stickLeft, TrackedControllers.NativeFrame(stickLeft)) +
                    " · View: " + ControllerButtonNames.Describe(b.WristMenu.Value, !stickLeft, TrackedControllers.NativeFrame(!stickLeft)) +
                    "\n" + (b.RightBumperZoom.Value ? "Right bumper: hold magnifier, with either grip released or held." : "Right bumper magnifier is OFF.");
            }
            if (_bindingsInfo != null && _bindingsPage?.activeSelf == true) _bindingsInfo.text = ThrottleBindingsHelp();
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
        if (camera == null) { Close(); return; }
        var trackingRoot = camera.transform.parent;
        var followHand = config.MotionControls.ViewMenuFollowsHand.Value;
        if (_followHand != followHand) { _followHand = followHand; _anchored = false; }
        if (!_anchored || followHand && hand.Valid && VrUiCursor.I?.PrimaryHeld != true)
        {
            Vector3 position; Quaternion rotation;
            if (followHand && hand.Valid)
            {
                position = hand.Position + new Vector3(0, .19f, .16f);
                if (trackingRoot != null) position = trackingRoot.TransformPoint(position);
                var forward = position - camera.transform.position;
                rotation = forward.sqrMagnitude > .01f ? Quaternion.LookRotation(forward, camera.transform.up) : camera.transform.rotation;
            }
            else
            {
                var up = trackingRoot != null ? trackingRoot.up : Vector3.up;
                var forward = Vector3.ProjectOnPlane(camera.transform.forward, up);
                if (forward.sqrMagnitude < .01f) forward = trackingRoot != null ? trackingRoot.forward : Vector3.forward;
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
                (CockpitLayout.Active ? "Layout preview · Save or cancel when finished" : _consolePage?.activeSelf == true ? "Outline preview · Close View to see displays" : "Either hand: point + trigger · View closes this menu");
            if (_status.text != status) _status.text = status;
        }
    }

    private void Open()
    {
        if (DynamicMap.mapMaximized) SceneSingleton<DynamicMap>.i?.Minimize();
        if (!GameManager.GetLocalAircraft(out _openAircraft)) return;
        EnsurePanel();
        Home();
        _panel!.SetActive(true);
        _anchored = false;
        CursorManager.SetFlag(WristCursorFlag, true);
        MotionFlightController.Instance?.ResetGrips();
    }

    internal static void Close()
    {
        if (_instance?._resetPage?.activeSelf == true) _instance.ShowSetupPage();
        if (_instance?._ejectPage?.activeSelf == true) _instance.ShowFlightPage();
        if (_instance != null) _instance._ejectAircraft = null;
        if (_instance?._panel != null) _instance._panel.SetActive(false);
        CursorManager.SetFlag(WristCursorFlag, false);
        MotionFlightController.Instance?.ResetGrips();
    }

    private void ShowPage(GameObject page)
    {
        _history!.Push(page); RefreshPage();
    }
    private void Back() { _history!.Back(); RefreshPage(); }
    private void Home() { _history!.Home(); RefreshPage(); }
    private void RefreshPage()
    {
        foreach (var page in _pageTitles.Keys) page.SetActive(page == _history!.Current);
        if (_history!.Current != _ejectPage) _ejectAircraft = null;
        if (_history.Current != _resetPage) _resetAircraft = null;
        _pageTitle = string.Join(" / ", _history.Trail.Select(page => _pageTitles[page]));
        _nextHelpRefresh = 0;
    }
    private void ShowFlightPage() { Home(); ShowPage(_flightPage!); }
    private void ShowSetupPage() { Home(); ShowPage(_setupPage!); }
    private void AskReset()
    {
        CockpitLayout.Instance?.Finish(false);
        if (!GameManager.GetLocalAircraft(out _resetAircraft)) return;
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
        if (!GameManager.GetLocalAircraft(out _ejectAircraft)) return;
        ShowPage(_ejectPage!);
    }

    private void ConfirmEject()
    {
        if (GameManager.GetLocalAircraft(out var aircraft) && aircraft == _ejectAircraft)
            ControllerActions.Instance?.QueueWristAction("Eject");
        Close();
    }

    private void EnsurePanel()
    {
        if (_panel != null) return;
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _panel = new GameObject("NOVR Wrist Panel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        _panel.transform.SetParent(transform, false);
        _panel.transform.localScale = Vector3.one * 0.0009f;
        var rect = (RectTransform)_panel.transform;
        rect.sizeDelta = new Vector2(540, 660);
        var canvas = _panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = APIBus.CockpitHudCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;
        var background = _panel.AddComponent<Image>();
        background.color = new Color(0.025f, 0.045f, 0.055f, 1f);
        _status = Label(rect, "", new Vector2(0, 265), new Vector2(510, 105), 19);
        var config = ModConfiguration.Instance;
        var motion = config.MotionControls;
        _homePage = Page("VIEW");
        AddButton("Flight actions", 0, 0, () => ShowPage(_flightPage!));
        AddButton("Cockpit setup", 1, 0, () => ShowPage(_setupPage!));
        AddButton("Flight controls", 0, 1, () => ShowPage(_controlsPage!));
        AddButton("Comfort & display", 1, 1, () => ShowPage(_comfortPage!));
        AddButton("Controller guide", 0, 2, () => ShowPage(_helpPage!));
        AddButton("Recenter view", 1, 2, () => { NOVRHeadsetData.CalibrateTranslation(); NOVRHeadsetData.CalibrateRotation(); Close(); });
        AddLiveButton(() => "EOTS HUD: " + (config.OpticalHud.Enabled.Value ? "ON" : "OFF"), 0, 3, () => { OpticalTargetingHud.Toggle(); Close(); });
        AddButton("EOTS setup", 1, 3, () => ShowPage(_opticalPage!));
        AddLiveButton(() => CockpitLayout.Active ? "Save layout" : Fs20NativeCockpit.Selected ? "Edit handle layout" : "Edit cockpit layout", 0, 4, ToggleLayout);
        AddLiveButton(() => CockpitLayout.Active ? "Cancel layout" : "Motion: " + (motion.Enabled.Value ? "ON" : "OFF"), 1, 4, () =>
        {
            if (CockpitLayout.Active) { CockpitLayout.Instance?.Finish(false); Close(); }
            else motion.Enabled.Value = !motion.Enabled.Value;
        });
        Label(_homePage.transform, "FLY · weapons, gear, radar and hover\nSET UP · seat, panel and handle placement\nTUNE · response, hands, HUD and feedback", new Vector2(0,-184), new Vector2(510,108), 19);

        _setupPage = Page("COCKPIT");
        AddButton("Seat position", 0, 0, () => ShowPage(_seatPage!));
        AddLiveButton(() => Fs20NativeCockpit.Selected ? "FS-20 cockpit" : "Console position", 1, 0,
            () => ShowPage(Fs20NativeCockpit.Selected ? _nativeCockpitPage! : _consolePage!));
        AddLiveButton(() => CockpitLayout.Active ? "Save layout" : Fs20NativeCockpit.Selected ? "Edit handle layout" : "Edit cockpit layout", 0, 1, ToggleLayout);
        AddLiveButton(() => CockpitLayout.Active ? "Cancel layout" : "Place handles (3s)", 1, 1, () =>
        {
            if (CockpitLayout.Active) CockpitLayout.Instance?.Finish(false);
            else { motion.Enabled.Value = true; MotionFlightController.Instance?.SchedulePlacement(); }
            Close();
        });
        AddLiveButton(() => "Stick hand: " + (motion.LeftHanded.Value ? "LEFT" : "RIGHT"), 0, 2,
            () => { CockpitLayout.Instance?.Finish(false); motion.LeftHanded.Value = !motion.LeftHanded.Value; Close(); });
        AddButton("Reset seat & handles…", 1, 2, AskReset);
        Label(_setupPage.transform, "Edit layout: grip each item to move it.\nReopen View to save or cancel.\n\nSeat movement leaves handles and panel in place.\nPlacements are saved for this aircraft.", new Vector2(0,-115), new Vector2(510,220), 20);

        _seatPage = Page("SEAT");
        AddButton("Seat up", 0, 0, () => CockpitProfiles.Instance?.MoveSeat(Vector3.up * .02f));
        AddButton("Seat down", 1, 0, () => CockpitProfiles.Instance?.MoveSeat(Vector3.down * .02f));
        AddButton("Seat left", 0, 1, () => CockpitProfiles.Instance?.MoveSeat(Vector3.left * .02f));
        AddButton("Seat right", 1, 1, () => CockpitProfiles.Instance?.MoveSeat(Vector3.right * .02f));
        AddButton("Seat forward", 0, 2, () => CockpitProfiles.Instance?.MoveSeat(Vector3.forward * .02f));
        AddButton("Seat back", 1, 2, () => CockpitProfiles.Instance?.MoveSeat(Vector3.back * .02f));
        Label(_seatPage.transform, "Each step moves the viewpoint 2 cm.\nHandles and console keep their cockpit positions.", new Vector2(0,-92), new Vector2(510,140), 20);

        _resetPage = Page("RESET SETUP");
        Label(_resetPage.transform, "Restore the default seat, stick and throttle\nfor this aircraft and handedness?\n\nYour saved handle positions will be replaced.\nThe control panel keeps its current placement.", new Vector2(0,45), new Vector2(510,180), 21);
        AddButton("Keep my setup", 0, 4, Back);
        AddButton("Reset seat & handles", 1, 4, ConfirmReset);

        _flightPage = Page("FLIGHT");
        AddButton("Next weapon", 0, 0, () => ControllerActions.Instance?.QueueWristAction("Next Weapon"));
        AddButton("Next countermeasure", 1, 0, () => ControllerActions.Instance?.QueueWristAction("Next Countermeasure"));
        AddButton("Unlock last target", 0, 1, () => { SceneSingleton<CombatHUD>.i?.DeselectLast(); Close(); });
        AddButton("Clear targets", 1, 1, () => { SceneSingleton<CombatHUD>.i?.DeselectAll(); Close(); });
        AddButton("Toggle gear", 0, 2, () => ControllerActions.Instance?.QueueWristAction("Gear"));
        AddButton("Toggle radar", 1, 2, () => ControllerActions.Instance?.QueueWristAction("Radar"));
        AddButton("Toggle engine", 0, 3, () => ControllerActions.Instance?.QueueWristAction("Toggle Engine"));
        AddButton("Tactical map", 1, 3, ControllerActions.ToggleMap);
        var assistLabel = AddButton("Flight assist", 0, 4, () => ControllerActions.Instance?.QueueWristAction("Flight Assist"));
        _assistButton = assistLabel.transform.parent.GetComponent<Button>();
        _liveLabels.Add((assistLabel, () => GameManager.GetLocalAircraft(out var a) && a.GetControlsFilter()?.HasFlightAssist() == true ? "Toggle flight assist" : "Assist: not switchable"));
        var hoverLabel = AddButton("Hover assist", 1, 4, () => ControllerActions.Instance?.QueueWristAction("Auto Hover"));
        _hoverButton = hoverLabel.transform.parent.GetComponent<Button>();
        _liveLabels.Add((hoverLabel, () => !GameManager.GetLocalAircraft(out var a) || a.GetControlsFilter()?.HasAutoHover() != true
            ? "Hover: not fitted" : a.radarAlt < 1f ? "Hover: ground locked" : $"Hover assist: {(a.IsAutoHoverEnabled() ? "ON" : "OFF")}"));
        AddButton("Eject…", 0, 6, AskToEject);
        _ejectPage = Page("EJECT");
        Label(_ejectPage.transform, "Eject from this aircraft?\nThis starts the game's ejection sequence.", new Vector2(0,65), new Vector2(510,120), 24);
        AddButton("Cancel", 0, 4, Back);
        AddButton("Confirm eject", 1, 4, ConfirmEject);

        _consolePage = Page("CONSOLE");
        AddLiveButton(() => "Console: " + (config.CockpitPanelEnabled.Value ? "ON" : "OFF"), 0, 0, () => { CockpitLayout.Instance?.Finish(false); config.CockpitPanelEnabled.Value = !config.CockpitPanelEnabled.Value; });
        AddButton("Anchor to dashboard", 1, 0, () => { CockpitPanelPlacement.Instance?.ResetToDashboard(); config.CockpitPanelEnabled.Value = true; });
        AddButton("Panel up", 0, 1, () => MoveConsole(Vector3.up * .02f));
        AddButton("Panel down", 1, 1, () => MoveConsole(Vector3.down * .02f));
        AddButton("Panel left", 0, 2, () => MoveConsole(Vector3.left * .02f));
        AddButton("Panel right", 1, 2, () => MoveConsole(Vector3.right * .02f));
        AddButton("Panel nearer", 0, 3, () => MoveConsole(Vector3.back * .02f));
        AddButton("Panel farther", 1, 3, () => MoveConsole(Vector3.forward * .02f));
        AddButton("Tilt up", 0, 4, () => CockpitPanelPlacement.Adjust(Vector3.zero, tilt: 5));
        AddButton("Tilt down", 1, 4, () => CockpitPanelPlacement.Adjust(Vector3.zero, tilt: -5));
        AddButton("Panel smaller", 0, 5, () => CockpitPanelPlacement.Adjust(Vector3.zero, resize: -.05f));
        AddButton("Panel larger", 1, 5, () => CockpitPanelPlacement.Adjust(Vector3.zero, resize: .05f));
        AddLiveButton(() => CockpitLayout.Active ? "Save layout" : "Grab to position", 0, 6, ToggleLayout);
        var placementInfo = Label(_consolePage.transform, "", new Vector2(0, -223), new Vector2(510, 65), 16);
        _liveLabels.Add((placementInfo, () => CockpitPanelPlacement.Status + "\nPanel placement is saved per aircraft.\nSeat movement leaves panel and handles in place."));

        _nativeCockpitPage = Page("FS-20 NATIVE COCKPIT");
        AddLiveButton(() => "Interactions: " + (config.Fs20NativeCockpitEnabled.Value ? "ON" : "OFF"), 0, 0,
            () => config.Fs20NativeCockpitEnabled.Value = !config.Fs20NativeCockpitEnabled.Value);
        AddButton("Flight actions", 1, 0, () => ShowPage(_flightPage!));
        Label(_nativeCockpitPage.transform,
            "Physical controls · fixed to the aircraft\nPointing finger: touch a key to press it\nSwitches: reach, grip, then flick up/down\n\nEach MFD: separate PREV and NEXT buttons\nLeft MFD starts on minimap · MAP PAN slaves stick\nGear · hold BRAKE · radar · EOTS HUD\n\nSide panels: unlock, move handle, press to lock\nLeft side arrows: move seat by 2 cm per press\nSeat movement leaves the controls in place\n\nEject: grip a striped seat handle and pull UP\nCockpit stays visible while adjusting layout.",
            new Vector2(0,-48), new Vector2(510,350), 18);

        _controlsPage = Page("CONTROLS");
        AddLiveButton(() => "Stick grip: " + (motion.StickTapGrip.Value ? "TAP" : "HOLD"), 0, 0,
            () => { motion.StickTapGrip.Value = !motion.StickTapGrip.Value; MotionFlightController.Instance?.ResetGrips(); });
        AddLiveButton(() => (CollectiveControls ? "Collective: " : "Throttle grip: ") + (motion.ThrottleTapGrip.Value ? "TAP" : "HOLD"), 1, 0,
            () => { motion.ThrottleTapGrip.Value = !motion.ThrottleTapGrip.Value; MotionFlightController.Instance?.ResetGrips(); });
        StickSensitivitySlider.Create(_controlsPage.transform, new Vector2(0, 120), 510, _font!, 18);
        AddButton("Deadzone −", 0, 2, () => motion.Deadzone.Value -= .01f);
        AddButton("Deadzone +", 1, 2, () => motion.Deadzone.Value += .01f);
        AddLiveButton(() => CollectiveControls ? "Shorter lift (1 cm)" : "Shorter travel (1 cm)", 0, 3, () => motion.ThrottleTravel.Value -= .01f);
        AddLiveButton(() => CollectiveControls ? "Longer lift (1 cm)" : "Longer travel (1 cm)", 1, 3, () => motion.ThrottleTravel.Value += .01f);
        AddLiveButton(() => "Twist yaw: " + (motion.TwistYaw.Value ? "ON" : "OFF"), 0, 4,
            () => { motion.TwistYaw.Value = !motion.TwistYaw.Value; MotionFlightController.Instance?.ResetGrips(); });
        AddLiveButton(() => "Thumbstick yaw: " + (motion.ThumbstickYaw.Value ? "ON" : "OFF"), 1, 4, () => motion.ThumbstickYaw.Value = !motion.ThumbstickYaw.Value);
        AddLiveButton(() => "MIL detent: " + (motion.MilDetent.Value ? "ON" : "OFF"), 0, 5, () => motion.MilDetent.Value = !motion.MilDetent.Value);
        AddButton("Reset response", 1, 5, () => { motion.StickAngle.Value = 25; motion.YawAngle.Value = 30; motion.Deadzone.Value = .06f; motion.ResponseExponent.Value = 1.5f; motion.ThrottleTravel.Value = .12f; });
        AddButton("Button bindings", 0, 6, () => ShowPage(_bindingsPage!));
        AddButton("Controller guide", 1, 6, () => ShowPage(_helpPage!));
        _controlsInfo = Label(_controlsPage.transform, "", new Vector2(0,-225), new Vector2(510,65), 16);
        _bindingsPage = Page("BINDINGS");
        AddButton("Trigger: CMs", 0, 0, () => { config.ControllerBindings.ApplyThrottleLayout(ThrottleBindingLayout.CountermeasureTrigger); _nextHelpRefresh = 0; });
        AddButton("Trigger: brakes", 1, 0, () => { config.ControllerBindings.ApplyThrottleLayout(ThrottleBindingLayout.BrakeTrigger); _nextHelpRefresh = 0; });
        AddButton("VTOL-style controls", 0, 1, () => { config.ControllerBindings.ApplyVtolStyle(); _nextHelpRefresh = 0; });
        AddButton("Controller guide", 1, 1, () => ShowPage(_helpPage!));
        _bindingsInfo = Label(_bindingsPage.transform, "", new Vector2(0,-61), new Vector2(510,310), 17);
        _helpPage = Page("GUIDE");
        _bindingHelp = Label(_helpPage.transform, "", new Vector2(-132, 12), new Vector2(248, 390), 18);
        _throttleHelp = Label(_helpPage.transform, "", new Vector2(132, 12), new Vector2(248, 390), 18);
        _helpFooter = Label(_helpPage.transform, "", new Vector2(0, -217), new Vector2(510, 77), 16);

        _comfortPage = Page("COMFORT");
        AddButton("Hands & arms", 0, 0, () => ShowPage(_handsPage!));
        AddButton("HUD & display", 1, 0, () => ShowPage(_displayPage!));
        AddButton("Sound & haptics", 0, 1, () => ShowPage(_feedbackPage!));
        AddButton("VR performance", 0, 2, () => ShowPage(_performancePage!));
        AddLiveButton(() => "Brow NV: " + (motion.HelmetNightVision.Value ? "ON" : "OFF"), 1, 1, () => motion.HelmetNightVision.Value = !motion.HelmetNightVision.Value);
        AddLiveButton(() => "Menu: " + (motion.ViewMenuFollowsHand.Value ? "FOLLOW HAND" : "STEADY"), 0, 3, () => motion.ViewMenuFollowsHand.Value = !motion.ViewMenuFollowsHand.Value);
        AddButton("Bring menu forward", 1, 3, () => _anchored = false);
        Label(_comfortPage.transform, "Either controller can point and click.\n\nBrow NV: raise a free hand to your forehead,\nthen press its trigger to toggle night vision.", new Vector2(0,-149), new Vector2(510,160), 20);

        _handsPage = Page("HANDS");
        AddLiveButton(() => "Gloves: " + (motion.RiggedGloves.Value ? "ON" : "OFF"), 0, 0, () => motion.RiggedGloves.Value = !motion.RiggedGloves.Value);
        AddLiveButton(() => "Sleeves: " + (motion.PilotArms.Value ? "ON" : "OFF"), 1, 0, () => motion.PilotArms.Value = !motion.PilotArms.Value);
        AddButton("Slimmer sleeves", 0, 1, () => motion.SleeveThickness.Value -= .05f);
        AddButton("Wider sleeves", 1, 1, () => motion.SleeveThickness.Value += .05f);
        AddButton("Fit left hand", 0, 2, () => { _fittingLeftHand = true; ShowPage(_alignmentPage!); });
        AddButton("Fit right hand", 1, 2, () => { _fittingLeftHand = false; ShowPage(_alignmentPage!); });
        var handInfo = Label(_handsPage.transform, "", new Vector2(0,-97), new Vector2(510,235), 20);
        _liveLabels.Add((handInfo, () => $"Sleeve width: {motion.SleeveThickness.Value*100:0}%\n\nFree hands follow your controllers.\nHeld hands wrap around and follow the handle.\nFit each free hand independently.\n\nSleeves soften near their reach limit."));

        _alignmentPage = Page("HAND FIT");
        AddButton("Position", 0, 0, () => ShowPage(_handPositionPage!));
        AddButton("Wrist & palm angle", 1, 0, () => ShowPage(_handAnglesPage!));
        AddLiveButton(() => _fittingLeftHand ? "Switch to right hand" : "Switch to left hand", 0, 1, () => _fittingLeftHand = !_fittingLeftHand);
        AddButton("Reset this hand", 1, 1, () => motion.ResetFreeHand(_fittingLeftHand));
        var alignmentInfo = Label(_alignmentPage.transform, "", new Vector2(0,-64), new Vector2(510,285), 20);
        _liveLabels.Add((alignmentInfo, () => $"{FittedHand}\n\nHold this controller comfortably, thumb up.\nUse the other controller to adjust its glove.\nMatch the wrist first, then the fingertips.\n\nReset restores your previous shared fit.\nChanges save automatically for this hand.\nHeld grips and flight inputs stay fixed."));

        _handPositionPage = Page("POSITION");
        AddButton("Hand left", 0, 0, () => motion.MoveFreeHand(_fittingLeftHand, Vector3.left * .005f));
        AddButton("Hand right", 1, 0, () => motion.MoveFreeHand(_fittingLeftHand, Vector3.right * .005f));
        AddButton("Hand lower", 0, 1, () => motion.MoveFreeHand(_fittingLeftHand, Vector3.down * .005f));
        AddButton("Hand higher", 1, 1, () => motion.MoveFreeHand(_fittingLeftHand, Vector3.up * .005f));
        AddButton("Hand nearer", 0, 2, () => motion.MoveFreeHand(_fittingLeftHand, Vector3.back * .005f));
        AddButton("Hand farther", 1, 2, () => motion.MoveFreeHand(_fittingLeftHand, Vector3.forward * .005f));
        AddButton("Wrist & palm angle", 0, 3, () => { Back(); ShowPage(_handAnglesPage!); });
        AddButton("Reset position", 1, 3, () => (_fittingLeftHand ? motion.LeftGloveOffset : motion.RightGloveOffset).Value = Vector3.zero);
        var handPositionInfo = Label(_handPositionPage.transform, "", new Vector2(0,-130), new Vector2(510,190), 19);
        _liveLabels.Add((handPositionInfo, () => {
            var p = motion.FreeHandOffset(_fittingLeftHand) * 100;
            return $"{FittedHand} · 5 mm per press\nLeft/right {p.x:0.#} · Up/down {p.y:0.#} cm\nForward/back {p.z:0.#} cm\n\nDirections follow the upright controller.\nPhysical touch follows the visible fingertip.\nMenu pointer and handle placement stay fixed.";
        }));

        _handAnglesPage = Page("WRIST & PALM");
        AddButton("Palm upright", 0, 0, () => motion.SetFreeHandPalm(_fittingLeftHand, true));
        AddButton("Palm down", 1, 0, () => motion.SetFreeHandPalm(_fittingLeftHand, false));
        AddButton("Wrist tips down", 0, 1, () => motion.TurnFreeHand(_fittingLeftHand, Vector3.right * 5));
        AddButton("Wrist tips up", 1, 1, () => motion.TurnFreeHand(_fittingLeftHand, Vector3.left * 5));
        AddButton("Turn left", 0, 2, () => motion.TurnFreeHand(_fittingLeftHand, Vector3.down * 5));
        AddButton("Turn right", 1, 2, () => motion.TurnFreeHand(_fittingLeftHand, Vector3.up * 5));
        AddButton("Palm roll −", 0, 3, () => motion.TurnFreeHand(_fittingLeftHand, Vector3.back * 5));
        AddButton("Palm roll +", 1, 3, () => motion.TurnFreeHand(_fittingLeftHand, Vector3.forward * 5));
        AddButton("Position", 0, 4, () => { Back(); ShowPage(_handPositionPage!); });
        AddButton("Reset angle", 1, 4, () => (_fittingLeftHand ? motion.LeftGloveRotation : motion.RightGloveRotation).Value = Vector3.zero);
        var handAngleInfo = Label(_handAnglesPage.transform, "", new Vector2(0,-172), new Vector2(510,140), 19);
        _liveLabels.Add((handAngleInfo, () => {
            var a = motion.FreeHandAngles(_fittingLeftHand);
            return $"{FittedHand} · 5° per press\nTilt {a.x:0}° · Turn {a.y:0}° · Roll {a.z:0}°\n\nPalms upright = thumbs up, like a handshake.\nGrip a handle to use its fitted pose.";
        }));

        _displayPage = Page("DISPLAY");
        AddButton("All HUD smaller", 0, 0, () => ResizeHud(-.04f));
        AddButton("All HUD larger", 1, 0, () => ResizeHud(.04f));
        AddButton("HUD nearer", 0, 1, () => config.HudPanelDistance.Value -= .5f);
        AddButton("HUD farther", 1, 1, () => config.HudPanelDistance.Value += .5f);
        AddLiveButton(() => "Backplates: " + (config.HudContrast.Value ? "ON" : "OFF"), 0, 2, () => config.HudContrast.Value = !config.HudContrast.Value);
        AddLiveButton(() => "Declutter: " + (config.HudDeclutter.Value ? "ON" : "OFF"), 1, 2, () => config.HudDeclutter.Value = !config.HudDeclutter.Value);
        AddButton("Brightness", 0, 3, () => ShowPage(_brightnessPage!));
        AddButton("Aiming & markers", 1, 3, () => ShowPage(_aimingPage!));
        AddButton("EOTS helmet view", 1, 4, () => ShowPage(_opticalPage!));
        AddButton("Reset HUD", 0, 4, () => { config.FocusedFlightHud.Value = true; config.FlightHudScale.Value = .38f; config.MarkerScale.Value = .45f; config.MarkerLabelScale.Value = .55f; config.ReticleScale.Value = .45f; config.WeaponHudScale.Value = .5f; config.HudPanelScale.Value = 1.15f; config.HudPanelDistance.Value = 4; config.HudContrast.Value = true; config.HudDeclutter.Value = false; });
        AddButton("Threats & feed", 0, 5, () => ShowPage(_alertsPage!));
        AddButton("Inspection zoom", 1, 5, () => ShowPage(_zoomPage!));
        var displayInfo = Label(_displayPage.transform, "", new Vector2(0,-210), new Vector2(510,76), 18);
        _liveLabels.Add((displayInfo, () => $"Flight {config.FlightHudScale.Value*100:0}% • Symbols {config.MarkerScale.Value*100:0}%\nStatus-panel depth {config.HudPanelDistance.Value:0.0} m\nDepth changes preserve apparent size."));

        _zoomPage = Page("INSPECTION ZOOM");
        var zoom = config.InspectionZoom;
        AddLiveButton(() => "Image: " + InspectionZoomSettings.ResolutionLabels[zoom.Resolution.Value], 0, 0,
            () => zoom.Resolution.Value = (zoom.Resolution.Value + 1) % InspectionZoomSettings.ResolutionLabels.Length);
        AddLiveButton(() => "Edges: " + InspectionZoomSettings.AaLabels[zoom.AntiAliasing.Value], 1, 0,
            () => zoom.AntiAliasing.Value = (zoom.AntiAliasing.Value + 1) % InspectionZoomSettings.AaLabels.Length);
        AddLiveButton(() => "Scene effects: " + (zoom.SceneEffects.Value ? "ON" : "OFF"), 0, 1,
            () => zoom.SceneEffects.Value = !zoom.SceneEffects.Value);
        AddButton("Reset zoom quality", 1, 1, zoom.Reset);
        AddButton("Less magnification", 0, 2, () => config.ControllerBindings.ViewZoomFactor.Value -= .1f);
        AddButton("More magnification", 1, 2, () => config.ControllerBindings.ViewZoomFactor.Value += .1f);
        var zoomInfo = Label(_zoomPage.transform, "", new Vector2(0,-100), new Vector2(510,245), 18);
        _liveLabels.Add((zoomInfo, () => $"Right bumper · {config.ControllerBindings.ViewZoomFactor.Value:0.0}× magnification\nOnly the inspection window changes.\nHigher resolution and smoothing cost GPU time\nwhile held. Scene effects are optional.\n\nMSAA requires main Anti-Aliasing enabled.\nUnsupported smoothing falls back automatically.\nChanges apply the next time you hold RB.\nHeadset resolution and eye separation stay fixed."));

        _alertsPage = Page("THREATS & FEED");
        var alerts = config.CockpitAlerts;
        AddLiveButton(() => "Compact: " + (alerts.Enabled.Value ? "ON" : "OFF"), 0, 0, () => alerts.Enabled.Value = !alerts.Enabled.Value);
        AddLiveButton(() => "Kill feed: " + (alerts.Feed.Value ? "ON" : "OFF"), 1, 0, () => alerts.Feed.Value = !alerts.Feed.Value);
        AddButton("Smaller alerts", 0, 1, () => alerts.Scale.Value -= .05f);
        AddButton("Larger alerts", 1, 1, () => alerts.Scale.Value += .05f);
        AddButton("Alerts lower", 0, 2, () => alerts.Height.Value -= .025f);
        AddButton("Alerts higher", 1, 2, () => alerts.Height.Value += .025f);
        AddLiveButton(() => "World notch: " + (alerts.WorldNotch.Value ? "ON" : "OFF"), 0, 3, () => alerts.WorldNotch.Value = !alerts.WorldNotch.Value);
        AddButton("Reset alerts", 1, 3, () => { alerts.Enabled.Value = true; alerts.Feed.Value = true; alerts.Scale.Value = 1; alerts.Height.Value = -.30f; alerts.WorldNotch.Value = true; });
        var alertInfo = Label(_alertsPage.transform, "", new Vector2(0, -150), new Vector2(510, 190), 18);
        _liveLabels.Add((alertInfo, () => $"Size {alerts.Scale.Value*100:0}% · Height {alerts.Height.Value:0.00} m\nM = missile · R = radar · J = jamming\n12h is the aircraft nose; 6h is behind.\nNOTCH is an evasion direction, not a threat bearing.\nQuiet scope disappears. Latest 3 feed entries only.\nClose View to inspect placement."));

        _opticalPage = Page("EOTS HELMET VIEW");
        AddLiveButton(() => "EOTS HUD: " + (config.OpticalHud.Enabled.Value ? "ON" : "OFF"), 0, 0, OpticalTargetingHud.Toggle);
        AddButton("Look to aim", 1, 0, () => { config.OpticalHud.Enabled.Value = true; OpticalTargetingHud.LookToAim(); Close(); });
        AddButton("Less magnification", 0, 1, () => config.OpticalHud.Magnification.Value -= .5f);
        AddButton("More magnification", 1, 1, () => config.OpticalHud.Magnification.Value += .5f);
        AddButton("Smaller image", 0, 2, () => config.OpticalHud.Width.Value -= 2);
        AddButton("Larger image", 1, 2, () => config.OpticalHud.Width.Value += 2);
        AddButton("Reset optical size", 0, 3, () => { config.OpticalHud.Width.Value = 20; config.OpticalHud.Magnification.Value = 2; });
        var opticalInfo = Label(_opticalPage.transform, "", new Vector2(0, -163), new Vector2(510, 155), 18);
        _liveLabels.Add((opticalInfo, () => $"{OpticalTargetingHud.ModeLabel} · Head zoom {config.OpticalHud.Magnification.Value:0.#}× · Width {config.OpticalHud.Width.Value:0}°\nLook with your head, then click target-select\nwhile gripping its assigned handle.\nUnlock returns to head aiming.\nTRACK uses the aircraft's automatic camera zoom.\nRight bumper temporarily shows the magnifier."));

        _aimingPage = Page("AIMING & MARKERS");
        AddButton("Symbols smaller", 0, 0, () => config.MarkerScale.Value -= .05f);
        AddButton("Symbols larger", 1, 0, () => config.MarkerScale.Value += .05f);
        AddButton("Labels smaller", 0, 1, () => config.MarkerLabelScale.Value -= .05f);
        AddButton("Labels larger", 1, 1, () => config.MarkerLabelScale.Value += .05f);
        AddButton("Reticle smaller", 0, 2, () => config.ReticleScale.Value -= .05f);
        AddButton("Reticle larger", 1, 2, () => config.ReticleScale.Value += .05f);
        AddButton("Range UI smaller", 0, 3, () => config.WeaponHudScale.Value -= .05f);
        AddButton("Range UI larger", 1, 3, () => config.WeaponHudScale.Value += .05f);
        AddLiveButton(() => "Aim cue: " + config.TargetCue.Value.ToString().ToUpperInvariant(), 0, 4,
            () => config.TargetCue.Value = (TargetCueMode)(((int)config.TargetCue.Value + 1) % 3));
        var aimingInfo = Label(_aimingPage.transform, "", new Vector2(0, -175), new Vector2(510, 100), 19);
        _liveLabels.Add((aimingInfo, () => $"Symbols {config.MarkerScale.Value*100:0}% • Labels {config.MarkerLabelScale.Value*100:0}%\nReticle {config.ReticleScale.Value*100:0}% • Range {config.WeaponHudScale.Value*100:0}%\nAim points stay aligned with the world."));

        _brightnessPage = Page("BRIGHTNESS");
        AddButton("MFD dimmer", 0, 0, () => config.MfdBrightness.Value -= .1f);
        AddButton("MFD brighter", 1, 0, () => config.MfdBrightness.Value += .1f);
        AddButton("HUD dimmer", 0, 1, () => config.InstrumentBrightness.Value -= .1f);
        AddButton("HUD brighter", 1, 1, () => config.InstrumentBrightness.Value += .1f);
        AddButton("Day preset", 0, 3, () => { config.InstrumentBrightness.Value = 1; config.MfdBrightness.Value = 1; });
        AddButton("Night preset", 1, 3, () => { config.InstrumentBrightness.Value = .5f; config.MfdBrightness.Value = .45f; });
        var lightInfo = Label(_brightnessPage.transform, "", new Vector2(0,-137), new Vector2(510,215), 19);
        _liveLabels.Add((lightInfo, () => $"MFD content {config.MfdBrightness.Value*100:0}%\nHUD instruments {config.InstrumentBrightness.Value*100:0}%\n\nHUD: flight instruments, compass and pitch ladder.\nTarget markers and missile alerts keep native brightness.\nMFD page buttons and weapon readout stay readable.\nClose View to inspect the displays."));

        _performancePage = Page("VR PERFORMANCE");
        var performanceInfo = Label(_performancePage.transform, "", new Vector2(0, -12), new Vector2(510, 390), 18);
        _liveLabels.Add((performanceInfo, () => PcVrCapabilities.Report));

        _feedbackPage = Page("FEEDBACK");
        AddButton("Quieter sounds", 0, 0, () => motion.InteractionVolume.Value -= .1f);
        AddButton("Louder sounds", 1, 0, () => motion.InteractionVolume.Value += .1f);
        AddButton("Weaker haptics", 0, 1, () => motion.HapticStrength.Value -= .1f);
        AddButton("Stronger haptics", 1, 1, () => motion.HapticStrength.Value += .1f);
        AddFeedbackPreview("Try touch", 0, 2, InteractionCue.Touch);
        AddFeedbackPreview("Try grab", 1, 2, InteractionCue.Grab);
        AddFeedbackPreview("Try switch", 0, 3, InteractionCue.Switch);
        AddFeedbackPreview("Try MIL detent", 1, 3, InteractionCue.Detent);
        var feedbackInfo = Label(_feedbackPage.transform, "", new Vector2(0,-133), new Vector2(510,190), 20);
        _liveLabels.Add((feedbackInfo, () => $"Sounds {motion.InteractionVolume.Value*100:0}% · Haptics {motion.HapticStrength.Value*100:0}%\n\nPoint + trigger: one sample in that hand.\nZero disables sound or haptics independently.\nSamples do not operate aircraft controls."));

        _history = new MenuHistory<GameObject>(_homePage);
        _buttonParent = rect;
        _backButton = AddButton("Back", 0, 8, Back, 3).transform.parent.GetComponent<Button>();
        AddButton("Home", 1, 8, Home, 3);
        AddButton("Close", 2, 8, Close, 3);
        Home();
        LayerHelper.SetLayerRecursive(_panel.transform, LayerHelper.GetVrUiLayer());
    }

    private static void ToggleLayout()
    {
        if (CockpitLayout.Active) { CockpitLayout.Instance?.Finish(true); Close(); }
        else CockpitLayout.Instance?.Begin();
    }

    private static void MoveConsole(Vector3 delta)
    {
        CockpitPanelPlacement.Adjust(delta);
    }

    private GameObject Page(string title)
    {
        var page = new GameObject(title, typeof(RectTransform));
        page.transform.SetParent(_panel!.transform, false);
        page.SetActive(false); _pageTitles.Add(page, title); _buttonParent = page.transform;
        return page;
    }

    private string ThrottleBindingsHelp()
    {
        var config = ModConfiguration.Instance;
        var left = !config.MotionControls.LeftHanded.Value;
        var frame = TrackedControllers.NativeFrame(left);
        var buttons = config.ControllerBindings.ThrottleButtons;
        var layout = ThrottleBindingProfiles.Identify(buttons);
        var wristConflict = ThrottleBindingProfiles.SharesButton(config.ControllerBindings.WristMenu.Value, frame,
            buttons.Brake, buttons.Countermeasures, buttons.Target, buttons.NextCountermeasure);
        string Name(ControllerButton value) => !left && config.ControllerBindings.RightBumperZoom.Value && ControllerButtonNames.Resolve(value, frame) == ControllerButton.Shoulder
            ? "Reserved for zoom" : ControllerButtonNames.Describe(value, left, frame);
        return $"{(left ? "LEFT" : "RIGHT")} HAND • THROTTLE\n" +
            "Layout: " + (layout == ThrottleBindingLayout.VtolStyle ? "VTOL-style trigger brake" : layout == ThrottleBindingLayout.CountermeasureTrigger ? "Countermeasure trigger" : layout == ThrottleBindingLayout.BrakeTrigger ? "Brake trigger" : "Custom") +
            $"\n\nBrake: {Name(buttons.Brake)} (hold)\nCountermeasures: {Name(buttons.Countermeasures)}\n" +
            (config.ControllerBindings.SelectTargetOnStick.Value ? "Target selection is on the STICK hand.\n" : $"Select target: {Name(buttons.Target)} (release)\n") +
            $"CM type: {Name(buttons.NextCountermeasure)} (release)\n\n" +
            (wristConflict ? "Wrist button overlaps; edit custom binding.\n" :
                ThrottleBindingProfiles.Conflicts(buttons, frame) ? "Shared buttons detected; choose a layout.\n" : "") +
            "Grip throttle + thumbstick: VTOL angle.\nVTOL style: stick click selects a target.\nRight bumper is reserved while zoom is enabled.";
    }

    private string HandHelp(bool stick)
    {
        var config = ModConfiguration.Instance;
        var left = stick ? config.MotionControls.LeftHanded.Value : !config.MotionControls.LeftHanded.Value;
        var b = config.ControllerBindings;
        string Name(ControllerButton button) => !left && b.RightBumperZoom.Value && ControllerButtonNames.Resolve(button, TrackedControllers.NativeFrame(left)) == ControllerButton.Shoulder
            ? "Reserved for magnifier" : ControllerButtonNames.Describe(button, left, TrackedControllers.NativeFrame(left));
        string Action(string name, ControllerButton button) => name + "\n" + Name(button) + "\n\n";
        var text = (stick ? "STICK" : CollectiveControls ? "COLLECTIVE" : "THROTTLE") + " · " + (left ? "LEFT" : "RIGHT") + "\n\n";
        if (stick)
        {
            text += Action("Guns (hold)", b.Fire.Value) + Action("Missile / weapon release", b.WeaponRelease.Value) + Action("Next weapon (release)", b.NextWeapon.Value);
            if (b.SelectTargetOnStick.Value) text += "Select: " + Name(b.SelectTarget.Value) + "\n\n";
            text += "Unlock: " + Name(b.UnlockTarget.Value) + "\n\nClear all: " + Name(b.ClearTargets.Value);
        }
        else
        {
            text += Action("Brake (hold)", b.Brake.Value) + Action("Countermeasures (hold)", b.Countermeasures.Value) + Action("CM type (release)", b.NextCountermeasure.Value);
            if (!b.SelectTargetOnStick.Value) text += Action("Select target (release)", b.SelectTarget.Value);
            text += "Grip + thumbstick\nVTOL angle, when fitted\n\nL STK on MFD\nMap pan · click to recenter";
        }
        return text.TrimEnd();
    }

    private void AddLiveButton(Func<string> text, int column, int row, Action action) =>
        _liveLabels.Add((AddButton(text(), column, row, action), text));

    private void AddFeedbackPreview(string text, int column, int row, InteractionCue cue)
    {
        var button = AddButton(text, column, row, null).transform.parent.GetComponent<Button>();
        var preview = button.gameObject.AddComponent<FeedbackPreviewButton>();
        preview.Cue = cue;
        button.onClick.AddListener(preview.Play);
    }

    private Text AddButton(string text, int column, int row, Action? action, int columns = 2)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(_buttonParent, false);
        var width = columns == 3 ? 160f : 248f;
        rect.sizeDelta = new Vector2(width, 49);
        rect.anchoredPosition = new Vector2((column - (columns - 1) * .5f) * (width + 16), 177 - row * 57);
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        if (action != null) button.onClick.AddListener(() => action());
        NativeButtonFeedback.Configure(button, new Color(0.10f, 0.24f, 0.29f));
        return Label(rect, text, Vector2.zero, new Vector2(width - 10, 45), 20);
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

    private void OnDisable() { Close(); _toggle.Reset(); }
    private void OnDestroy() { OnDisable(); if (_instance == this) _instance = null; }
}
