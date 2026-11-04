using System.Collections.Generic;
using HarmonyLib;
using NOVR.MotionControls;
using NOVR.VrUi.HarmonyPatches;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.SpecialBehavior;

// A presentation-only view of native detections. Never disables ThreatList,
// MessageUI, KillDisplay or RadarWarning: those own sounds, expiry and gameplay.
[DefaultExecutionOrder(120)]
internal sealed class CockpitAlerts : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<ThreatList, Dictionary<PersistentID, ThreatItem>> Items = AccessTools.FieldRefAccess<ThreatList, Dictionary<PersistentID, ThreatItem>>("itemLookup");
    private static readonly AccessTools.FieldRef<ThreatItem, Missile> MissileOf = AccessTools.FieldRefAccess<ThreatItem, Missile>("missile");
    private static readonly AccessTools.FieldRef<MessageUI, TextMeshProUGUI> FeedOf = AccessTools.FieldRefAccess<MessageUI, TextMeshProUGUI>("killFeedText");
    private static readonly AccessTools.FieldRef<MessageUI, TextMeshProUGUI> MissionOf = AccessTools.FieldRefAccess<MessageUI, TextMeshProUGUI>("messageText");
    private static readonly AccessTools.FieldRef<MessageUI, ChatBox> ChatOf = AccessTools.FieldRefAccess<MessageUI, ChatBox>("chat");
    private static readonly AccessTools.FieldRef<MessageUI, Image> BackgroundOf = AccessTools.FieldRefAccess<MessageUI, Image>("backgroundImage");
    private static readonly AccessTools.FieldRef<KillDisplay, TextMeshProUGUI> RewardOf = AccessTools.FieldRefAccess<KillDisplay, TextMeshProUGUI>("killText");
    private static readonly AccessTools.FieldRef<KillDisplay, TextMeshProUGUI> RankOf = AccessTools.FieldRefAccess<KillDisplay, TextMeshProUGUI>("rankText");
    private static readonly AccessTools.FieldRef<KillDisplay, Image> RankBackground = AccessTools.FieldRefAccess<KillDisplay, Image>("rankBackground");
    private sealed class Signal
    {
        internal Unit Source = null!;
        internal bool Jam;
        internal Vector3 Direction;
        internal float Expires;
    }
    private sealed class TextMirror
    {
        internal readonly TMP_Text Source;
        internal bool Visible { get; private set; }
        private readonly TextMeshProUGUI[] _copies;
        private readonly HudGraphicVisibilityLease _hide;
        private string? _last;
        internal TextMirror(TMP_Text source, Transform parent, Vector2 position, Vector2 size, int fontSize, bool feed = false)
        {
            Source = source;
            _hide = new HudGraphicVisibilityLease(source, true);
            _copies = new TextMeshProUGUI[feed ? 3 : 1];
            for (var i = 0; i < _copies.Length; i++)
            {
                var go = new GameObject("Compact notification", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer;
                var copy = go.GetComponent<TextMeshProUGUI>(); _copies[i] = copy;
                copy.font = source.font; copy.fontSharedMaterial = source.fontSharedMaterial;
                copy.text = ""; copy.fontSize = fontSize; copy.richText = false; copy.raycastTarget = false;
                copy.alignment = TextAlignmentOptions.TopLeft; copy.enableWordWrapping = !feed;
                copy.overflowMode = TextOverflowModes.Ellipsis;
                copy.maxVisibleLines = feed ? 1 : 2;
                var height = size.y / _copies.Length;
                copy.rectTransform.sizeDelta = new Vector2(size.x, height);
                copy.rectTransform.anchoredPosition = position + new Vector2(0, size.y * .5f - height * (i + .5f));
            }
        }
        internal void Refresh(bool active, bool show, bool feed, float opacity = 1, bool suppressNative = false)
        {
            var visible = active && show && Source != null && Source.isActiveAndEnabled && !string.IsNullOrWhiteSpace(Source.text);
            if (suppressNative) _hide.Apply(); else _hide.Restore();
            Visible = visible;
            foreach (var copy in _copies) copy.enabled = visible;
            if (!visible) return;
            var text = Source!.text;
            if (_last != text)
            {
                _last = text;
                var compact = CockpitAlertMath.LatestFeed(text, feed ? 3 : 2);
                var lines = feed ? compact.Split('\n') : new[] { compact };
                for (var i = 0; i < _copies.Length; i++) _copies[i].text = i < lines.Length ? lines[i] : "";
            }
            var color = feed ? new Color(.72f, .82f, .84f, .85f) : Source.color;
            color.a *= opacity;
            foreach (var copy in _copies) copy.color = color;
        }
        internal void Release()
        {
            _hide.Release();
            foreach (var copy in _copies) if (copy != null) Destroy(copy.gameObject);
        }
    }

    internal static CockpitAlerts? Instance { get; private set; }
    // Existing notch/backplate callers need to know whether the missile list
    // was actually replaced, not merely whether some other widget is running.
    internal static bool Active => Instance is { } self && self.Replaces("threats", self._missileRoot);
    internal static bool ReplacesEw => Instance is { } self && self.Replaces("ew", self._root);
    internal static bool InRecovery => Instance?._recovering == true;
    private Aircraft? _aircraft;
    private ThreatList? _list;
    private HudCanvasGroupLease? _listHide;
    private HudGraphicVisibilityLease? _rankHide;
    private Image? _rankBackground;
    private MessageUI? _messages;
    private HudGraphicVisibilityLease? _messageBackgroundHide;
    private TextMirror? _feed, _mission, _reward, _rank;
    private readonly List<Signal> _signals = new();
    private readonly List<ThreatItem> _missiles = new();
    private readonly List<Text> _pins = new();
    private readonly List<Text> _rows = new();
    private GameObject? _root, _threatRoot, _missileRoot;
    private Transform? _feedRoot;
    private Image? _feedBackplate, _missileBackplate;
    private Text? _heading, _missileHeading;
    private bool _active;
    private bool _hadCockpit, _recovering;
    private FlightHud? _sessionHud;
    private CockpitFeedMode _feedMode;
    private HudWidgetPose _feedPose = HudWidgetProfiles.FactoryDefaults["feed"];
    private float _nextRead, _nextFind;
    private static readonly Color MissileColor = new(1f, .34f, .20f);
    private static readonly Color RadarColor = new(1f, .74f, .25f);
    private static readonly Color JamColor = new(.45f, .85f, 1f);
    private CockpitAlertSettings Settings => ModConfiguration.Instance.CockpitAlerts;

    private void Awake() => Instance = this;
    private void OnEnable() => Application.onBeforeRender += Place;

    internal static void Record(Aircraft aircraft, Unit source, bool jam)
    {
        var self = Instance;
        if (self == null || aircraft == null || source == null || !GameManager.GetLocalAircraft(out var local) || local != aircraft) return;
        self.SetAircraft(aircraft);
        Signal? signal = null;
        foreach (var candidate in self._signals)
            if (candidate.Source == source && candidate.Jam == jam) { signal = candidate; break; }
        if (signal == null)
        {
            // Bound memory and geometry even in missions with many emitters.
            if (self._signals.Count >= 24) self._signals.RemoveAt(0);
            signal = new Signal { Source = source, Jam = jam }; self._signals.Add(signal);
        }
        // Radar bearing is sampled only when the game reports a detection.
        signal.Direction = (source.transform.position - aircraft.transform.position).normalized;
        signal.Expires = Time.timeSinceLevelLoad + (jam ? 1f : 4f);
    }

    private void SetAircraft(Aircraft? aircraft)
    {
        if (_aircraft == aircraft) return;
        // MessageUI survives aircraft destruction and respawn. Keep its leases
        // and copies alive so a crash cannot reveal the old flat feed for a frame.
        _signals.Clear(); _missiles.Clear();
        if (_missileRoot != null) _missileRoot.SetActive(false);
        foreach (var row in _rows) row.text = "";
        _aircraft = aircraft; _nextRead = _nextFind = 0;
    }

    private bool Ready() => Settings.Enabled.Value && XRSettings.enabled && _aircraft != null && !LostCockpit(_aircraft) &&
        _aircraft.cockpit != null && !_aircraft.cockpit.IsDetached() &&
        CameraStateManager.cameraMode == CameraMode.cockpit && GameManager.flightControlsEnabled &&
        NOUIManager.I != null && APIBus.MainCamera != null &&
        SceneSingleton<CameraStateManager>.i?.followingUnit == _aircraft &&
        Time.timeScale > 0 && !PlayerSettings.cinematicMode && !DynamicMap.mapMaximized && !CursorManager.GetFlag(CursorFlags.GameMenu) &&
        (SceneSingleton<RadialMenuMain>.i == null || !RadialMenuMain.IsInUse()) &&
        (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen);

    private void LateUpdate()
    {
        GameManager.GetLocalAircraft(out var aircraft); SetAircraft(aircraft);
        var wasActive = _active; _active = Ready();
        if (wasActive && !_active) ThreatItemNotchDirectionPatch.RestoreCompactIndicators();
        var playing = GameManager.gameState == GameState.SinglePlayer || GameManager.gameState == GameState.Multiplayer;
        var hud = SceneSingleton<FlightHud>.i;
        if (!playing || !XRSettings.enabled || hud != _sessionHud) _hadCockpit = false;
        _sessionHud = hud;
        var lostCockpit = LostCockpit(_aircraft);
        if (playing && XRSettings.enabled && !lostCockpit && CameraStateManager.cameraMode == CameraMode.cockpit)
            _hadCockpit = true;
        _recovering = XRSettings.enabled && playing && _hadCockpit && lostCockpit;
        CockpitStatusSources.RefreshInstructions();
        if (_active || _hadCockpit && playing && XRSettings.enabled && (Settings.Enabled.Value || _recovering) && NOUIManager.I != null)
        {
            EnsureUi();
            if (Time.unscaledTime >= _nextFind) { _nextFind = Time.unscaledTime + 1; FindSources(); }
            if (_active && Time.unscaledTime >= _nextRead) { _nextRead = Time.unscaledTime + .1f; RefreshThreats(); }
        }
        if (CockpitHudWidgets.CurrentAircraft == _aircraft && CockpitHudWidgets.Owns("feed") && CockpitHudWidgets.Instance != null)
            _feedPose = CockpitHudWidgets.Instance.GetPose("feed");
        var chat = _messages != null ? ChatOf(_messages) : null;
        var chatOpen = chat != null && chat.gameObject.activeSelf;
        var unobstructed = NOUIManager.I != null && APIBus.MainCamera != null &&
            !PlayerSettings.cinematicMode && !DynamicMap.mapMaximized && !CursorManager.GetFlag(CursorFlags.GameMenu) && !chatOpen &&
            (SceneSingleton<RadialMenuMain>.i == null || !RadialMenuMain.IsInUse()) &&
            (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen);
        _feedMode = CockpitFeedPresentation.Choose((Settings.Enabled.Value || _recovering) && XRSettings.enabled, playing, unobstructed,
            _active, _hadCockpit, lostCockpit);
        Place();
        var hasThreats = _missiles.Count > 0;
        var hasEw = hasThreats || _signals.Count > 0;
        var recovery = _feedMode == CockpitFeedMode.Recovery;
        var feedVisible = _feedMode == CockpitFeedMode.Cockpit ? WidgetVisible("feed") : recovery && _feedPose.Visible;
        // Ownership, not whether there is currently text, controls the source.
        // Hiding the widget must never bring back its old vanilla duplicate.
        var suppressNativeFeed = _feedMode != CockpitFeedMode.Native;
        var feedOpacity = recovery ? _feedPose.Opacity : 1;
        if (_root != null) _root.SetActive(WidgetVisible("ew") && hasEw);
        if (_missileRoot != null) _missileRoot.SetActive(WidgetVisible("threats") && hasThreats);
        if (_feedRoot != null) _feedRoot.gameObject.SetActive(feedVisible);
        if (Active) _listHide?.Apply(0); else _listHide?.Restore();
        _feed?.Refresh(feedVisible, Settings.Feed.Value, true, feedOpacity, suppressNativeFeed);
        // Typing/expanded chat keeps its native history and input presentation.
        // Outside chat, mirror only the latest two live mission-message lines.
        _mission?.Refresh(feedVisible && !chatOpen, true, false, feedOpacity, suppressNativeFeed && !chatOpen);
        _reward?.Refresh(feedVisible, true, false, feedOpacity, suppressNativeFeed); _rank?.Refresh(feedVisible, true, false, feedOpacity, suppressNativeFeed);
        var hasFeed = _feed?.Visible == true || _mission?.Visible == true || _reward?.Visible == true || _rank?.Visible == true;
        if (suppressNativeFeed) _rankHide?.Apply(); else _rankHide?.Restore();
        if (_messageBackgroundHide != null && _messages != null)
        {
            if (suppressNativeFeed && !chatOpen) _messageBackgroundHide.Apply();
            else _messageBackgroundHide.Restore();
        }
        if (_feedBackplate != null)
        {
            _feedBackplate.enabled = feedVisible && hasFeed && (recovery || !CockpitHudWidgets.Owns("feed"));
            _feedBackplate.color = new Color(.015f, .025f, .03f, recovery ? _feedPose.Background : .12f);
        }
        if (_missileBackplate != null) _missileBackplate.enabled = !CockpitHudWidgets.Owns("threats");
        if (_heading != null) _heading.enabled = CockpitHudWidgets.Editing;
        if (_missileHeading != null) _missileHeading.enabled = CockpitHudWidgets.Editing;
        CockpitHudWidgets.SetHasContent("threats", _active && hasThreats);
        CockpitHudWidgets.SetHasContent("ew", _active && hasEw);
        CockpitHudWidgets.SetHasContent("feed", _active && hasFeed);
    }

    internal static bool LostCockpit(Aircraft? aircraft)
    {
        if (aircraft == null || aircraft.disabled || aircraft.HasEjected() || aircraft.cockpit == null || aircraft.cockpit.IsDetached()) return true;
        foreach (var pilot in aircraft.pilots)
            if (pilot != null && !pilot.dead && !pilot.ejected) return false;
        return true;
    }

    private bool WidgetVisible(string id) => _active &&
        (!CockpitHudWidgets.Owns(id) || CockpitHudWidgets.CurrentAircraft == _aircraft && CockpitHudWidgets.IsVisible(id));

    private bool Replaces(string id, GameObject? root) => WidgetVisible(id) && root != null && root.activeInHierarchy;

    private void FindSources()
    {
        var flightHud = SceneSingleton<FlightHud>.i;
        var list = flightHud != null ? flightHud.GetComponentInChildren<ThreatList>(true) : null;
        if (_list != list)
        {
            _listHide?.Release(); _list = list;
            _listHide = list != null ? new HudCanvasGroupLease(list.gameObject) : null;
        }
        var messages = SceneSingleton<MessageUI>.i;
        if (_messages != messages)
        {
            _messageBackgroundHide?.Release();
            _messages = messages;
            var messageBackground = messages != null ? BackgroundOf(messages) : null;
            _messageBackgroundHide = messageBackground != null ? new HudGraphicVisibilityLease(messageBackground) : null;
        }
        var source = messages != null ? FeedOf(messages) : null;
        if (_feed?.Source != source)
        {
            _feed?.Release(); _feed = source == null ? null : new TextMirror(source, _feedRoot!, new Vector2(0, 53), new Vector2(480, 84), 20, true);
        }
        var mission = messages != null ? MissionOf(messages) : null;
        if (_mission?.Source != mission)
        {
            _mission?.Release(); _mission = mission == null ? null : new TextMirror(mission, _feedRoot!, new Vector2(0, -54), new Vector2(480, 72), 18);
        }
        var kills = SceneSingleton<KillDisplay>.i;
        var reward = kills != null ? RewardOf(kills) : null;
        if (_reward?.Source != reward)
        {
            _reward?.Release(); _reward = reward == null ? null : new TextMirror(reward, _feedRoot!, new Vector2(0, 126), new Vector2(480, 28), 20);
        }
        var rank = kills != null ? RankOf(kills) : null;
        if (_rank?.Source != rank)
        {
            _rank?.Release(); _rank = rank == null ? null : new TextMirror(rank, _feedRoot!, new Vector2(0, -126), new Vector2(480, 28), 18);
        }
        var background = kills != null ? RankBackground(kills) : null;
        if (_rankBackground != background)
        {
            _rankHide?.Release();
            _rankBackground = background;
            _rankHide = background == null ? null : new HudGraphicVisibilityLease(background);
        }
    }

    private void Place()
    {
        if (_feedMode == CockpitFeedMode.Recovery) PlaceRecoveryFeed();
        if (!_active || _root == null || _aircraft == null || _aircraft.cockpitViewPoint == null) return;
        var gameCamera = APIBus.MainCamera;
        if (gameCamera == null || NOUIManager.I == null) return;
        var gameRoot = gameCamera.transform.parent;
        var uiRoot = APIBus.CockpitHudCamera.transform.parent;
        if (gameRoot == null || uiRoot == null) return;
        var ewOwned = AttachWidget("ew", _root.transform, new Vector2(260, 260));
        var threatsOwned = AttachWidget("threats", _missileRoot!.transform, new Vector2(520, 200));
        var feedOwned = AttachWidget("feed", _feedRoot!, new Vector2(520, 300));
        var eye = _aircraft.cockpitViewPoint;
        // Same cockpit-to-UI-space conversion as the dashboard. Physical leaning,
        // head turns and saved seat shifts do not drag the overlay around.
        if (!ewOwned)
        {
            var position = CockpitWidgetSpace.ToWorld(eye.position, eye.rotation, new Vector3(-.30f, Settings.Height.Value, 1.4f));
            _root.transform.SetPositionAndRotation(uiRoot.TransformPoint(gameRoot.InverseTransformPoint(position)),
                uiRoot.rotation * Quaternion.Inverse(gameRoot.rotation) * eye.rotation);
            _root.transform.localScale = Vector3.one * (.00085f * Settings.Scale.Value);
        }
        // Other aircraft retain the factory-seat reference; without a fitted
        // coaming profile, place the two feeds below the forward eye line.
        if (!threatsOwned) PlaceWindow(_missileRoot!.transform, CockpitWidgetSpace.ToWorld(eye.position, eye.rotation, new Vector3(-.30f, Settings.Height.Value + .18f, 1.4f)),
            eye.rotation * Quaternion.Euler(5,-12,0), .0007f, gameRoot, uiRoot);
        if (!feedOwned) PlaceWindow(_feedRoot!, CockpitWidgetSpace.ToWorld(eye.position, eye.rotation, new Vector3(.30f, Settings.Height.Value + .18f, 1.4f)),
            eye.rotation * Quaternion.Euler(5,12,0), .0007f, gameRoot, uiRoot);
    }

    private void PlaceRecoveryFeed()
    {
        if (_feedRoot == null || NOUIManager.I == null) return;
        var camera = APIBus.CockpitHudCamera;
        if (camera == null) return;
        if (_feedRoot.parent != transform) _feedRoot.SetParent(transform, false);
        // No surviving seat is required. Keep crash/respawn messages compact and
        // below-right of the recovery view until a cockpit or full menu returns.
        _feedRoot.SetPositionAndRotation(camera.transform.TransformPoint(new Vector3(.24f, -.18f, .85f)), camera.transform.rotation);
        _feedRoot.localScale = Vector3.one * (.0007f * _feedPose.Scale * (460f / 520f));
        var canvas = _feedRoot.GetComponent<Canvas>();
        if (canvas != null) canvas.worldCamera = camera;
    }

    private bool AttachWidget(string id, Transform window, Vector2 dimensions)
    {
        if (CockpitHudWidgets.CurrentAircraft == _aircraft && CockpitHudWidgets.TryGetContent(id, out var content))
        {
            if (window.parent != content) window.SetParent(content, false);
            var rect = (RectTransform)window;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = dimensions;
            var scale = Mathf.Min(content.rect.width / dimensions.x, content.rect.height / dimensions.y);
            window.localPosition = Vector3.zero; window.localRotation = Quaternion.identity;
            window.localScale = Vector3.one * scale;
            return true;
        }
        if (window.parent != transform) window.SetParent(transform, false);
        return false;
    }

    // Called by the widget manager before destroying its content slots. Alert
    // views belong to this component and must not be destroyed with a layout.
    internal static void ReleaseWidgetParents()
    {
        var self = Instance; if (self == null) return;
        foreach (var root in new[] { self._root?.transform, self._missileRoot?.transform, self._feedRoot })
            if (root != null) { root.SetParent(self.transform, false); root.gameObject.SetActive(false); }
        self._active = false;
        self._listHide?.Restore();
        // LateUpdate chooses recovery or native presentation before rendering.
        // Do not reveal native feed sources during the widget teardown itself.
        ThreatItemNotchDirectionPatch.RestoreCompactIndicators();
    }

    private void PlaceWindow(Transform window, Vector3 position, Quaternion rotation, float scale, Transform gameRoot, Transform uiRoot)
    {
        window.SetPositionAndRotation(uiRoot.TransformPoint(gameRoot.InverseTransformPoint(position)),
            uiRoot.rotation * Quaternion.Inverse(gameRoot.rotation) * rotation);
        window.localScale = Vector3.one * (scale * Settings.Scale.Value);
    }

    private void RefreshThreats()
    {
        if (_aircraft == null) return;
        _missiles.Clear();
        if (_list != null)
            foreach (var entry in Items(_list))
                if (entry.Value != null && MissileOf(entry.Value) != null) _missiles.Add(entry.Value);
        _missiles.Sort(CompareThreats);
        var now = Time.timeSinceLevelLoad;
        _signals.RemoveAll(s => s.Source == null || s.Expires <= now);
        _threatRoot!.SetActive(_missiles.Count > 0 || _signals.Count > 0);
        foreach (var pin in _pins) pin.enabled = false;
        foreach (var row in _rows) row.text = "";
        _heading!.text = _missiles.Count > 0 ? "THREAT BEARINGS" : "RADAR / EW";
        _heading.color = RadarColor;
        _missileHeading!.text = $"INCOMING  {_missiles.Count}" + (_missiles.Count > 3 ? $"  ·  +{_missiles.Count - 3} MORE" : "");
        _missileHeading.color = MissileColor;
        var pinIndex = 0;
        for (var i = 0; i < _missiles.Count; i++)
        {
            var item = _missiles[i]; var missile = MissileOf(item);
            var direction = _aircraft.transform.InverseTransformDirection(missile.transform.position - _aircraft.transform.position);
            Pin(ref pinIndex, direction, "M", MissileColor, 70);
            if (i >= 3) continue;
            var height = direction.y > direction.magnitude * .2f ? " HIGH" : direction.y < -direction.magnitude * .2f ? " LOW" : "";
            var text = $"{missile.GetSeekerType()}  {CockpitAlertMath.Clock(direction.x, direction.z)}h{height}  {UnitConverter.DistanceReading(direction.magnitude)}";
            if (ThreatItemNotchDirectionPatch.TryGetNotchDirection(item, out var notch) && missile.GetSeekerType().Contains("RH"))
            {
                notch = _aircraft.transform.InverseTransformDirection(notch);
                text += $"\nNOTCH {CockpitAlertMath.Clock(notch.x, notch.z)}h";
            }
            _rows[i].text = text; _rows[i].color = MissileColor;
        }
        var radar = 0; var jamming = 0;
        foreach (var signal in _signals)
        {
            var direction = _aircraft.transform.InverseTransformDirection(signal.Direction);
            var color = signal.Jam ? JamColor : RadarColor;
            color.a = Mathf.Clamp01((signal.Expires - now) / (signal.Jam ? 1f : 4f));
            Pin(ref pinIndex, direction, signal.Jam ? "J" : "R", color, signal.Jam ? 88 : 52);
            if (signal.Jam) jamming++; else radar++;
        }
        _rows[3].text = $"RADAR {radar}  ·  JAM {jamming}";
        _rows[3].color = RadarColor;
    }

    private int CompareThreats(ThreatItem a, ThreatItem b)
    {
        var first = MissileOf(a); var second = MissileOf(b);
        var priority = (second.seekerMode == Missile.SeekerMode.activeLock).CompareTo(first.seekerMode == Missile.SeekerMode.activeLock);
        return priority != 0 ? priority : (first.transform.position - _aircraft!.transform.position).sqrMagnitude.CompareTo((second.transform.position - _aircraft!.transform.position).sqrMagnitude);
    }
    private void Pin(ref int index, Vector3 direction, string symbol, Color color, float radius)
    {
        if (index >= _pins.Count) return;
        var pin = _pins[index++]; var p = CockpitAlertMath.Scope(direction.x, direction.z, radius);
        pin.rectTransform.anchoredPosition = new Vector2(p.x, p.y) * .7f;
        pin.text = symbol; pin.color = color; pin.enabled = true;
    }

    private void EnsureUi()
    {
        if (_root != null) return;
        _root = new GameObject("NOVR frameless cockpit alerts", typeof(RectTransform), typeof(Canvas));
        _root.transform.SetParent(transform, false);
        ((RectTransform)_root.transform).sizeDelta = new Vector2(260, 260);
        var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = APIBus.CockpitHudCamera;
        var group = _root.AddComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
        _threatRoot = new GameObject("Threat scope", typeof(RectTransform));
        _threatRoot.transform.SetParent(_root.transform, false);
        _missileRoot = CreateWindow("NOVR incoming missile window", out _missileBackplate);
        _feedRoot = CreateWindow("NOVR notifications window", out _feedBackplate, 300).transform;
        _missileHeading = Label(_missileRoot.transform, "", new Vector2(0, 69), new Vector2(480, 28), 20);
        _missileHeading.alignment = TextAnchor.MiddleLeft;
        for (var i = 0; i < 36; i++)
        {
            var angle = i * Mathf.PI * 2 / 36;
            var tick = new GameObject("Bearing tick", typeof(RectTransform), typeof(Image));
            tick.transform.SetParent(_threatRoot.transform, false);
            var rect = (RectTransform)tick.transform;
            rect.anchoredPosition = new Vector2(Mathf.Sin(angle) * 58, Mathf.Cos(angle) * 58);
            rect.sizeDelta = new Vector2(1, i % 9 == 0 ? 9 : 3);
            rect.localRotation = Quaternion.Euler(0, 0, -angle * Mathf.Rad2Deg);
            var image = tick.GetComponent<Image>(); image.raycastTarget = false; image.color = new Color(.55f, .7f, .72f, .45f);
        }
        Label(_threatRoot.transform, "12", new Vector2(0, 85), new Vector2(40, 25), 16);
        Label(_threatRoot.transform, "6", new Vector2(0, -85), new Vector2(40, 25), 16);
        Label(_threatRoot.transform, "9", new Vector2(-94, 0), new Vector2(30, 25), 16);
        Label(_threatRoot.transform, "3", new Vector2(94, 0), new Vector2(30, 25), 16);
        Label(_threatRoot.transform, "▲", Vector2.zero, new Vector2(30, 30), 20);
        _heading = Label(_threatRoot.transform, "", new Vector2(0, 116), new Vector2(240, 24), 17);
        for (var i = 0; i < 4; i++)
        {
            var row = i < 3
                ? Label(_missileRoot.transform, "", new Vector2(0, 29 - i * 51), new Vector2(480, 48), 20)
                : Label(_threatRoot.transform, "", new Vector2(0, -116), new Vector2(240, 24), 15);
            row.alignment = i < 3 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter; _rows.Add(row);
        }
        for (var i = 0; i < 40; i++) _pins.Add(Label(_threatRoot.transform, "", Vector2.zero, new Vector2(24, 24), 20));
        LayerHelper.SetLayerRecursive(_root.transform, LayerHelper.GetVrUiLayer());
        LayerHelper.SetLayerRecursive(_missileRoot.transform, LayerHelper.GetVrUiLayer());
    }
    private GameObject CreateWindow(string name, out Image backplate, float height = 200)
    {
        var window = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        window.transform.SetParent(transform, false);
        ((RectTransform)window.transform).sizeDelta = new Vector2(520, height);
        var canvas = window.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = APIBus.CockpitHudCamera;
        var group = window.GetComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
        // A faint glass-like tint and text shadow retain contrast over sky and
        // terrain, without an opaque panel, border, or controller hit target.
        var background = new GameObject("Transparent backing", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(window.transform, false);
        ((RectTransform)background.transform).sizeDelta = new Vector2(520, height);
        backplate = background.GetComponent<Image>(); backplate.color = new Color(.015f,.025f,.03f,.12f); backplate.raycastTarget = false;
        LayerHelper.SetLayerRecursive(window.transform, LayerHelper.GetVrUiLayer());
        return window;
    }
    private static Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject("Alert text", typeof(RectTransform), typeof(Text), typeof(Shadow));
        go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer;
        var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = value; text.fontSize = fontSize; text.supportRichText = false;
        text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        text.color = new Color(.72f, .82f, .84f);
        text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position;
        var shadow = go.GetComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .85f); shadow.effectDistance = new Vector2(1, -1);
        return text;
    }
    private void ReleaseSources()
    {
        _listHide?.Release(); _rankHide?.Release(); _messageBackgroundHide?.Release();
        _feed?.Release(); _mission?.Release(); _reward?.Release(); _rank?.Release();
        _feed = _mission = _reward = _rank = null; _list = null; _listHide = null; _rankHide = null; _rankBackground = null;
        _messageBackgroundHide = null; _messages = null;
    }
    private void OnDisable()
    {
        Application.onBeforeRender -= Place; _active = _hadCockpit = _recovering = false; _sessionHud = null; _feedMode = CockpitFeedMode.Native; ReleaseSources();
        ThreatItemNotchDirectionPatch.RestoreCompactIndicators();
        if (_root != null) _root.SetActive(false);
        if (_missileRoot != null) _missileRoot.SetActive(false);
        if (_feedRoot != null) _feedRoot.gameObject.SetActive(false);
    }
    private void OnDestroy() { ReleaseSources(); if (Instance == this) Instance = null; }
}
