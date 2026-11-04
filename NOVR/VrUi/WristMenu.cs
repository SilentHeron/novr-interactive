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
    private Transform? _buttonParent;
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
        { _toggle.Reset(); _bindingVersion = bindingVersion; _menuProfile = config.ControllerBindings.EffectiveProfile; _menuLeft = menuLeft; }
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
            if (recoveryMap) { ControllerActions.ToggleMap(); return; }
            if (IsOpen) Close(); else Open();
        }
        if (!IsOpen) return;
        if (_backButton != null) _backButton.interactable = _history?.CanBack == true;
        foreach (var setting in _settingRefresh)
            if (setting.page.activeInHierarchy) setting.refresh();
        foreach (var item in _liveLabels)
            if (item.label != null && item.label.gameObject.activeInHierarchy)
            { var text = item.text(); if (item.label.text != text) item.label.text = text; }
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
                _bindingHelp.text = HandHelp(true); _throttleHelp!.text = HandHelp(false);
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
                (CockpitHudWidgets.Editing ? "Layout preview · Save or cancel when finished" : "Either hand: point + trigger · View closes this menu");
            if (_status.text != status) _status.text = status;
        }
    }

    private void Open()
    {
        if (DynamicMap.mapMaximized) SceneSingleton<DynamicMap>.i?.Minimize();
        if (!GameManager.GetLocalAircraft(out _openAircraft)) return;
        EnsurePanel();
        Home();
        if (CockpitHudWidgets.Editing) ShowLayoutControls();
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
        _pageTitle = _pageTitles[_history.Current];
        _nextHelpRefresh = 0;
    }
    private void ShowFlightPage() { Home(); ShowPage(_flightPage!); }
    private void ShowSetupPage() { Home(); ShowPage(_seatPage!); }
    private void AskReset()
    {
        CockpitHudWidgets.Instance?.Finish(false);
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
        rect.sizeDelta = new Vector2(WristMenuNavigation.PanelWidth, WristMenuNavigation.PanelHeight);
        var canvas = _panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = APIBus.CockpitHudCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;
        var background = _panel.AddComponent<Image>();
        background.color = new Color(0.025f, 0.045f, 0.055f, 1f);
        _status = Label(rect, "", new Vector2(0, 279), new Vector2(620, 81), 19);
        ViewMenuControls.Panel(rect,"Header divider",new Vector2(0,234),new Vector2(620,2),ViewMenuControls.Accent);
        ViewMenuControls.Panel(rect,"Footer divider",new Vector2(0,-252),new Vector2(620,2),ViewMenuControls.Accent);
        var config = ModConfiguration.Instance;
        var motion = config.MotionControls;
        _homePage = Page("VIEW");
        ViewMenuControls.Panel(_homePage.transform,"Flight card",new Vector2(-160,129),new Vector2(300,202),ViewMenuControls.Surface);
        ViewMenuControls.Panel(_homePage.transform,"Cockpit card",new Vector2(-160,-60),new Vector2(300,202),ViewMenuControls.Surface);
        ViewMenuControls.Panel(_homePage.transform,"Visual card",new Vector2(160,105),new Vector2(300,250),ViewMenuControls.Surface);
        ViewMenuControls.Panel(_homePage.transform,"System card",new Vector2(160,-111),new Vector2(300,202),ViewMenuControls.Surface);
        Section("FLIGHT",215,300,-160);
        Section("COCKPIT",26,300,-160);
        Section("VISUALS",215,300,160);
        Section("FEEL & SYSTEM",-23,300,160);
        WristMenuNavigation.BuildHome((item, action) =>
        {
            var label = AddButton(item.Label, item.Column, item.Row, action);
            var bounds=WristMenuNavigation.HomeBounds(item);
            var rect=(RectTransform)label.transform.parent;
            rect.anchoredPosition=new Vector2(bounds.X,bounds.Y);rect.sizeDelta=new Vector2(bounds.Width,bounds.Height);
            var labelRect=(RectTransform)label.transform;labelRect.anchoredPosition=new Vector2(-10,0);labelRect.sizeDelta=new Vector2(254,40);
            label.alignment=TextAnchor.MiddleLeft;
            NativeButtonFeedback.Configure(rect.GetComponent<Button>(),item.OpensPage?ViewMenuControls.Surface:new Color(.14f,.22f,.25f));
            if(item.OpensPage) ViewMenuControls.Text(rect,_font!,"›",new Vector2(132,0),new Vector2(24,34),26,TextAnchor.MiddleCenter);
        },
            new Dictionary<ViewMenuDestination, Action>
            {
                [ViewMenuDestination.FlightActions] = () => ShowPage(_flightPage!),
                [ViewMenuDestination.FlightControls] = () => ShowPage(_controlsPage!),
                [ViewMenuDestination.Bindings] = () => ShowPage(_bindingsPage!),
                [ViewMenuDestination.EditLayout] = StartOrResumeLayout,
                [ViewMenuDestination.Seat] = () => ShowPage(_seatPage!),
                [ViewMenuDestination.Profiles] = OpenAircraftProfiles,
                [ViewMenuDestination.Recenter] = () => { NOVRHeadsetData.CalibrateTranslation(); NOVRHeadsetData.CalibrateRotation(); Close(); },
                [ViewMenuDestination.LookCue] = () => ShowPage(_lookCuePage!),
                [ViewMenuDestination.Hud] = () => ShowPage(_displayPage!),
                [ViewMenuDestination.Widgets] = OpenWidgetAppearance,
                [ViewMenuDestination.Hands] = () => ShowPage(_handsPage!),
                [ViewMenuDestination.Feedback] = () => ShowPage(_feedbackPage!),
                [ViewMenuDestination.Performance] = () => ShowPage(_performancePage!),
                [ViewMenuDestination.MenuOptions] = () => ShowPage(_menuOptionsPage!)
            });
        ViewMenuControls.Text(_homePage.transform,_font!,"SESSION ACTION",new Vector2(-160,-166),new Vector2(300,24),16);

        _seatPage = Page("SEAT & HANDLES");
        Section("Seat · 2 cm steps",214);
        ActionButton("Seat up",0,168,()=>CockpitProfiles.Instance?.MoveSeat(Vector3.up*.02f));
        ActionButton("Seat down",1,168,()=>CockpitProfiles.Instance?.MoveSeat(Vector3.down*.02f));
        ActionButton("Seat left",0,120,()=>CockpitProfiles.Instance?.MoveSeat(Vector3.left*.02f));
        ActionButton("Seat right",1,120,()=>CockpitProfiles.Instance?.MoveSeat(Vector3.right*.02f));
        ActionButton("Seat forward",0,72,()=>CockpitProfiles.Instance?.MoveSeat(Vector3.forward*.02f));
        ActionButton("Seat back",1,72,()=>CockpitProfiles.Instance?.MoveSeat(Vector3.back*.02f));
        Section("Handles & layout",23);
        Setting("Stick in left hand",()=>motion.LeftHanded.Value,value=>{CockpitHudWidgets.Instance?.Finish(false);motion.LeftHanded.Value=value;Close();},0,-24);
        ActionButton("Edit layout",0,-74,StartOrResumeLayout);
        ActionButton("Aircraft profiles  ›",1,-74,OpenAircraftProfiles);
        ActionButton("Reset seat & handles…",0,-126,AskReset,true);
        Note("Seat adjustments leave handles and HUD fixed.\nPlacements belong to this aircraft's profile.",-198,70);

        _resetPage = Page("RESET SETUP");
        Label(_resetPage.transform, "Restore the default seat, stick and throttle\nfor this aircraft and handedness?\n\nYour saved handle positions will be replaced.\nOriginal cockpit instruments stay in place.", new Vector2(0,45), new Vector2(510,180), 21);
        AddButton("Keep my setup", 0, 4, Back);
        ActionButton("Reset seat & handles",1,-51,ConfirmReset,true);

        _flightPage = Page("FLIGHT ACTIONS");
        Section("Aircraft commands",214);
        ActionButton("Next weapon", 0, 168, () => ControllerActions.Instance?.QueueWristAction("Next Weapon"));
        ActionButton("Next countermeasure", 1, 168, () => ControllerActions.Instance?.QueueWristAction("Next Countermeasure"));
        ActionButton("Unlock last target", 0, 120, () => { SceneSingleton<CombatHUD>.i?.DeselectLast(); Close(); });
        ActionButton("Clear targets", 1, 120, () => { SceneSingleton<CombatHUD>.i?.DeselectAll(); Close(); });
        ActionButton("Toggle gear", 0, 72, () => ControllerActions.Instance?.QueueWristAction("Gear"));
        ActionButton("Toggle radar", 1, 72, () => ControllerActions.Instance?.QueueWristAction("Radar"));
        ActionButton("Toggle engine", 0, 24, () => ControllerActions.Instance?.QueueWristAction("Toggle Engine"));
        ActionButton("Tactical map", 1, 24, ControllerActions.ToggleMap);
        var assistLabel = ActionButton("Flight assist", 0, -24, () => ControllerActions.Instance?.QueueWristAction("Flight Assist"));
        _assistButton = assistLabel.transform.parent.GetComponent<Button>();
        _liveLabels.Add((assistLabel, () => GameManager.GetLocalAircraft(out var a) && a.GetControlsFilter()?.HasFlightAssist() == true ? "Toggle flight assist" : "Assist: not switchable"));
        var hoverLabel = ActionButton("Hover assist", 1, -24, () => ControllerActions.Instance?.QueueWristAction("Auto Hover"));
        _hoverButton = hoverLabel.transform.parent.GetComponent<Button>();
        _liveLabels.Add((hoverLabel, () => !GameManager.GetLocalAircraft(out var a) || a.GetControlsFilter()?.HasAutoHover() != true
            ? "Hover: not fitted" : a.radarAlt < 1f ? "Hover: ground locked" : $"Hover assist: {(a.IsAutoHoverEnabled() ? "ON" : "OFF")}"));
        Section("Emergency",-158);
        ActionButton("Eject…",0,-213,AskToEject,true);
        _ejectPage = Page("EJECT");
        Label(_ejectPage.transform, "Eject from this aircraft?\nThis starts the game's ejection sequence.", new Vector2(0,65), new Vector2(510,120), 24);
        AddButton("Cancel", 0, 4, Back);
        ActionButton("Confirm eject",1,-51,ConfirmEject,true);

        _controlsPage = Page("FLIGHT CONTROLS");
        Section("Grip & steering", 214);
        Setting("Motion controls", motion.Enabled, 0, 170);
        Setting("Twist for yaw", motion.TwistYaw, 1, 170, () => MotionFlightController.Instance?.ResetGrips());
        Setting("Toggle stick grip", motion.StickTapGrip, 0, 123, () => MotionFlightController.Instance?.ResetGrips());
        Setting("Thumbstick for yaw", motion.ThumbstickYaw, 1, 123);
        Section("Stick response", 77);
        Setting("Stick sensitivity", 23, 0, 1,
            () => Mathf.InverseLerp(MotionControlSettings.MaximumStickAngle, MotionControlSettings.MinimumStickAngle, motion.StickAngle.Value),
            value => motion.StickAngle.Value = Mathf.Round(Mathf.Lerp(MotionControlSettings.MaximumStickAngle, MotionControlSettings.MinimumStickAngle, value)*2)*.5f,
            _ => $"{MotionControlSettings.DefaultStickAngle/motion.StickAngle.Value:0.00}× / {motion.StickAngle.Value:0.#}°");
        Setting("Pitch / roll deadzone", motion.Deadzone, -45, Percent, .01f);
        Setting("Twist yaw deadzone", motion.YawDeadzone, -113, Percent, .01f);
        ActionButton("Throttle / collective  ›", 0, -175, () => ShowPage(_throttlePage!));
        ActionButton("Controller guide  ›", 1, -175, () => ShowPage(_helpPage!));
        ActionButton("Reset response", 0, -223, () => { motion.StickAngle.Value = 25; motion.YawAngle.Value = 30; motion.Deadzone.Value = .06f; motion.YawDeadzone.Value = MotionControlSettings.DefaultYawDeadzone; motion.ResponseExponent.Value = 1.5f; motion.ThrottleTravel.Value = .12f; });
        ViewMenuControls.Text(_controlsPage.transform,_font!,"Pedals: turn both yaw options off.",new Vector2(160,-223),new Vector2(300,42),16);

        _throttlePage = Page("THROTTLE / COLLECTIVE");
        Section("Handle behavior",214);
        Setting("Toggle throttle grip",motion.ThrottleTapGrip,0,168,()=>MotionFlightController.Instance?.ResetGrips());
        Setting("MIL power detent",motion.MilDetent,1,168);
        Setting("Throttle travel / collective lift",motion.ThrottleTravel,94,Centimeters,.005f);
        Note("Move forward to increase throttle; lift up for collective.\nMIL detent applies to afterburning aircraft.",19,60);
        Section("Placement",-47);
        Note("Use Edit layout to reposition the handle.\nGrip modes use one squeeze to hold, another to release.\nWith toggle grip off, keep squeezing to hold.",-113,95);
        ActionButton("Edit layout",0,-202,StartOrResumeLayout);
        CreateBindingPages();
        _helpPage = Page("GUIDE");
        _bindingHelp = Label(_helpPage.transform, "", new Vector2(-132, 12), new Vector2(248, 390), 18);
        _throttleHelp = Label(_helpPage.transform, "", new Vector2(132, 12), new Vector2(248, 390), 18);
        _helpFooter = Label(_helpPage.transform, "", new Vector2(0, -217), new Vector2(510, 77), 16);

        _menuOptionsPage = Page("MENU OPTIONS");
        Section("Menu placement",214);
        Setting("Follow View hand",motion.ViewMenuFollowsHand,0,167);
        ActionButton("Bring menu forward",1,167,()=>_anchored=false);
        Note("Off: keep this menu steady in front of your seat.\nOn: attach it to the controller assigned to View.",99,64);
        Section("Controller input",31);
        ActionButton("Controller guide  ›",0,-16,()=>ShowPage(_helpPage!));
        Note("Either controller can point and click.\nPress a button on the other controller to switch.\nView closes this menu.",-100,100);

        _handsPage = Page("HANDS & ARMS");
        Section("Pilot appearance",214);
        Setting("Show gloves",motion.RiggedGloves,0,168);
        Setting("Show sleeves",motion.PilotArms,1,168);
        Setting("Sleeve width",motion.SleeveThickness,94,Percent,.01f);
        Section("Hand fit & helmet gesture",22);
        ActionButton("Fit left hand  ›",0,-29,()=>{_fittingLeftHand=true;ShowPage(_handPositionPage!);});
        ActionButton("Fit right hand  ›",1,-29,()=>{_fittingLeftHand=false;ShowPage(_handPositionPage!);});
        Setting("Brow night vision",motion.HelmetNightVision,0,-80);
        var browHint=ViewMenuControls.Text(_handsPage.transform,_font!,"Raise a free hand to your brow,\nthen press its trigger.",new Vector2(160,-80),new Vector2(300,44),17);
        browHint.color=ViewMenuControls.Muted;
        Note("Free hands follow your controllers.\nHeld hands wrap around and follow the handle.\nFit corrections leave held grip poses unchanged.\nSleeves soften near their reach limit.",-172,112);

        _handPositionPage = Page("HAND POSITION");
        Section("Free-hand position correction",214);
        HandPositionSetting("Left / right",0,159);
        HandPositionSetting("Down / up",1,89);
        HandPositionSetting("Nearer / farther",2,19);
        Choice("Hand",new[]{"Left hand","Right hand"},()=>_fittingLeftHand?0:1,value=>_fittingLeftHand=value==0,-54);
        ActionButton("Wrist & palm angle  ›",0,-125,()=>{Back();ShowPage(_handAnglesPage!);});
        ActionButton("Reset position",1,-125,()=> (_fittingLeftHand?motion.LeftGloveOffset:motion.RightGloveOffset).Value=Vector3.zero);
        ActionButton("Reset this hand",0,-177,()=>motion.ResetFreeHand(_fittingLeftHand));
        Note("Hold thumb up; adjust with the other hand. Changes save\nautomatically. Held grips and input stay unchanged.",-223,44);

        _handAnglesPage = Page("WRIST & PALM");
        Section("Free-hand angle correction",214);
        HandAngleSetting("Wrist tilt",0,159);
        HandAngleSetting("Hand turn",1,89);
        HandAngleSetting("Palm roll",2,19);
        Choice("Palm preset",new[]{"Upright · thumbs up","Down"},()=>WristMenuSettingsActions.PalmPreset(motion.FreeHandAngles(_fittingLeftHand),_fittingLeftHand),
            value=>motion.SetFreeHandPalm(_fittingLeftHand,value==0),-54);
        Choice("Hand",new[]{"Left hand","Right hand"},()=>_fittingLeftHand?0:1,value=>_fittingLeftHand=value==0,-130);
        var positionLink=ActionButton("Position  ›",0,-213,()=>{Back();ShowPage(_handPositionPage!);});
        var angleReset=ActionButton("Reset angle",1,-213,()=> (_fittingLeftHand?motion.LeftGloveRotation:motion.RightGloveRotation).Value=Vector3.zero);
        var fullReset=ActionButton("Reset hand",0,-213,()=>motion.ResetFreeHand(_fittingLeftHand));
        CompactAction(positionLink,0,3);CompactAction(angleReset,1,3);CompactAction(fullReset,2,3);

        _displayPage = Page("HUD & MARKERS");
        Section("Flight instruments",214);
        Setting("Flight HUD size",config.FlightHudScale,159,Percent,.01f,value=>WristMenuSettingsActions.FlightHudSize(config,value));
        Setting("Instrument brightness",config.InstrumentBrightness,89,Percent,.01f);
        Setting("Status backplates",config.HudContrast,0,27);
        Setting("Declutter status",config.HudDeclutter,1,27);
        Section("Individual elements",-22);
        ActionButton("Symbols & labels  ›",0,-70,()=>ShowPage(_aimingPage!));
        ActionButton("Threats & feed  ›",1,-70,()=>ShowPage(_alertsPage!));
        ActionButton("Widget appearance  ›",0,-120,OpenWidgetAppearance);
        ActionButton("Look direction cue  ›",1,-120,()=>ShowPage(_lookCuePage!));
        ActionButton("Day · 100%",0,-168,()=>config.InstrumentBrightness.Value=1);
        ActionButton("Night · 50%",1,-168,()=>config.InstrumentBrightness.Value=.5f);
        ActionButton("Reset HUD",0,-217,()=>{config.FocusedFlightHud.Value=true;config.FlightHudScale.Value=.38f;config.MarkerScale.Value=.45f;config.MarkerLabelScale.Value=.55f;config.ReticleScale.Value=.45f;config.WeaponHudScale.Value=.5f;config.HudPanelScale.Value=1.15f;config.HudPanelDistance.Value=4;config.HudContrast.Value=true;config.HudDeclutter.Value=false;});
        ViewMenuControls.Text(_displayPage.transform,_font!,"Screen brightness stays native.",new Vector2(160,-217),new Vector2(300,42),16);

        _alertsPage = Page("THREATS & FEED");
        var alerts = config.CockpitAlerts;
        Section("Cockpit overlays",214);
        Setting("Compact alerts",alerts.Enabled,0,167);
        Setting("Kill feed",alerts.Feed,1,167);
        Setting("World notch cue",alerts.WorldNotch,0,119);
        Section("Appearance & placement",64);
        ActionButton("Widget appearance  ›",0,15,OpenWidgetAppearance);
        ActionButton("Edit layout",1,15,StartOrResumeLayout);
        Note("M = missile · R = radar · J = jamming\n12h is the aircraft nose; 6h is behind.\nNOTCH is an evasion direction, not a threat bearing.\nQuiet scope disappears. Latest 3 feed entries only.\nWidget visibility also applies.",-117,158);

        CreateHudLayoutPages();

        _aimingPage = Page("SYMBOLS & LABELS");
        Section("World-aligned HUD elements",214);
        Setting("Target & location symbols",config.MarkerScale,159,Percent,.01f);
        Setting("Marker labels",config.MarkerLabelScale,89,Percent,.01f);
        Setting("Crosshair & bomb pipper",config.ReticleScale,19,Percent,.01f);
        Setting("Weapon range readouts",config.WeaponHudScale,-51,Percent,.01f);
        Note("Size changes preserve each element's aim point.",-116,42);
        ActionButton("Look direction cue  ›",0,-185,()=>ShowPage(_lookCuePage!));

        _lookCuePage = Page("LOOK DIRECTION CUE");
        var cue = config.LookCue;
        Section("Appearance",214);
        Setting("Size",cue.Size,159,Percent,.05f);
        Setting("Opacity",cue.Opacity,89,Percent,.01f);
        Choice("Shape",new[]{"Circle","Dot","Crosshair","Brackets"},()=> (int)cue.CurrentShape,value=>cue.Shape.Value=(GazeCueShape)value,17);
        Choice("Show cue",new[]{"Near targets","Always","Off"},()=> (int)config.TargetCue.Value,value=>config.TargetCue.Value=(TargetCueMode)value,-59);
        Setting("Vertical position",cue.VerticalDegrees,-140,_=>cue.PositionLabel,.25f);
        ActionButton("Reset cue",0,-213,()=>{cue.Reset();config.TargetCue.Value=TargetCueMode.Context;});
        ActionButton("Centre cue",1,-213,()=>cue.VerticalDegrees.Value=0);

        _performancePage = Page("VR PERFORMANCE");
        var performanceInfo = Label(_performancePage.transform, "", new Vector2(0, -12), new Vector2(510, 390), 18);
        _liveLabels.Add((performanceInfo, () => PcVrCapabilities.Report));

        _feedbackPage = Page("SOUND & HAPTICS");
        Section("Interaction feedback",214);
        Setting("Sound volume",motion.InteractionVolume,159,Percent,.01f);
        Setting("Haptic strength",motion.HapticStrength,89,Percent,.01f);
        Section("Try a sample",17);
        AddFeedbackPreview("Touch",0,3,InteractionCue.Touch);
        AddFeedbackPreview("Grab",1,3,InteractionCue.Grab);
        AddFeedbackPreview("Switch",0,4,InteractionCue.Switch);
        AddFeedbackPreview("MIL detent",1,4,InteractionCue.Detent);
        Note("Point and press the trigger for a sample in that hand.\nSamples never operate aircraft controls.\nZero disables sound or haptics independently.",-159,100);

        _history = new MenuHistory<GameObject>(_homePage);
        _buttonParent = rect;
        _backButton = AddButton("Back", 0, 8, Back, 3).transform.parent.GetComponent<Button>();
        AddButton("Home", 1, 8, Home, 3);
        var closeButton=AddButton("Close", 2, 8, Close, 3).transform.parent.GetComponent<Button>();
        NativeButtonFeedback.Configure(closeButton,new Color(.19f,.25f,.28f));
        Home();
        LayerHelper.SetLayerRecursive(_panel.transform, LayerHelper.GetVrUiLayer());
    }

    private GameObject Page(string title)
    {
        var page = new GameObject(title, typeof(RectTransform));
        page.transform.SetParent(_panel!.transform, false);
        page.SetActive(false); _pageTitles.Add(page, title); _buttonParent = page.transform;
        return page;
    }

    private void AddLiveButton(Func<string> text, int column, int row, Action action) =>
        _liveLabels.Add((AddButton(text(), column, row, action), text));

    private void AddFeedbackPreview(string text, int column, int row, InteractionCue cue)
    {
        var button = AddButton(text, column, row, null).transform.parent.GetComponent<Button>();
        ((RectTransform)button.transform).anchoredPosition = new Vector2(column==0?-160:160,row==3?-32:-82);
        ((RectTransform)button.transform).sizeDelta = new Vector2(300,42);
        var preview = button.gameObject.AddComponent<FeedbackPreviewButton>();
        preview.Cue = cue;
        button.onClick.AddListener(preview.Play);
    }

    private Text AddButton(string text, int column, int row, Action? action, int columns = 2)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(_buttonParent, false);
        var bounds = WristMenuNavigation.ButtonBounds(column, row, columns);
        rect.sizeDelta = new Vector2(bounds.Width, bounds.Height);
        rect.anchoredPosition = new Vector2(bounds.X, bounds.Y);
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        if (action != null) button.onClick.AddListener(() => action());
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

    private void OnDisable() { Close(); _toggle.Reset(); }
    private void OnDestroy() { OnDisable(); if (_instance == this) _instance = null; }
}
