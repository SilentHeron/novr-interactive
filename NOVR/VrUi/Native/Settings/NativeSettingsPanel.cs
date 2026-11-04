using System;
using System.Collections.Generic;
using NuclearOption.Effects;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public partial class NativeSettingsPanel : MonoBehaviour
{
    private const float RowStartY = 350f;
    private const float RowSpacing = 42f;

    private static readonly Color BackgroundColor = new(0.025f, 0.035f, 0.045f, 0.93f);
    private static readonly Color PanelColor = new(0.05f, 0.06f, 0.065f, 0.94f);
    private static readonly Color ButtonColor = new(0.24f, 0.29f, 0.31f, 0.96f);
    private static readonly Color ButtonSelectedColor = new(0.44f, 0.49f, 0.50f, 1f);
    private static readonly Color BackButtonColor = new(0.62f, 0.12f, 0.14f, 0.96f);
    private static readonly Color ApplyButtonColor = new(0.12f, 0.34f, 0.20f, 0.96f);

    private readonly Dictionary<SettingsTab, Button> _tabButtons = new();

    private NativeGameActionAdapter? _actions;
    private RectTransform? _container;
    private RectTransform? _contentRoot;
    private Font? _font;
    private SettingsTab _currentTab = SettingsTab.Audio;
    private float _nextRowY;
    private bool _wasVisible;
    private ControllerBindingPanel? _vrBindings;
    private bool _lookCueSettings;

    public void Initialize(NativeGameActionAdapter actions, RectTransform root)
    {
        _actions = actions;
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        BuildLayout(root);
    }

    public void SetVisible(bool visible)
    {
        if (_container == null)
            return;

        if (!visible && _wasVisible)
        {
            CancelBindingCapture("Binding capture canceled.");
        }

        if (visible && !_wasVisible)
        {
            NativePlayerSettingsAccess.ReloadPlayerSettings();
            _bindingEntriesDirty = true;
            RenderCurrentTab();
        }

        _wasVisible = visible;
        NativePanelTransition.SetVisible(_container, visible);
    }

    private void Update()
    {
        if (_wasVisible)
            _vrBindings?.Refresh();
        var readyBinding = _bindingCapture.TakeReady(Time.unscaledTime);
        if (readyBinding != null)
            StartBindingCapture(readyBinding);

        if (_wasVisible && _currentTab == SettingsTab.Bindings)
        {
            UpdateVisibleBindingAxisValues();

            if (!_bindingCapture.Pending)
            {
                UpdateActiveBindingFocus();
            }
        }
    }

    private void BuildLayout(RectTransform root)
    {
        _container = CreateContainer("Native Settings", root, root.sizeDelta);
        CreateImage("Background", _container, BackgroundColor, Vector2.zero, _container.sizeDelta);
        CreateText("Header", _container, "SETTINGS", new Vector2(0f, NativeUiLayout.HeaderY), NativeUiLayout.HeaderSize, 22, TextAnchor.MiddleCenter, Color.white);

        var tabPanel = CreatePanel("Settings Tabs", _container, PanelColor, new Vector2(-770f, -15f), new Vector2(280f, 950f));
        CreateText("Tab Header", tabPanel, "CATEGORY", new Vector2(0f, 420f), new Vector2(240f, 30f), 17, TextAnchor.MiddleCenter, Color.white);

        AddTabButton(tabPanel, SettingsTab.Audio, "AUDIO", 345f);
        AddTabButton(tabPanel, SettingsTab.Graphics, "GRAPHICS", 285f);
        AddTabButton(tabPanel, SettingsTab.Gameplay, "GAMEPLAY", 225f);
        AddTabButton(tabPanel, SettingsTab.Controls, "CONTROLS", 165f);
        AddTabButton(tabPanel, SettingsTab.Bindings, "BINDINGS", 105f);
        AddTabButton(tabPanel, SettingsTab.Hud, "HUD", 45f);
        AddTabButton(tabPanel, SettingsTab.Chat, "CHAT", -15f);

        _contentRoot = CreatePanel("Settings Content", _container, PanelColor, new Vector2(170f, -15f), new Vector2(1440f, 950f));
        CreateMenuButton("BACK", _container, new Vector2(NativeUiLayout.FooterLeftX, NativeUiLayout.FooterY), NativeUiLayout.FooterButtonSize, BackButtonColor, BackToMainMenu, 15);
        CreateMenuButton("APPLY", _container, new Vector2(NativeUiLayout.FooterRightX, NativeUiLayout.FooterY), NativeUiLayout.FooterButtonSize, ApplyButtonColor, ApplyAndSave, 15);

        SetActiveTabButtonColors();
        RenderCurrentTab();
        NativePanelTransition.SetVisible(_container, false, instant: true);
    }

    private void AddTabButton(RectTransform parent, SettingsTab tab, string label, float y)
    {
        var button = CreateMenuButton(label, parent, new Vector2(0f, y), new Vector2(220f, 40f), ButtonColor, () => SelectTab(tab), 15);
        _tabButtons[tab] = button;
    }

    private void SelectTab(SettingsTab tab)
    {
        if (_currentTab == tab)
            return;

        if (_currentTab == SettingsTab.Bindings)
        {
            CancelBindingCapture(null);
        }

        _currentTab = tab;
        _lookCueSettings = false;
        SetActiveTabButtonColors();
        RenderCurrentTab();
    }

    private void SetActiveTabButtonColors()
    {
        foreach (var pair in _tabButtons)
        {
            SetButtonColor(pair.Value, pair.Key == _currentTab ? ButtonSelectedColor : ButtonColor);
        }
    }

    private void RenderCurrentTab()
    {
        if (_contentRoot == null)
            return;

        ClearContent();
        _nextRowY = RowStartY;

        switch (_currentTab)
        {
            case SettingsTab.Audio:
                CreateContentTitle("AUDIO");
                RenderAudioTab();
                break;
            case SettingsTab.Graphics:
                CreateContentTitle("GRAPHICS");
                RenderGraphicsTab();
                break;
            case SettingsTab.Gameplay:
                CreateContentTitle("GAMEPLAY");
                RenderGameplayTab();
                break;
            case SettingsTab.Controls:
                CreateContentTitle("CONTROLS");
                RenderControlsTab();
                break;
            case SettingsTab.Bindings:
                CreateContentTitle("CONTROL BINDINGS");
                RenderBindingsTab();
                break;
            case SettingsTab.Hud:
                CreateContentTitle(_lookCueSettings ? "LOOK DIRECTION CUE" : "HUD");
                if (_lookCueSettings)
                    RenderLookCueSettings();
                else
                    RenderHudTab();
                break;
            case SettingsTab.Chat:
                CreateContentTitle("CHAT");
                RenderChatTab();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void ClearContent()
    {
        if (_contentRoot == null)
            return;

        _vrBindings = null;
        _bindingAxisValueTexts.Clear();

        for (var index = _contentRoot.childCount - 1; index >= 0; index--)
        {
            Destroy(_contentRoot.GetChild(index).gameObject);
        }
    }

    private void CreateContentTitle(string title)
    {
        if (_contentRoot == null)
            return;

        CreateText($"{title} Title", _contentRoot, title, new Vector2(0f, 420f), new Vector2(1320f, 32f), 20, TextAnchor.MiddleCenter, Color.white);
    }

    private void RenderAudioTab()
    {
        AddAudioRow("Master", AudioMixerVolume.Master);
        AddAudioRow("Music", AudioMixerVolume.Music);
        AddAudioRow("Interface", AudioMixerVolume.Interface);
        AddAudioRow("Effects", AudioMixerVolume.Effects);
        AddAudioRow("Menu", AudioMixerVolume.Menu);
        AddAudioRow("Radar Warning", AudioMixerVolume.RadarWarning);
        AddAudioRow("Missile Alert", AudioMixerVolume.MissileAlert);
        AddAudioRow("Jammed Noise", AudioMixerVolume.JammedNoise);
    }

    private void RenderGraphicsTab()
    {
        if (PlayerSettings.graphics == null || PlayerSettings.DetailSettings == null)
        {
            AddReadOnlyRow("Graphics settings are not initialized yet.");
            return;
        }

        AddToggleRow("VSync", () => PlayerSettings.graphics.Vsync, value => PlayerSettings.graphics.Vsync = value);
        AddToggleRow("Cinematic Mode", () => PlayerSettings.cinematicMode, NativePlayerSettingsAccess.SetCinematicMode);
        AddToggleRow("Debug Visuals", () => PlayerSettings.debugVis, NativePlayerSettingsAccess.SetDebugVisuals);
        AddOptionRow("Anti-Aliasing", GraphicsHelper.AAOptions, () => PlayerSettings.graphics.AntiAliasing, value => PlayerSettings.graphics.AntiAliasing = value);
        AddOptionRow("Texture Quality", GraphicsHelper.MipmapLimitOptions, () => PlayerSettings.graphics.MipmapLevel, value => PlayerSettings.graphics.MipmapLevel = value);
        AddOptionRow("Anisotropic Filtering", GraphicsHelper.AnisotropicOptions, () => PlayerSettings.graphics.AnisotropicFiltering, value => PlayerSettings.graphics.AnisotropicFiltering = value);
        AddOptionRow("Shadow Quality", GraphicsHelper.ShadowQualityOptions, () => PlayerSettings.graphics.ShadowQuality, value => PlayerSettings.graphics.ShadowQuality = value);
        AddFloatRow("Shadow Distance", () => PlayerSettings.graphics.ShadowDistance, value => PlayerSettings.graphics.ShadowDistance = Mathf.RoundToInt(value), 500f, 10000f, 500f, value => $"{value:0} m");
        AddToggleRow("Soft Shadows", () => PlayerSettings.graphics.SoftShadows, value => PlayerSettings.graphics.SoftShadows = value);
        AddFloatRow("LOD Bias", () => PlayerSettings.graphics.LodBias, value => PlayerSettings.graphics.LodBias = value, 1f, 4f, 0.1f, value => $"{value:0.0}");
        AddFloatRow("Cloud Detail", () => PlayerSettings.graphics.CloudDetail, value => PlayerSettings.graphics.CloudDetail = value, 0f, 1f, 0.05f, value => $"{value * 100f:0}%");
        AddToggleRow("Grass", () => PlayerSettings.DetailSettings.GrassEnabled, value => PlayerSettings.DetailSettings.GrassEnabled = value);
        AddFloatRow("Tree Distance", () => PlayerSettings.DetailSettings.TreeRangeMultiplier, value => PlayerSettings.DetailSettings.TreeRangeMultiplier = value, DetailSettings.TreeRangeMultiplierMin, DetailSettings.TreeRangeMultiplierMax, 0.1f, value => $"{value:0.0}");
        AddActionRow("Graphics Defaults", "RESET", ResetGraphicsSettings);
    }

    private void RenderGameplayTab()
    {
        AddOptionRow("Units", new[] { "Metric", "Imperial" }, () => (int)PlayerSettings.unitSystem, NativePlayerSettingsAccess.SetUnitSystem);
        AddFloatRow("Cockpit Camera Inertia", () => PlayerSettings.cockpitCamInertia, value => NativePlayerSettingsAccess.SetPlayerFloat("CockpitCamInertia", value, static assigned => PlayerSettings.cockpitCamInertia = assigned, apply: true), 0f, 1f, 0.05f, value => $"{value * 100f:0}%");
        AddFloatRow("Cockpit FOV", () => PlayerSettings.defaultFoV, value => NativePlayerSettingsAccess.SetPlayerFloat("DefaultFoV", value, static assigned => PlayerSettings.defaultFoV = assigned, apply: true), 30f, 120f, 1f, value => $"{value:0} deg");
        AddFloatRow("External FOV", () => PlayerSettings.defaultExternalFoV, value => NativePlayerSettingsAccess.SetPlayerFloat("DefaultExternalFoV", value, static assigned => PlayerSettings.defaultExternalFoV = assigned, apply: true), 30f, 120f, 1f, value => $"{value:0} deg");
        AddToggleRow("Zoom on Boresight", () => PlayerSettings.zoomOnBoresight, value => NativePlayerSettingsAccess.SetPlayerBool("ZoomOnBoresight", value, static assigned => PlayerSettings.zoomOnBoresight = assigned, apply: true));
        AddToggleRow("Padlock Target", () => PlayerSettings.padLockTarget, value => NativePlayerSettingsAccess.SetPlayerBool("PadLockTarget", value, static assigned => PlayerSettings.padLockTarget = assigned, apply: true));
        AddToggleRow("Tac Screen IR", () => PlayerSettings.tacScreenIR, value => NativePlayerSettingsAccess.SetPlayerBool("TacScreenIR", value, static assigned => PlayerSettings.tacScreenIR = assigned, apply: true));
        AddToggleRow("Camera Auto NVG", () => PlayerSettings.cameraAutoNVG, value => NativePlayerSettingsAccess.SetPlayerBool("CameraAutoNVG", value, static assigned => PlayerSettings.cameraAutoNVG = assigned, apply: true));
        AddToggleRow("Hit Markers", () => PlayerSettings.showHitMarkers, value => NativePlayerSettingsAccess.SetPlayerBool("ShowHitMarkers", value, static assigned => PlayerSettings.showHitMarkers = assigned, apply: true));
    }

    private void RenderControlsTab()
    {
        var motion = ModConfiguration.Instance.MotionControls;
        AddActionRow("Visual VR Controller Bindings", "OPEN", RenderVrControllerBindings);
        var gripModes = new[] { "HOLD", "TOGGLE" };
        AddOptionRow("Stick Grip", gripModes, () => motion.StickTapGrip.Value ? 1 : 0, value =>
        {
            motion.StickTapGrip.Value = value == 1;
            ModConfiguration.Instance.Config.Save();
        });
        AddOptionRow("Throttle Grip", gripModes, () => motion.ThrottleTapGrip.Value ? 1 : 0, value =>
        {
            motion.ThrottleTapGrip.Value = value == 1;
            ModConfiguration.Instance.Config.Save();
        });
        if (_contentRoot != null)
        {
            StickSensitivitySlider.Create(_contentRoot, new Vector2(0, ConsumeRowY() - 14), 1000, _font!, 18);
            ConsumeRowY();
            AddReadOnlyRow("Motion stick: higher sensitivity needs less wrist tilt. Pitch/roll only; yaw is unchanged. Saves immediately.");
        }
        AddActionRow("Control Bindings", "VIEW", () => SelectTab(SettingsTab.Bindings));
        AddToggleRow("Virtual Joystick", () => PlayerSettings.virtualJoystickEnabled, value => NativePlayerSettingsAccess.SetPlayerBool("VirtualJoystickEnabled", value, static assigned => PlayerSettings.virtualJoystickEnabled = assigned, apply: true));
        AddToggleRow("Invert Virtual Pitch", () => PlayerSettings.virtualJoystickInvertPitch, value => NativePlayerSettingsAccess.SetPlayerBool("VirtualJoystickInvertPitch", value, static assigned => PlayerSettings.virtualJoystickInvertPitch = assigned, apply: true));
        AddToggleRow("Invert View Pitch", () => PlayerSettings.viewInvertPitch, value => NativePlayerSettingsAccess.SetPlayerBool("ViewInvertPitch", value, static assigned => PlayerSettings.viewInvertPitch = assigned, apply: true));
        AddToggleRow("Throttle Uses Negative Axis", () => PlayerSettings.throttleUseNegative, value => NativePlayerSettingsAccess.SetPlayerBool("ThrottleUseNegative", value, static assigned => PlayerSettings.throttleUseNegative = assigned, apply: true));
        AddToggleRow("Relative Throttle", () => PlayerSettings.throttleUseRelative, value => NativePlayerSettingsAccess.SetPlayerBool("ThrottleUseRelative", value, static assigned => PlayerSettings.throttleUseRelative = assigned, apply: true));
        AddToggleRow("Controller Menu Navigation", () => PlayerSettings.controllerMenuNavigation, value => NativePlayerSettingsAccess.SetPlayerBool("ControllerMenuNavigation", value, static assigned => PlayerSettings.controllerMenuNavigation = assigned, apply: true));
        AddToggleRow("Menu Weapon Safety", () => PlayerSettings.menuWeaponSafety, value => NativePlayerSettingsAccess.SetPlayerBool("MenuWeaponSafety", value, static assigned => PlayerSettings.menuWeaponSafety = assigned, apply: true));
        AddToggleRow("Invert Collective", () => PlayerSettings.invertCollective, value => NativePlayerSettingsAccess.SetPlayerBool("InvertCollective", value, static assigned => PlayerSettings.invertCollective = assigned, apply: true));
        AddToggleRow("TrackIR", () => PlayerSettings.useTrackIR, value => NativePlayerSettingsAccess.SetPlayerBool("UseTrackIR", value, static assigned => PlayerSettings.useTrackIR = assigned, apply: true));
        AddFloatRow("Virtual Joystick Sensitivity", () => PlayerSettings.virtualJoystickSensitivity, value => NativePlayerSettingsAccess.SetPlayerFloat("VirtualJoystickSensitivity", value, static assigned => PlayerSettings.virtualJoystickSensitivity = assigned, apply: true), 0f, 1f, 0.05f, value => $"{value:0.00}");
        AddFloatRow("Virtual Joystick Centering", () => PlayerSettings.virtualJoystickCentering, value => NativePlayerSettingsAccess.SetPlayerFloat("VirtualJoystickCentering", value, static assigned => PlayerSettings.virtualJoystickCentering = assigned, apply: true), 0f, 1f, 0.05f, value => $"{value:0.00}");
        AddFloatRow("View Sensitivity", () => PlayerSettings.viewSensitivity, value => NativePlayerSettingsAccess.SetPlayerFloat("ViewSensitivity", value, static assigned => PlayerSettings.viewSensitivity = assigned, apply: true), 0f, 1f, 0.05f, value => $"{value:0.00}");
        AddFloatRow("View Smoothing", () => PlayerSettings.viewSmoothing, value => NativePlayerSettingsAccess.SetPlayerFloat("ViewSmoothing", value, static assigned => PlayerSettings.viewSmoothing = assigned, apply: true), 0f, 1f, 0.05f, value => $"{value:0.00}");
        AddFloatRow("Button Click Delay", () => PlayerSettings.clickDelay, value => NativePlayerSettingsAccess.SetPlayerFloat("ClickDelay", value, static assigned => PlayerSettings.clickDelay = assigned, apply: true), 0f, 1f, 0.05f, value => $"{value:0.00} s");
        AddFloatRow("Button Hold Delay", () => PlayerSettings.pressDelay, value => NativePlayerSettingsAccess.SetPlayerFloat("PressDelay", value, static assigned => PlayerSettings.pressDelay = assigned, apply: true), 0f, 1f, 0.05f, value => $"{value:0.00} s");
    }

    private void RenderVrControllerBindings()
    {
        if (_contentRoot == null)
            return;
        CancelBindingCapture("Binding capture canceled.");
        ClearContent();
        CreateContentTitle("VISUAL VR CONTROLLER BINDINGS");
        var host = CreateContainer("Controller binding editor", _contentRoot, new Vector2(540, 500));
        host.anchoredPosition = new Vector2(0, 20);
        host.localScale = Vector3.one * 1.7f;
        _vrBindings = new ControllerBindingPanel(host, _font!, RenderCurrentTab);
    }

    private void RenderHudTab()
    {
        AddActionRow("VR Look Direction Cue", "OPEN", () => { _lookCueSettings = true; RenderCurrentTab(); });
        var hmdWidthMax = Mathf.Max(1080f, 1080f * Screen.width / Mathf.Max(1f, Screen.height));
        var hmdSideDistMax = Mathf.Max(100f, PlayerSettings.hmdWidth * 0.5f);
        var hmdTopHeightMax = Mathf.Max(100f, PlayerSettings.hmdHeight * 0.5f);

        AddToggleRow("Lag PIP", () => PlayerSettings.lagPip, value => NativePlayerSettingsAccess.SetHudBool("LagPip", value, static assigned => PlayerSettings.lagPip = assigned));
        AddToggleRow("Range Circle", () => PlayerSettings.rangeCircle, value => NativePlayerSettingsAccess.SetHudBool("RangeCircle", value, static assigned => PlayerSettings.rangeCircle = assigned));
        AddToggleRow("Gauges", () => PlayerSettings.gauges, value => NativePlayerSettingsAccess.SetHudBool("Gauges", value, static assigned => PlayerSettings.gauges = assigned));
        AddToggleRow("HUD Weapons", () => PlayerSettings.hudWeapons, value => NativePlayerSettingsAccess.SetHudBool("HUDWeapons", value, static assigned => PlayerSettings.hudWeapons = assigned));
        AddFloatRow("HMD Width", () => PlayerSettings.hmdWidth, value => NativePlayerSettingsAccess.SetHudFloat("HMDWidth", value, static assigned => PlayerSettings.hmdWidth = assigned), 500f, hmdWidthMax, 10f, value => $"{value:0} px");
        AddFloatRow("HMD Height", () => PlayerSettings.hmdHeight, value => NativePlayerSettingsAccess.SetHudFloat("HMDHeight", value, static assigned => PlayerSettings.hmdHeight = assigned), 500f, 1080f, 10f, value => $"{value:0} px");
        AddFloatRow("HMD Side Distance", () => PlayerSettings.hmdSideDist, value => NativePlayerSettingsAccess.SetHudFloat("HMDSideDist", value, static assigned => PlayerSettings.hmdSideDist = assigned), 100f, hmdSideDistMax, 5f, value => $"{value:0} px");
        AddFloatRow("HMD Side Angle", () => PlayerSettings.hmdSideAngle, value => NativePlayerSettingsAccess.SetHudFloat("HMDSideAngle", value, static assigned => PlayerSettings.hmdSideAngle = assigned), 0f, 90f, 1f, value => $"{value:0} deg");
        AddFloatRow("HMD Top Height", () => PlayerSettings.hmdTopHeight, value => NativePlayerSettingsAccess.SetHudFloat("HMDTopHeight", value, static assigned => PlayerSettings.hmdTopHeight = assigned), -hmdTopHeightMax, hmdTopHeightMax, 5f, value => $"{value:0} px");
        AddFloatRow("HMD Hide Distance", () => PlayerSettings.hmdHideDist, value => NativePlayerSettingsAccess.SetHudFloat("HMDHideDist", value, static assigned => PlayerSettings.hmdHideDist = assigned), 0.2f, 1f, 0.05f, value => $"{value * 100f:0}%");
        AddFloatRow("HMD Icon Size", () => PlayerSettings.hmdIconSize, value => NativePlayerSettingsAccess.SetHudFloat("HMDIconSize", value, static assigned => PlayerSettings.hmdIconSize = assigned), 10f, 80f, 5f, value => $"{value:0}");
        AddFloatRow("HUD Text Size", () => PlayerSettings.hudTextSize, value => NativePlayerSettingsAccess.SetHudFloat("HUDTextSize", value, static assigned => PlayerSettings.hudTextSize = assigned), 20f, 80f, 2f, value => $"{value:0}");
        AddFloatRow("HMD Text Size", () => PlayerSettings.hmdTextSize, value => NativePlayerSettingsAccess.SetHudFloat("HMDTextSize", value, static assigned => PlayerSettings.hmdTextSize = assigned), 20f, 80f, 2f, value => $"{value:0}");
        AddFloatRow("Overlay Text Size", () => PlayerSettings.overlayTextSize, value => NativePlayerSettingsAccess.SetHudFloat("OverlayTextSize", value, static assigned => PlayerSettings.overlayTextSize = assigned), 16f, 80f, 2f, value => $"{value:0}");
    }

    private void RenderLookCueSettings()
    {
        var config = ModConfiguration.Instance;
        var cue = config.LookCue;
        AddActionRow("HUD Settings", "BACK", () => { _lookCueSettings = false; RenderCurrentTab(); });
        AddFloatRow("Cue Size", () => cue.Scale, value => cue.Size.Value = value,
            MotionControls.GazeCueSettings.MinSize, MotionControls.GazeCueSettings.MaxSize, .1f, value => $"{value * 100:0}%");
        AddFloatRow("Cue Opacity", () => cue.Alpha, value => cue.Opacity.Value = value,
            MotionControls.GazeCueSettings.MinOpacity, 1, .05f, value => $"{value * 100:0}%");
        AddOptionRow("Cue Shape", new[] { "Circle", "Dot", "Crosshair", "Brackets" }, () => (int)cue.CurrentShape,
            value => cue.Shape.Value = (MotionControls.GazeCueShape)value);
        AddOptionRow("Show Cue", new[] { "Near targets", "Always", "Off" }, () => (int)config.TargetCue.Value,
            value => config.TargetCue.Value = (MotionControls.TargetCueMode)value);
        if (_contentRoot != null)
        {
            GazeCuePositionSlider.Create(_contentRoot, new Vector2(0, ConsumeRowY() - 12), 1000, _font!, 18);
            ConsumeRowY();
        }
        AddActionRow("Centre of Vision", "CENTRE", () => { cue.VerticalDegrees.Value = 0; RenderCurrentTab(); });
        AddActionRow("Look Cue Defaults", "RESET", () => { cue.Reset(); config.TargetCue.Value = MotionControls.TargetCueMode.Context; RenderCurrentTab(); });
        AddReadOnlyRow("Target selection follows the cue's position. Weapon sights are unchanged.");
    }

    private void RenderChatTab()
    {
        AddToggleRow("Chat", () => PlayerSettings.chatEnabled, value => NativePlayerSettingsAccess.SetPlayerBool("ChatEnabled", value, static assigned => PlayerSettings.chatEnabled = assigned));
        AddToggleRow("Chat Filter", () => PlayerSettings.chatFilter, value => NativePlayerSettingsAccess.SetPlayerBool("ChatFilter", value, static assigned => PlayerSettings.chatFilter = assigned));
        AddToggleRow("Text To Speech", () => PlayerSettings.chatTts, value => NativePlayerSettingsAccess.SetPlayerBool("ChatTts", value, static assigned => PlayerSettings.chatTts = assigned));
        AddIntRow("TTS Speed", () => PlayerSettings.chatTtsSpeed, value => NativePlayerSettingsAccess.SetPlayerInt("ChatTtsSpeed", value, static assigned => PlayerSettings.chatTtsSpeed = assigned), -10, 10, 1, value => $"{value}");
        AddIntRow("TTS Volume", () => PlayerSettings.chatTtsVolume, value => NativePlayerSettingsAccess.SetPlayerInt("ChatTtsVolume", value, static assigned => PlayerSettings.chatTtsVolume = assigned), 0, 100, 5, value => $"{value}%");
    }

    private void AddAudioRow(string label, string channel)
    {
        AddFloatRow(label, () => AudioMixerVolume.GetPref(channel), value => AudioMixerVolume.SetValue(channel, value), 0f, 1f, 0.05f, value => $"{value * 100f:0}%");
    }

    private void AddToggleRow(string label, Func<bool> getValue, Action<bool> setValue)
    {
        if (_contentRoot == null)
            return;

        var y = ConsumeRowY();
        CreateText($"{label} Label", _contentRoot, label, new Vector2(-310f, y), new Vector2(420f, 32f), 15, TextAnchor.MiddleLeft, Color.white);
        CreateMenuButton(
            getValue() ? "ON" : "OFF",
            _contentRoot,
            new Vector2(310f, y),
            new Vector2(170f, 32f),
            getValue() ? ApplyButtonColor : ButtonColor,
            () =>
            {
                setValue(!getValue());
                ApplyAndRefresh();
            },
            14);
    }

    private void AddOptionRow(string label, IList<string> options, Func<int> getValue, Action<int> setValue)
    {
        if (_contentRoot == null || options.Count == 0)
            return;

        var y = ConsumeRowY();
        CreateText($"{label} Label", _contentRoot, label, new Vector2(-310f, y), new Vector2(420f, 32f), 15, TextAnchor.MiddleLeft, Color.white);
        CreateMenuButton("-", _contentRoot, new Vector2(150f, y), new Vector2(48f, 32f), ButtonColor, () =>
        {
            setValue(ClampInt(getValue() - 1, 0, options.Count - 1));
            ApplyAndRefresh();
        }, 18);
        var index = ClampInt(getValue(), 0, options.Count - 1);
        CreateText($"{label} Value", _contentRoot, options[index], new Vector2(310f, y), new Vector2(230f, 32f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton("+", _contentRoot, new Vector2(470f, y), new Vector2(48f, 32f), ButtonColor, () =>
        {
            setValue(ClampInt(getValue() + 1, 0, options.Count - 1));
            ApplyAndRefresh();
        }, 18);
    }

    private void AddFloatRow(string label, Func<float> getValue, Action<float> setValue, float min, float max, float step, Func<float, string> format)
    {
        if (_contentRoot == null)
            return;

        var y = ConsumeRowY();
        CreateText($"{label} Label", _contentRoot, label, new Vector2(-310f, y), new Vector2(420f, 32f), 15, TextAnchor.MiddleLeft, Color.white);
        CreateMenuButton("-", _contentRoot, new Vector2(150f, y), new Vector2(48f, 32f), ButtonColor, () =>
        {
            setValue(AdjustFloat(getValue(), -step, min, max, step));
            ApplyAndRefresh();
        }, 18);
        CreateText($"{label} Value", _contentRoot, format(getValue()), new Vector2(310f, y), new Vector2(230f, 32f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton("+", _contentRoot, new Vector2(470f, y), new Vector2(48f, 32f), ButtonColor, () =>
        {
            setValue(AdjustFloat(getValue(), step, min, max, step));
            ApplyAndRefresh();
        }, 18);
    }

    private void AddIntRow(string label, Func<int> getValue, Action<int> setValue, int min, int max, int step, Func<int, string> format)
    {
        if (_contentRoot == null)
            return;

        var y = ConsumeRowY();
        CreateText($"{label} Label", _contentRoot, label, new Vector2(-310f, y), new Vector2(420f, 32f), 15, TextAnchor.MiddleLeft, Color.white);
        CreateMenuButton("-", _contentRoot, new Vector2(150f, y), new Vector2(48f, 32f), ButtonColor, () =>
        {
            setValue(ClampInt(getValue() - step, min, max));
            ApplyAndRefresh();
        }, 18);
        CreateText($"{label} Value", _contentRoot, format(getValue()), new Vector2(310f, y), new Vector2(230f, 32f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton("+", _contentRoot, new Vector2(470f, y), new Vector2(48f, 32f), ButtonColor, () =>
        {
            setValue(ClampInt(getValue() + step, min, max));
            ApplyAndRefresh();
        }, 18);
    }

    private void AddActionRow(string label, string buttonLabel, UnityEngine.Events.UnityAction action)
    {
        if (_contentRoot == null)
            return;

        var y = ConsumeRowY();
        CreateText($"{label} Label", _contentRoot, label, new Vector2(-310f, y), new Vector2(420f, 32f), 15, TextAnchor.MiddleLeft, Color.white);
        CreateMenuButton(buttonLabel, _contentRoot, new Vector2(310f, y), new Vector2(170f, 32f), ButtonColor, action, 14);
    }

    private void AddReadOnlyRow(string label)
    {
        if (_contentRoot == null)
            return;

        var y = ConsumeRowY();
        CreateText($"{label} Label", _contentRoot, label, new Vector2(0f, y), new Vector2(820f, 32f), 15, TextAnchor.MiddleCenter, new Color(0.84f, 0.88f, 0.90f, 1f));
    }

    private float ConsumeRowY()
    {
        var y = _nextRowY;
        _nextRowY -= _currentTab == SettingsTab.Controls ? 38f : RowSpacing;
        return y;
    }

    private void BackToMainMenu()
    {
        CancelBindingCapture(null);
        ApplyAndSave();
        if (_actions?.TryCloseSettingsMenu() != true)
        {
            _actions?.TryInvokeCurrentMenuButton("Back", "< BACK", "BACK", "CLOSE", "MenuExit_Button");
        }
    }

    private void ResetGraphicsSettings()
    {
        if (PlayerSettings.graphics == null || PlayerSettings.DetailSettings == null)
            return;

        PlayerSettings.graphics.Clear();
        PlayerSettings.DetailSettings.Clear();
        ApplyAndRefresh();
    }

    private void ApplyAndRefresh()
    {
        NativePlayerSettingsAccess.Save();
        RenderCurrentTab();
    }

    private void ApplyAndSave()
    {
        NativePlayerSettingsAccess.SaveAndApply();
    }

    private static float AdjustFloat(float value, float delta, float min, float max, float step)
    {
        var adjusted = Mathf.Clamp(value + delta, min, max);
        return Mathf.Clamp(Mathf.Round(adjusted / step) * step, min, max);
    }

    private static int ClampInt(int value, int min, int max)
    {
        return Mathf.Clamp(value, min, max);
    }

    private RectTransform CreateContainer(string name, RectTransform parent, Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = Vector2.zero;
        return rectTransform;
    }

    private RectTransform CreatePanel(string name, RectTransform parent, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        return CreateImage(name, parent, color, anchoredPosition, size);
    }

    private RectTransform CreateImage(string name, RectTransform parent, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        var image = gameObject.AddComponent<Image>();
        image.color = color;
        return rectTransform;
    }

    private Text CreateText(string name, RectTransform parent, string text, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        var textComponent = gameObject.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = _font;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.verticalOverflow = VerticalWrapMode.Truncate;
        return textComponent;
    }

    private Button CreateMenuButton(string label, RectTransform parent, Vector2 anchoredPosition, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick, int fontSize = 15)
    {
        var rectTransform = CreateImage(label, parent, color, anchoredPosition, size);
        var button = rectTransform.gameObject.AddComponent<Button>();
        button.targetGraphic = rectTransform.GetComponent<Image>();
        button.onClick.AddListener(onClick);

        NativeButtonFeedback.Configure(button, color);

        CreateText($"{label} Text", rectTransform, label, Vector2.zero, size, fontSize, TextAnchor.MiddleCenter, Color.white);
        return button;
    }

    private static void SetButtonColor(Button button, Color color)
    {
        var image = button.targetGraphic;
        if (image != null)
        {
            image.color = color;
        }

        NativeButtonFeedback.SetNormalColor(button, color);
    }

    private enum SettingsTab
    {
        Audio,
        Graphics,
        Gameplay,
        Controls,
        Bindings,
        Hud,
        Chat
    }

}
