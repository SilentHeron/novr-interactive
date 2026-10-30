using System;
using System.Collections.Generic;
using TMPro;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.MotionControls;

// Move the existing live widgets, rather than copying their values or drawing a
// second map. Every game-owned transform is restored before the console hides.
internal sealed class CockpitPanelHud
{
    private static readonly System.Reflection.FieldInfo AppsField = HarmonyLib.AccessTools.Field(typeof(MFDAppManager), "apps");
    private static readonly HarmonyLib.AccessTools.FieldRef<TargetCam, TargetCam.CamMode> SensorModeField = HarmonyLib.AccessTools.FieldRefAccess<TargetCam, TargetCam.CamMode>("currentMode");
    private static readonly System.Reflection.FieldInfo SensorCameraField = HarmonyLib.AccessTools.Field(typeof(TargetCam), "cam");
    private static readonly System.Reflection.FieldInfo TacticalCameraField = HarmonyLib.AccessTools.Field(typeof(TacScreen), "cam");
    private static readonly System.Reflection.FieldInfo TacticalCenterField = HarmonyLib.AccessTools.Field(typeof(TacScreen), "radarCenter");
    private static readonly System.Reflection.FieldInfo TacticalCanvasField = HarmonyLib.AccessTools.Field(typeof(TacScreen), "canvas");
    private readonly bool _integrated;
    private float DisplayHeight => _integrated ? Fs20DisplayLayout.Height : 180f;
    private BepInEx.Configuration.ConfigEntry<MfdPage> LeftPage => _integrated ? ModConfiguration.Instance.Fs20LeftMfdPage : ModConfiguration.Instance.LeftMfdPage;
    private BepInEx.Configuration.ConfigEntry<MfdPage> RightPage => _integrated ? ModConfiguration.Instance.Fs20RightMfdPage : ModConfiguration.Instance.RightMfdPage;
    private sealed class Dock
    {
        internal readonly RectTransform Widget, Slot;
        private readonly Transform _parent;
        private readonly Vector3 _position, _scale;
        private readonly Quaternion _rotation;
        private readonly Vector2 _anchorMin, _anchorMax, _pivot, _size;
        private readonly int _index;
        private readonly List<Action> _restoreLayout = new();
        private readonly Dictionary<GameObject, int> _originalLayers = new();
        private readonly List<Transform> _layerScan = new();
        private readonly List<GameObject> _destroyedLayers = new();
        private Image? _safety;
        private Text? _safeLabel;
        internal Dock(RectTransform widget, RectTransform slot)
        {
            Widget = widget; Slot = slot; _parent = widget.parent;
            _index = widget.GetSiblingIndex(); _position = widget.localPosition;
            _scale = widget.localScale; _rotation = widget.localRotation;
            _anchorMin = widget.anchorMin; _anchorMax = widget.anchorMax;
            _pivot = widget.pivot; _size = widget.sizeDelta;
            var active = widget.gameObject.activeSelf;
            _restoreLayout.Add(() => { if (widget != null) widget.gameObject.SetActive(active); });
            // Record before reparenting/activation can invoke native callbacks.
            // Integrated screens use cockpit depth; the generic console uses UI.
            ApplyLayers(); widget.gameObject.SetActive(true);
            var dimensions = widget.rect.size;
            if (widget.name == "countermeasuresBackground") dimensions = new Vector2(240, 48);
            widget.SetParent(slot, false);
            widget.anchorMin = widget.anchorMax = new Vector2(.5f, .5f);
            widget.sizeDelta = dimensions;
            widget.localRotation = Quaternion.identity;
            if (widget.name == "weaponPanel" || widget.name == "countermeasuresBackground")
            {
                var background = widget.GetComponent<Image>();
                if (background != null)
                {
                    var enabled = background.enabled;
                    _restoreLayout.Add(() => { if (background != null) background.enabled = enabled; });
                    background.enabled = false;
                }
            }
            if (widget.name == "weaponPanel")
            {
                _safety = Find(widget, "weaponDisabled")?.GetComponent<Image>();
                if (_safety != null)
                {
                    var safety = _safety; var color = safety.color;
                    safety.color = new Color(.4f, .3f, .03f, .18f);
                    _safeLabel = Label(widget, "SAFE", new Vector2(80, 33), new Vector2(70, 24), 17);
                    _safeLabel.color = new Color(1f, .75f, .2f);
                    var visibility = _safeLabel.gameObject.AddComponent<CanvasGroup>();
                    visibility.ignoreParentGroups = true; visibility.blocksRaycasts = false;
                    var label = _safeLabel;
                    _restoreLayout.Add(() =>
                    {
                        if (safety != null) safety.color = color;
                        if (label != null) UnityEngine.Object.Destroy(label.gameObject);
                    });
                }
            }
            if (widget.name == "countermeasuresBackground")
            {
                Reflow("countermeasuresImage", new Vector2(-98, 0), new Vector2(36, 36));
                Reflow("countermeasureName", new Vector2(-4, 0), new Vector2(142, 42), 17);
                Reflow("countermeasureAmmo", new Vector2(96, 0), new Vector2(42, 34), 23);
            }
            Fit();
            Debug.Log($"[NOVR] Docked {widget.name} on universal cockpit display; source size={dimensions}.");
        }
        private void Reflow(string name, Vector2 position, Vector2 size, int fontSize = 0)
        {
            var rect = Find(Widget, name) as RectTransform;
            if (rect == null) return;
            var min = rect.anchorMin; var max = rect.anchorMax; var pivot = rect.pivot;
            var delta = rect.sizeDelta; var pos = rect.localPosition; var scale = rect.localScale;
            _restoreLayout.Add(() =>
            {
                if (rect == null) return;
                rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
                rect.sizeDelta = delta; rect.localPosition = pos; rect.localScale = scale;
            });
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition3D = position; rect.localScale = Vector3.one;
            var text = rect.GetComponent<TMP_Text>();
            if (text == null || fontSize == 0) return;
            var originalSize = text.fontSize; var auto = text.enableAutoSizing;
            var alignment = text.alignment; var wrap = text.enableWordWrapping;
            _restoreLayout.Add(() =>
            {
                if (text == null) return;
                text.fontSize = originalSize; text.enableAutoSizing = auto;
                text.alignment = alignment; text.enableWordWrapping = wrap;
            });
            text.enableAutoSizing = false; text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center; text.enableWordWrapping = true;
        }
        // Native map icons, markers and text can appear after docking. Run on
        // the existing discovery tick, not every rendered frame. Keep the first
        // observed layer even if a late native initializer changes it again.
        internal void RefreshLayers()
        {
            if (Widget != null && Widget.parent == Slot) ApplyLayers();
        }
        private void ApplyLayers()
        {
            if (Widget == null || Slot == null) return;
            _destroyedLayers.Clear();
            // Destroyed Unity objects compare equal to null but retain their
            // non-null managed dictionary key until explicitly removed.
            foreach (var entry in _originalLayers)
                if (entry.Key == null) _destroyedLayers.Add(entry.Key!);
            foreach (var destroyed in _destroyedLayers) _originalLayers.Remove(destroyed);
            _destroyedLayers.Clear();
            _layerScan.Clear();
            Widget.GetComponentsInChildren(true, _layerScan);
            var layer = Slot.gameObject.layer;
            foreach (var node in _layerScan)
            {
                if (node == null) continue;
                var go = node.gameObject;
                if (!_originalLayers.ContainsKey(go)) _originalLayers.Add(go, go.layer);
                if (go.layer != layer) go.layer = layer;
            }
            _layerScan.Clear();
        }
        internal void Fit()
        {
            if (Widget == null || Widget.parent != Slot) return;
            if (_safeLabel != null) _safeLabel.enabled = _safety != null && _safety.isActiveAndEnabled;
            // Map extent is its clipped viewport, not the large terrain image and
            // distant icons underneath it. The same applies to status containers.
            var size = Widget.rect.size;
            if (size.x < 1 || size.y < 1) return;
            var scale = Mathf.Min(Slot.rect.width / size.x, Slot.rect.height / size.y);
            var fittedScale = Vector3.one * scale;
            var fittedPosition = -(Vector3)Widget.rect.center * scale;
            // Native widgets may relayout themselves. Correct real changes but
            // avoid dirtying every canvas transform on every rendered frame.
            if (Widget.localScale != fittedScale) Widget.localScale = fittedScale;
            if (Widget.localPosition != fittedPosition) Widget.localPosition = fittedPosition;
        }
        internal void Restore()
        {
            foreach (var entry in _originalLayers)
                if (entry.Key != null) entry.Key.layer = entry.Value;
            _originalLayers.Clear(); _layerScan.Clear(); _destroyedLayers.Clear();
            foreach (var restore in _restoreLayout) restore();
            _restoreLayout.Clear();
            if (Widget == null || Widget.parent != Slot) return;
            if (_parent == null)
            {
                // Docking temporarily moves native UI under the persistent VR
                // rig. If its owning scene has gone, it must not become an
                // orphaned singleton or keep updating a destroyed aircraft.
                Widget.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(Widget.gameObject);
                return;
            }
            Widget.SetParent(_parent, false);
            Widget.anchorMin = _anchorMin; Widget.anchorMax = _anchorMax;
            Widget.pivot = _pivot; Widget.sizeDelta = _size;
            Widget.localPosition = _position; Widget.localScale = _scale; Widget.localRotation = _rotation;
            Widget.SetSiblingIndex(_index);
        }
    }
    private readonly Dictionary<string, RectTransform> _slots = new();
    private readonly Dictionary<string, Dock> _docks = new();
    private readonly List<Dock> _nativeDocks = new();
    private readonly NativeMfdAppVisibility _nativeVisibility = new();
    private readonly List<GameObject> _nativeSlots = new();
    private readonly Transform _canvas;
    private readonly CanvasGroup[] _brightness = new CanvasGroup[2];
    private readonly NativeWeaponReadout _weaponReadout = new();
    private readonly Text _targetStatus;
    private float _nextReadout, _lastBrightness = -1;
    private readonly RectTransform _weapons, _map;
    private readonly RectTransform[] _screens = new RectTransform[2];
    private readonly RectTransform[] _contents = new RectTransform[2];
    private readonly MfdPageMenu[] _pageMenus = new MfdPageMenu[2];
    private readonly Text[] _headers = new Text[2], _messages = new Text[2], _numbers = new Text[2];
    private readonly RawImage[] _feeds = new RawImage[2];
    private readonly Text[] _pageStatus = new Text[2];
    private readonly GameObject[] _statusStrip = new GameObject[2];
    private readonly (MfdPage page, int mode, float zoom, string? weapon, bool caution)[] _statusInputs = new (MfdPage, int, float, string?, bool)[2];
    private readonly bool[] _statusKnown = new bool[2];
    private readonly Text[,] _softkeyLabels = new Text[2, MfdContextControls.KeyCount];
    private readonly Image[,] _softkeyBackings = new Image[2, MfdContextControls.KeyCount];
    private string _weaponStatusText = "";
    private bool _weaponStatusCaution;
    private readonly Vector3[] _corners = new Vector3[4];
    private float _nextFind;
    private int _slave = -1, _labelSlave = -2;
    private MfdPage _labelLeft = (MfdPage)(-1), _labelRight = (MfdPage)(-1);
    private Vector2 _mapPan;
    private readonly ControllerButtonGate _recenter = new();
    internal bool LeftStickSlaved => _slave >= 0;
    internal Vector2 MapPan => _mapPan;
    private bool MapReady => SceneSingleton<DynamicMap>.i is DynamicMap map && !DynamicMap.mapMaximized &&
        map.isActiveAndEnabled && map.gameObject.activeInHierarchy && Contains(map.transform);
    private Camera? SensorCamera => _aircraft?.targetCam != null ? SensorCameraField.GetValue(_aircraft.targetCam) as Camera : null;
    internal int ContextGeneration(bool left)
    {
        var menu = _pageMenus[left ? 0 : 1];
        menu.Refresh(left ? LeftPage.Value : RightPage.Value, PageAvailability());
        return menu.Generation;
    }
    internal MfdContextKey ContextKey(bool left, int key)
    {
        var config = ModConfiguration.Instance;
        var camera = SensorCamera;
        var hud = SceneSingleton<CombatHUD>.i;
        var actions = ControllerActions.Instance != null && _aircraft != null;
        var available = PageAvailability();
        var menu = _pageMenus[left ? 0 : 1];
        menu.Refresh(left ? LeftPage.Value : RightPage.Value, available);
        var context = new MfdContextState
        {
            Available = available, MapReady = MapReady, MapNorthUp = config.CockpitMapNorthUp.Value,
            MapZoom = SceneSingleton<DynamicMap>.i?.GetZoomLevel() ?? 1,
            SensorReady = camera != null && camera.targetTexture != null && camera.gameObject.activeInHierarchy,
            OpticalEnabled = config.OpticalHud.Enabled.Value, Tracking = OpticalTargetingHud.ModeLabel == "TRACK",
            HeadFeed = _aircraft != null && camera != null && OpticalTargetingHud.TryGetHeadFeed(_aircraft, camera, out _),
            CanLookToAim = OpticalTargetingHud.CanLookToAim, CanTrackSelected = OpticalTargetingHud.CanTrackSelected,
            SensorZoom = config.OpticalHud.Magnification.Value,
            HasTargets = _aircraft?.weaponManager?.GetTargetList().Count > 0,
            LocalHudReady = hud != null && _aircraft != null && hud.aircraft == _aircraft,
            WeaponActionsReady = actions && _aircraft!.weaponManager != null && _aircraft.weaponStations.Count > 1,
            CountermeasureActionsReady = actions && _aircraft!.countermeasureManager != null,
            PickerOpen = menu.Open, PickerSelection = menu.Selection,
            CanSwap = LeftPage.Value != RightPage.Value && MfdPagePicker.Fitted(LeftPage.Value, available) && MfdPagePicker.Fitted(RightPage.Value, available)
        };
        return MfdContextControls.Key(left ? LeftPage.Value : RightPage.Value, key, context);
    }
    private void SelectPage(bool left, MfdPage page)
    {
        ClosePageMenus();
        var pair = MfdPages.Select(LeftPage.Value, RightPage.Value, left, page);
        LeftPage.Value = pair.left; RightPage.Value = pair.right;
    }
    internal bool UseContextKey(bool left, int key)
    {
        var control = ContextKey(left, key);
        if (!control.enabled) return false;
        var config = ModConfiguration.Instance;
        switch (control.action)
        {
            case MfdContextAction.PageMenu:
                ClosePageMenus();
                return _pageMenus[left ? 0 : 1].Show(left ? LeftPage.Value : RightPage.Value, PageAvailability());
            case MfdContextAction.SwapDisplays:
                ClosePageMenus();
                var oldLeft = LeftPage.Value; LeftPage.Value = RightPage.Value; RightPage.Value = oldLeft;
                if (_slave >= 0) _slave = 1 - _slave;
                _recenter.Reset(); return true;
            case MfdContextAction.PickerPrevious:
            case MfdContextAction.PickerNext:
                return _pageMenus[left ? 0 : 1].Move(control.action == MfdContextAction.PickerPrevious ? -1 : 1);
            case MfdContextAction.PickerOpen:
                if (!_pageMenus[left ? 0 : 1].Confirm(out var chosen)) return false;
                SelectPage(left, chosen); return true;
            case MfdContextAction.PickerBack:
                _pageMenus[left ? 0 : 1].Close(); return true;
            case MfdContextAction.ZoomIn:
            case MfdContextAction.ZoomOut:
                var direction = control.action == MfdContextAction.ZoomIn ? 1 : -1;
                if ((left ? LeftPage.Value : RightPage.Value) == MfdPage.Map)
                {
                    var map = SceneSingleton<DynamicMap>.i;
                    // The docked SetZoomLevel patch preserves the cockpit map
                    // pivot. Never use the desktop cursor-pivot zoom path.
                    if (!MapReady || map == null) return false;
                    map.SetZoomLevel(MfdContextControls.MapZoom(map.GetZoomLevel(), direction));
                }
                else config.OpticalHud.Magnification.Value = MfdContextControls.SensorZoom(config.OpticalHud.Magnification.Value, direction);
                return true;
            case MfdContextAction.RecenterMap: _mapPan = Vector2.zero; _recenter.Reset(); return true;
            case MfdContextAction.ToggleMapOrientation:
                config.CockpitMapNorthUp.Value = !config.CockpitMapNorthUp.Value;
                _mapPan = Vector2.zero; _recenter.Reset(); return true;
            case MfdContextAction.HeadAim: return OpticalTargetingHud.TryLookToAim();
            case MfdContextAction.TrackSelected: return OpticalTargetingHud.TryTrackSelected();
            case MfdContextAction.ClearTargets:
                SceneSingleton<CombatHUD>.i.DeselectAll(); return true;
            case MfdContextAction.PreviousWeapon: ControllerActions.Instance!.QueuePanelAction("Previous Weapon"); return true;
            case MfdContextAction.NextWeapon: ControllerActions.Instance!.QueuePanelAction("Next Weapon"); return true;
            case MfdContextAction.NextCountermeasure: ControllerActions.Instance!.QueuePanelAction("Next Countermeasure"); return true;
            default:
                var page = control.action switch
                {
                    MfdContextAction.Map => MfdPage.Map, MfdContextAction.Sensor => MfdPage.Sensor,
                    MfdContextAction.Weapons => MfdPage.Weapons, MfdContextAction.Flight => MfdPage.Flight,
                    MfdContextAction.EngineFuel => MfdPage.EngineFuel, MfdContextAction.Systems => MfdPage.Systems,
                    MfdContextAction.Tactical => MfdPage.Tactical, _ => (MfdPage)(-1)
                };
                if ((int)page < 0) return false;
                SelectPage(left, page); return true;
        }
    }
    internal string ContextHint(bool left, int key) => ContextKey(left, key).hint;
    internal void ToggleSlave(bool left)
    {
        var index = left ? 0 : 1;
        if (_slave == index) { _slave = -1; _mapPan = Vector2.zero; _recenter.Reset(); return; }
        var config = ModConfiguration.Instance;
        // Map is the first supported interactive page. Selecting the slave key
        // brings it onto this MFD, swapping the other page if necessary.
        if (left) { if (RightPage.Value == MfdPage.Map) RightPage.Value = LeftPage.Value; LeftPage.Value = MfdPage.Map; }
        else { if (LeftPage.Value == MfdPage.Map) LeftPage.Value = RightPage.Value; RightPage.Value = MfdPage.Map; }
        _slave = index; _mapPan = Vector2.zero; _recenter.Reset();
    }
    private void UpdateSlave()
    {
        if (_slave >= 0 && (_slave == 0 ? _left : _right) != MfdPage.Map) { _slave = -1; _mapPan = Vector2.zero; }
        if (_labelSlave != _slave || _labelLeft != _left || _labelRight != _right)
        {
            _labelSlave = _slave; _labelLeft = _left; _labelRight = _right;
            for (var i = 0; i < 2; i++)
            {
                var page = i == 0 ? _left : _right;
                _numbers[i].text = _slave == i ? "SLAVED" : "L STK";
            }
        }
        if (_slave < 0 || VrUi.WristMenu.IsOpen || CockpitLayout.Active || !TrackedControllers.Available ||
            !GameManager.flightControlsEnabled || Time.timeScale <= 0 || DynamicMap.mapMaximized ||
            CursorManager.GetFlag(CursorFlags.GameMenu) || GameManager.controlMapper?.isOpen == true ||
            (SceneSingleton<RadialMenuMain>.i != null && RadialMenuMain.IsInUse()) ||
            Fs20NativeCockpit.OwnsHand(true) || UniversalCockpitPanel.OwnsHand(true) || HelmetNightVision.OwnsHand(true))
        { _recenter.Reset(); return; }
        var hand = TrackedControllers.Read(true);
        _recenter.Update(hand.Valid, hand.StickClick ? 1 : 0);
        if (_recenter.Pressed) { _mapPan = Vector2.zero; InteractionHaptics.Pulse(hand.Device); }
        if (!hand.Valid || !hand.HasAxis) return;
        var map = SceneSingleton<DynamicMap>.i; if (map == null || DynamicMap.mapMaximized) return;
        var axis = new Vector2(FlightControlMath.Axis(hand.Axis.x, .2f, 1), FlightControlMath.Axis(hand.Axis.y, .2f, 1));
        _mapPan = Vector2.ClampMagnitude(_mapPan - axis * (180 * Mathf.Min(Time.unscaledDeltaTime, .05f) / Mathf.Max(1, map.GetZoomLevel())), 2000);
    }
    private Aircraft? _aircraft;
    private MfdPage _left = (MfdPage)(-1), _right = (MfdPage)(-1);
    private HUDApp[] _apps = Array.Empty<HUDApp>();
    private MFDAppManager? _appManager;
    private TacScreen? _tac;
    internal bool HasStatus => _docks.ContainsKey("weaponPanel");
    internal bool Contains(Transform widget)
    {
        foreach (var dock in _docks.Values) if (dock.Widget != null && (widget == dock.Widget || widget.IsChildOf(dock.Widget))) return true;
        foreach (var dock in _nativeDocks) if (dock.Widget != null && (widget == dock.Widget || widget.IsChildOf(dock.Widget))) return true;
        return false;
    }
    internal CockpitPanelHud(Transform panel, bool integrated = false)
    {
        _integrated = integrated;
        _aspect = integrated ? Fs20DisplayLayout.Width / 215f : 1;
        var go = new GameObject("Console live displays", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        _canvas = go.transform; _canvas.SetParent(panel, false); _canvas.localScale = Vector3.one * .001f;
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = integrated ? APIBus.MainCamera : APIBus.CockpitHudCamera;
        go.GetComponent<CanvasGroup>().blocksRaycasts = false;
        for (var i = 0; i < 2; i++)
        {
            var x = i == 0 ? -130f : 130f;
            _screens[i] = Rect("MFD " + i, _canvas, new Vector2(x, 20), new Vector2(215 * _aspect, DisplayHeight));
            _screens[i].localPosition += Vector3.back * 46;
            _screens[i].gameObject.AddComponent<RectMask2D>();
            _brightness[i] = _screens[i].gameObject.AddComponent<CanvasGroup>();
            _contents[i] = Rect("MFD page content", _screens[i], Vector2.zero, Vector2.zero);
            _contents[i].anchorMin = Vector2.zero; _contents[i].anchorMax = Vector2.one;
            _headers[i] = Label(_canvas, "", new Vector2(x, 120), new Vector2(212, 18), 12);
            _headers[i].rectTransform.localPosition += Vector3.back * 46;
            _numbers[i] = Label(_canvas, "", new Vector2(x, -89), new Vector2(45, 14), 10);
            _numbers[i].rectTransform.localPosition += Vector3.back * 53;
            _pageMenus[i] = new MfdPageMenu(i == 0, _headers[i], _screens[i], integrated, integrated ? _contents[i] : null);
            _messages[i] = Label(_contents[i], "", Vector2.zero, new Vector2(200, DisplayHeight - 15), 16);
            var feed = Rect("Native camera feed", _contents[i], Vector2.zero, new Vector2(215 * _aspect, DisplayHeight));
            _feeds[i] = feed.gameObject.AddComponent<RawImage>(); _feeds[i].raycastTarget = false; _feeds[i].enabled = false;
            var strip = Rect("Page status", _contents[i], new Vector2(0, -DisplayHeight * .5f + 9), new Vector2(215 * _aspect, 18));
            var tint = strip.gameObject.AddComponent<Image>(); tint.color = new Color(.008f,.015f,.016f,.96f); tint.raycastTarget = false;
            _statusStrip[i] = strip.gameObject;
            _pageStatus[i] = Label(strip, "", Vector2.zero, new Vector2(212 * _aspect, 18), 11);
            _pageStatus[i].supportRichText = false; _statusStrip[i].SetActive(false);
        }
        _weapons = Rect("Weapons page", _canvas, Vector2.zero, new Vector2(215 * _aspect, DisplayHeight));
        _targetStatus = Label(_weapons, "", new Vector2(0, DisplayHeight * .5f - 11), new Vector2(212 * _aspect, 18), 11);
        _targetStatus.supportRichText = false;
        var readoutVisibility = _targetStatus.gameObject.AddComponent<CanvasGroup>();
        readoutVisibility.ignoreParentGroups = true; readoutVisibility.blocksRaycasts = false;
        // Give the main weapon readout the additional vertical space. Dock.Fit
        // scales native contents uniformly; labels and map pixels never stretch.
        var extraHeight = DisplayHeight - 180;
        Slot("weaponPanel", _weapons, new Vector2(0, 32), new Vector2(212 * _aspect, 72 + extraHeight));
        Slot("countermeasuresBackground", _weapons, new Vector2(0, -25 - extraHeight * .5f), new Vector2(212 * _aspect, 32));
        Slot("PowerPanel", _weapons, new Vector2(0, -55 - extraHeight * .5f), new Vector2(212 * _aspect, 20));
        Slot("GridAircraft", _weapons, new Vector2(25, -80 - extraHeight * .5f), new Vector2(150, 16));
        Label(_weapons, "GRID", new Vector2(-72, -80 - extraHeight * .5f), new Vector2(50, 16), 11);
        _map = Rect("Map page", _canvas, Vector2.zero, new Vector2(215 * _aspect, DisplayHeight));
        Slot("map", _map, new Vector2(0, 9), new Vector2(215 * _aspect, DisplayHeight - 18));
        if (integrated)
        {
            // Taller upper instrument displays remain fixed to the cockpit.
            for (var i = 0; i < 2; i++)
            {
                var x = i == 0 ? -110f : 110f;
                _screens[i].localPosition = Fs20DisplayLayout.Center(i == 0) * 1000;
                _screens[i].localRotation = Fs20DisplayLayout.Rotation;
                _screens[i].localScale = Vector3.one * (Fs20DisplayLayout.Scale * 1000);
                var background = _screens[i].gameObject.AddComponent<Image>();
                background.color = new Color(.004f, .009f, .008f, 1); background.raycastTarget = false;
                _headers[i].rectTransform.localPosition = new Vector3(x, 340, 514);
                _headers[i].rectTransform.localRotation = Quaternion.Euler(12, 0, 0);
                _headers[i].rectTransform.localScale = Vector3.one * .67f;
                _numbers[i].gameObject.SetActive(false);
                _headers[i].transform.parent.gameObject.SetActive(false); // Physical nameplate carries the page title.
                for (var key = 0; key < MfdContextControls.KeyCount; key++)
                {
                    var physical = Fs20PanelLayout.MfdKeys[i][key];
                    var local = Quaternion.Inverse(Fs20DisplayLayout.Rotation) * (physical.Point - Fs20DisplayLayout.Center(i == 0));
                    var onLeft = key < 2;
                    var position = new Vector2((onLeft ? -1 : 1) * (Fs20DisplayLayout.Width * .5f - 31), local.y / Fs20DisplayLayout.Scale);
                    var edge = Rect("Softkey " + (key + 1), _screens[i], position, new Vector2(62, 29));
                    var visibility = edge.gameObject.AddComponent<CanvasGroup>();
                    visibility.ignoreParentGroups = true; visibility.blocksRaycasts = false;
                    _softkeyBackings[i, key] = edge.gameObject.AddComponent<Image>();
                    _softkeyBackings[i, key].raycastTarget = false;
                    var text = Label(edge, "", Vector2.zero, new Vector2(58, 27), 11);
                    text.alignment = onLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                    text.supportRichText = false;
                    _softkeyLabels[i, key] = text;
                }
            }
        }
        LayerHelper.SetLayerRecursive(_canvas, integrated ? LayerHelper.Layers.Cockpit : LayerHelper.GetVrUiLayer());
    }
    private float _aspect = 1;
    internal void SetAspect(float aspect)
    {
        if (_integrated) return; // Native apertures have a fixed measured aspect.
        if (Mathf.Abs(_aspect - aspect) < .001f) return;
        _aspect = aspect;
        for (var i = 0; i < 2; i++)
        {
            var x = (i == 0 ? -130 : 130) * aspect;
            _screens[i].sizeDelta = new Vector2(215 * aspect, 180);
            foreach (var rect in new[] { _screens[i], _headers[i].rectTransform, _numbers[i].rectTransform })
            { var p = rect.localPosition; p.x = x; rect.localPosition = p; }
        }
        _map.sizeDelta = new Vector2(215 * aspect, 180);
        _slots["map"].sizeDelta = new Vector2(215 * aspect, 162);
        for (var i = 0; i < 2; i++) ((RectTransform)_statusStrip[i].transform).sizeDelta = new Vector2(215 * aspect, 18);
        RestoreNative(); _nextFind = 0;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 center, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = center; rect.sizeDelta = size; go.layer = parent.gameObject.layer;
        return rect;
    }
    private static Text Label(Transform parent, string caption, Vector2 position, Vector2 size, int fontSize)
    {
        var rect = Rect("Console label", parent, position, size); var text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); text.text = caption;
        text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(.4f, .8f, .6f); text.raycastTarget = false; return text;
    }
    private void Slot(string key, Transform parent, Vector2 center, Vector2 size)
    {
        var rect = Rect("Display " + key, parent, center, size); rect.gameObject.AddComponent<RectMask2D>(); _slots.Add(key, rect);
    }
    internal void SetVisible(bool visible)
    {
        _canvas.gameObject.SetActive(visible);
        // Return borrowed widgets before releasing their native presentation
        // wrappers, which may be the original parent recorded by a Dock.
        if (!visible) Restore();
    }
    internal void ClosePageMenus() { foreach (var menu in _pageMenus) menu?.Close(); }
    internal void HighlightContextKeys(string? active, string? leftHand, string? rightHand)
    {
        if (!_integrated) return;
        for (var screen = 0; screen < 2; screen++)
            for (var key = 0; key < MfdContextControls.KeyCount; key++)
            {
                var value = ContextKey(screen == 0, key);
                var id = Fs20PanelLayout.MfdKeys[screen][key].Id;
                var touched = value.enabled && (active == id || leftHand == id || rightHand == id);
                var text = _softkeyLabels[screen, key];
                var caption = value.label;
                var backing = (RectTransform)_softkeyBackings[screen, key].transform;
                backing.sizeDelta = new Vector2(62, 19);
                if (text.text != caption) text.text = caption;
                text.color = value.enabled ? Color.white : new Color(.52f, .58f, .58f);
                _softkeyBackings[screen, key].color = touched ? new Color(.24f,.19f,.07f,.96f) : new Color(.008f,.014f,.015f,.88f);
                // Native pages are reparented when selected. Keep edge labels
                // above the page while retaining the cockpit depth buffer.
                text.transform.parent.SetAsLastSibling();
            }
    }
    internal string? PageHint(string? control) => _pageMenus[0].Hint(control) ?? _pageMenus[1].Hint(control);
    internal bool PagePoint(string control, out Vector3 point) => _pageMenus[0].TryPoint(control, out point) || _pageMenus[1].TryPoint(control, out point);
    internal bool PageRayPoint(string id, Ray ray, out Vector3 point) => _pageMenus[0].RayPoint(id, ray, out point) || _pageMenus[1].RayPoint(id, ray, out point);
    internal string? NearPageControl(Vector3 position, ref float distance)
    {
        var left = _pageMenus[0].Near(position, ref distance);
        return _pageMenus[1].Near(position, ref distance) ?? left;
    }
    internal string? RayPageControl(Ray ray, ref float distance)
    {
        var left = _pageMenus[0].RayTarget(ray, ref distance);
        return _pageMenus[1].RayTarget(ray, ref distance) ?? left;
    }
    internal void HighlightPages(string? active, string? left, string? right, string? ray)
    { foreach (var menu in _pageMenus) menu.Highlight(active, left, right, ray); }
    internal bool ActivatePageControl(string control, out string feedback)
    {
        for (var i = 0; i < 2; i++)
        {
            if (!_pageMenus[i].Activate(control, out var page, out feedback)) continue;
            if (_pageMenus[i].Open) _pageMenus[1 - i].Close();
            if (page.HasValue)
            {
                var config = ModConfiguration.Instance;
                var pair = MfdPages.Select(LeftPage.Value, RightPage.Value, i == 0, page.Value);
                LeftPage.Value = pair.left; RightPage.Value = pair.right;
            }
            return true;
        }
        feedback = ""; return false;
    }
    private int PageAvailability()
    {
        var mask = (1 << (int)MfdPage.Weapons) | (1 << (int)MfdPage.Map);
        foreach (var app in _apps)
        {
            if (app == null) continue;
            var name = app.GetType().Name;
            if (name == "BasicFlightInstruments") mask |= 1 << (int)MfdPage.Flight;
            if (name is "StatusGauges" or "EngineTelemetry" or "FuelGauge") mask |= 1 << (int)MfdPage.EngineFuel;
            if (name == "SystemStatusDisplay") mask |= 1 << (int)MfdPage.Systems;
        }
        if (_tac != null) mask |= 1 << (int)MfdPage.Tactical;
        if (_aircraft?.targetCam != null) mask |= 1 << (int)MfdPage.Sensor;
        return mask;
    }
    internal void Cycle(bool left, int direction)
    {
        ClosePageMenus();
        var config = ModConfiguration.Instance;
        var pages = MfdPages.Cycle(LeftPage.Value, RightPage.Value, left, direction, PageAvailability());
        LeftPage.Value = pages.left; RightPage.Value = pages.right;
    }
    internal void Update()
    {
        if (!GameManager.GetLocalAircraft(out var aircraft)) { Restore(); return; }
        var config = ModConfiguration.Instance;
        if (LeftPage.Value == RightPage.Value)
            RightPage.Value = (MfdPage)(((int)LeftPage.Value + 1) % MfdPages.Count);
        if (_aircraft != aircraft) { Restore(); _aircraft = aircraft; }
        if (_left != LeftPage.Value || _right != RightPage.Value)
        {
            RestoreNative(); _left = LeftPage.Value; _right = RightPage.Value; _nextFind = 0;
            PlacePage(_weapons, MfdPage.Weapons); PlacePage(_map, MfdPage.Map);
            for (var i = 0; i < 2; i++)
            {
                var page = i == 0 ? _left : _right;
                _numbers[i].text = ((int)page + 1) + " / " + MfdPages.Count;
                _messages[i].text = ""; _feeds[i].enabled = false;
            }
        }
        if (Time.unscaledTime >= _nextFind)
        {
            _nextFind = Time.unscaledTime + .5f;
            var flightHud = SceneSingleton<FlightHud>.i;
            if (flightHud != null)
                foreach (var slot in _slots)
                {
                    if (slot.Key == "map" || (_docks.TryGetValue(slot.Key, out var existing) && existing.Widget != null)) continue;
                    var widget = Find(flightHud.transform, slot.Key) as RectTransform;
                    if (widget != null) _docks[slot.Key] = new Dock(widget, slot.Value);
                }
            if (_appManager == null) _appManager = aircraft.cockpit.GetComponentInChildren<MFDAppManager>(true);
            _apps = _appManager == null ? Array.Empty<HUDApp>() : AppsField.GetValue(_appManager) as HUDApp[] ?? Array.Empty<HUDApp>();
            // Native UI rebuilds can replace an app without changing our page.
            // Release its old slot before visibility leases are refreshed so a
            // stale Dock cannot keep the replacement page empty.
            foreach (var dock in _nativeDocks)
                if (dock.Widget == null || dock.Slot == null ||
                    !Array.Exists(_apps, app => app != null && app.transform == dock.Widget))
                { RestoreNative(); break; }
            if (_tac == null) _tac = aircraft.cockpit.GetComponentInChildren<TacScreen>(true);
            if (_integrated)
            {
                var radar = _tac == null ? null : TacticalCenterField.GetValue(_tac) as RectTransform;
                var viewport = radar?.GetComponentInParent<RectMask2D>()?.transform;
                // Radar and ordinary instruments can be siblings on the same
                // canvas. Protect the radar viewport, not every sibling app.
                _nativeVisibility.Capture(_apps, viewport ?? (_tac == null ? null : TacticalCanvasField.GetValue(_tac) as Canvas)?.transform);
            }
            for (var i = 0; i < 2; i++) BuildNativePage(i, i == 0 ? _left : _right);
            foreach (var dock in _docks.Values) dock.RefreshLayers();
            foreach (var dock in _nativeDocks) dock.RefreshLayers();
        }
        var map = SceneSingleton<DynamicMap>.i;
        if (!DynamicMap.mapMaximized && map != null && map.gameObject.activeSelf &&
            (!_docks.TryGetValue("map", out var current) || current.Widget != map.transform || map.transform.parent != current.Slot))
            _docks["map"] = new Dock((RectTransform)map.transform, _slots["map"]);
        foreach (var dock in _docks.Values) dock.Fit();
        foreach (var dock in _nativeDocks) dock.Fit();
        if (_integrated) _nativeVisibility.Apply(_canvas);
        UpdateSlave();
        if (_lastBrightness != config.MfdBrightness.Value)
        {
            _lastBrightness = config.MfdBrightness.Value;
            for (var i = 0; i < 2; i++) _brightness[i].alpha = _lastBrightness;
        }
        if (Time.unscaledTime >= _nextReadout)
        {
            _nextReadout = Time.unscaledTime + .1f;
            var readout = _weaponReadout.Read(aircraft);
            _weaponStatusText = readout.text; _weaponStatusCaution = readout.caution;
            if (_targetStatus.text != readout.text) _targetStatus.text = readout.text;
            _targetStatus.color = readout.caution ? new Color(1, .75f, .2f) : new Color(.55f, .9f, .7f);
        }
        var available = PageAvailability();
        for (var i = 0; i < 2; i++)
        {
            UpdateFeed(i, i == 0 ? _left : _right);
            _pageMenus[i].Refresh(i == 0 ? _left : _right, available);
            UpdatePageStatus(i, i == 0 ? _left : _right);
            _pageMenus[i].BringToFront();
        }
    }
    private void UpdatePageStatus(int index, MfdPage page)
    {
        var shown = page is MfdPage.Map or MfdPage.Sensor;
        if (_statusStrip[index].activeSelf != shown) _statusStrip[index].SetActive(shown);
        if (!shown) return;
        var sensor = page == MfdPage.Sensor;
        var strip = (RectTransform)_statusStrip[index].transform;
        var position = new Vector2(0, -DisplayHeight * .5f + 9);
        var size = new Vector2(215 * _aspect, 18);
        var textSize = new Vector2(212 * _aspect, 18);
        if (strip.anchoredPosition != position) strip.anchoredPosition = position;
        if (strip.sizeDelta != size) strip.sizeDelta = size;
        if (_pageStatus[index].rectTransform.sizeDelta != textSize) _pageStatus[index].rectTransform.sizeDelta = textSize;
        if (strip.GetSiblingIndex() != strip.parent.childCount - 1) strip.SetAsLastSibling();

        // Compare the values before formatting. A stable map/sensor page must
        // not allocate identical status strings at the headset refresh rate.
        var config = ModConfiguration.Instance;
        int mode;
        float zoom;
        if (sensor)
        {
            mode = OpticalTargetingHud.HeadAimActive ? 3 :
                _aircraft?.targetCam != null && SensorModeField(_aircraft.targetCam) == TargetCam.CamMode.landingMode ? 2 :
                _feeds[index].enabled ? 1 : 0;
            zoom = mode == 3 ? config.OpticalHud.Magnification.Value : 0;
        }
        else
        {
            mode = (config.CockpitMapNorthUp.Value ? 1 : 0) | (_slave == index ? 2 : 0);
            zoom = SceneSingleton<DynamicMap>.i?.GetZoomLevel() ?? 1;
        }
        var inputs = (page, mode, zoom, sensor ? _weaponStatusText : null, sensor && _weaponStatusCaution);
        if (_statusKnown[index] && _statusInputs[index] == inputs) return;
        _statusKnown[index] = true; _statusInputs[index] = inputs;
        var text = !sensor ? ((mode & 1) != 0 ? "NORTH" : "HDG") + " · " +
            ((mode & 2) != 0 ? "PAN" : "FOLLOW") + " · " + zoom.ToString("0.#") + "×" :
            (mode == 3 ? "HEAD · " + zoom.ToString("0.#") + "×" : mode == 2 ? "LANDING · FIXED" : mode == 1 ? "AUTO" : "NO IMAGE") +
            " · " + _weaponStatusText.Replace("SELECTED", "SEL");
        if (_pageStatus[index].text != text) _pageStatus[index].text = text;
        var color = sensor && _weaponStatusCaution ? new Color(1,.75f,.2f) : new Color(.65f,.91f,.8f);
        if (_pageStatus[index].color != color) _pageStatus[index].color = color;
    }
    private void PlacePage(RectTransform page, MfdPage kind)
    {
        var side = _left == kind ? 0 : _right == kind ? 1 : -1;
        page.SetParent(side < 0 ? _canvas : _contents[side], false);
        page.localPosition = Vector3.zero; page.localScale = Vector3.one; page.localRotation = Quaternion.identity;
        page.gameObject.SetActive(side >= 0);
    }
    private void BuildNativePage(int index, MfdPage page)
    {
        if (page != MfdPage.Flight && page != MfdPage.EngineFuel && page != MfdPage.Systems) return;
        foreach (var dock in _nativeDocks) if (dock.Slot.IsChildOf(_screens[index])) return;
        var names = page == MfdPage.Flight ? new[] { "BasicFlightInstruments" } :
            page == MfdPage.EngineFuel ? new[] { "StatusGauges" } : new[] { "SystemStatusDisplay" };
        var apps = System.Array.FindAll(_apps, app => app != null && System.Array.IndexOf(names, app.GetType().Name) >= 0);
        if (apps.Length == 0 && page == MfdPage.EngineFuel)
            apps = System.Array.FindAll(_apps, app => app != null && (app.GetType().Name == "EngineTelemetry" || app.GetType().Name == "FuelGauge"));
        _messages[index].text = apps.Length == 0 ? "DISPLAY NOT FITTED\nTO THIS AIRCRAFT" : "";
        var columns = apps.Length > 1 ? 2 : 1; var rows = Mathf.CeilToInt(apps.Length / (float)columns);
        for (var i = 0; i < apps.Length; i++)
        {
            if (apps[i].transform is not RectTransform widget) continue;
            var size = new Vector2((215f * _aspect - 8) / columns, (DisplayHeight - 6) / rows);
            var position = new Vector2((i % columns - (columns - 1) / 2f) * size.x, ((rows - 1) / 2f - i / columns) * size.y);
            var slot = Rect("Native instrument slot", _contents[index], position, size);
            _nativeSlots.Add(slot.gameObject); _nativeDocks.Add(new Dock(widget, slot));
        }
    }
    private void UpdateFeed(int index, MfdPage page)
    {
        if (page != MfdPage.Tactical && page != MfdPage.Sensor) return;
        Texture? texture = null; var uv = new UnityEngine.Rect(0, 0, 1, 1);
        if (page == MfdPage.Sensor && _aircraft?.targetCam != null)
        {
            var camera = SensorCameraField.GetValue(_aircraft.targetCam) as Camera;
            if (camera != null)
            {
                if (OpticalTargetingHud.TryGetHeadFeed(_aircraft, camera, out var headFeed)) texture = headFeed;
                else if (camera.enabled) texture = camera.targetTexture;
            }
        }
        else if (page == MfdPage.Tactical && _tac != null)
        {
            var camera = TacticalCameraField.GetValue(_tac) as Camera;
            var center = TacticalCenterField.GetValue(_tac) as RectTransform;
            var canvas = TacticalCanvasField.GetValue(_tac) as Canvas;
            var viewport = center?.GetComponentInParent<RectMask2D>()?.transform as RectTransform;
            if (camera != null && camera.isActiveAndEnabled && viewport != null && canvas?.transform is RectTransform canvasRect)
            {
                texture = camera.targetTexture;
                // The radar center is only an icon origin, not its visible bounds.
                // Normalize the native clipped viewport in canvas coordinates:
                // screen-space canvas/camera poses can differ until render time.
                viewport.GetWorldCorners(_corners);
                var min = new Vector2(1, 1); var max = Vector2.zero;
                foreach (var corner in _corners)
                {
                    var local = canvasRect.InverseTransformPoint(corner);
                    var rect = canvasRect.rect;
                    var point = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
                    min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                }
                min = Vector2.Max(Vector2.zero, min); max = Vector2.Min(Vector2.one, max);
                uv = UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                if (uv.width <= .001f || uv.height <= .001f) texture = null;
            }
        }
        var image = _feeds[index]; image.texture = texture; image.uvRect = uv; image.enabled = texture != null;
        _messages[index].text = texture != null ? "" : page == MfdPage.Sensor ?
            _aircraft?.targetCam == null ? "SENSOR NOT FITTED" : "NO SENSOR IMAGE\nEnable EOTS HUD for head aim\nor select a target" : "TACTICAL DISPLAY UNAVAILABLE";
        if (texture == null) return;
        var ratio = texture.width * uv.width / Mathf.Max(1, texture.height * uv.height);
        var height = page == MfdPage.Sensor ? DisplayHeight - 18 : DisplayHeight;
        image.rectTransform.anchoredPosition = new Vector2(0, page == MfdPage.Sensor ? 9 : 0);
        image.rectTransform.sizeDelta = ratio > 215f * _aspect / height ? new Vector2(215 * _aspect, 215 * _aspect / ratio) : new Vector2(height * ratio, height);
    }
    private static Transform? Find(Transform root, string prefix)
    {
        for (var i = 0; i < root.childCount; i++)
        { var child = root.GetChild(i); if (child.name.StartsWith(prefix)) return child; var nested = Find(child, prefix); if (nested != null) return nested; }
        return null;
    }
    internal void ReleaseMap()
    { if (_docks.TryGetValue("map", out var dock)) { dock.Restore(); _docks.Remove("map"); } }
    private void RestoreNative()
    {
        foreach (var dock in _nativeDocks) dock.Restore(); _nativeDocks.Clear();
        foreach (var slot in _nativeSlots) if (slot != null) UnityEngine.Object.Destroy(slot); _nativeSlots.Clear();
        foreach (var feed in _feeds) if (feed != null) { feed.texture = null; feed.enabled = false; }
    }
    internal void Restore()
    {
        ClosePageMenus();
        if (_targetStatus != null) _targetStatus.text = "";
        _nextReadout = 0; _weaponStatusText = ""; _weaponStatusCaution = false;
        Array.Clear(_statusKnown, 0, _statusKnown.Length);
        foreach (var status in _statusStrip) if (status != null) status.SetActive(false);
        _slave = -1; _mapPan = Vector2.zero; _recenter.Reset();
        RestoreNative(); foreach (var dock in _docks.Values) dock.Restore(); _docks.Clear(); _nextFind = 0;
        _nativeVisibility.Restore();
        _left = _right = (MfdPage)(-1); _aircraft = null; _apps = Array.Empty<HUDApp>(); _appManager = null; _tac = null;
    }
}
