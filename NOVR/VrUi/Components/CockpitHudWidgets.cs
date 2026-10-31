using System.Collections.Generic;
using BepInEx.Configuration;
using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.SpecialBehavior;

// Coordinates belong to the original cockpit viewpoint, never the recentered
// HMD or adjustable seat. Sources retain their own update and visibility rules.
[DefaultExecutionOrder(-1260)]
internal sealed class CockpitHudWidgets : MonoBehaviour
{
    private const float PixelSize = .0007f;
    private sealed class Widget
    {
        internal RectTransform Root = null!, Content = null!;
        internal Canvas Canvas = null!;
        internal CanvasGroup Group = null!;
        internal Image Background = null!, EditPlate = null!;
        internal Text Caption = null!, Placeholder = null!;
        internal bool HasContent;
    }
    private sealed class Drag
    {
        internal readonly ControllerButtonGate Gate = new();
        internal string? Id;
        internal Vector3 Offset;
    }
    private readonly Dictionary<string, Widget> _widgets = new();
    private Dictionary<string, HudWidgetPose> _poses = new();
    private readonly Drag[] _hands = { new(), new() };
    private HudWidgetProfiles _profiles = null!;
    private ConfigEntry<bool> _snap = null!;
    private Aircraft? _aircraft;
    private string _key = "";
    private bool _editing, _resetToDefault;
    private Vector3 _seat, _anchor, _calibration;
    private Quaternion _recenter;
    private Font? _font;
    internal static CockpitHudWidgets? Instance { get; private set; }
    internal static bool Editing => Instance?._editing == true;
    internal static Aircraft? CurrentAircraft => Instance?._aircraft;
    internal string SelectedId { get; private set; } = "weapons";
    internal string AircraftName => _aircraft?.definition?.unitName ?? "No aircraft";
    internal bool SnapEnabled { get => _snap.Value; set => _snap.Value = value; }
    internal bool Available => _aircraft != null && Ready();

    private void Awake()
    {
        Instance = this;
        _profiles = new HudWidgetProfiles(ModConfiguration.Instance.Config);
        _snap = ModConfiguration.Instance.Config.Bind("VR HUD Widgets - Defaults", "Edge snapping", true,
            "Align neighboring widget edges when released nearby. Widget angles stay fixed.");
    }
    private void OnEnable() => Application.onBeforeRender += Place;
    internal static bool Owns(string id) => Instance != null && Instance._aircraft != null && Instance._widgets.ContainsKey(id);
    internal static bool IsVisible(string id) => Owns(id) && Instance!.Ready() && Instance._poses[id].Visible;
    internal static bool TryGetContent(string id, out RectTransform content)
    {
        content = null!;
        if (!Owns(id)) return false;
        content = Instance!._widgets[id].Content;
        return content != null;
    }
    internal static void SetHasContent(string id, bool visible)
    {
        if (Instance != null && Instance._widgets.TryGetValue(id, out var widget)) widget.HasContent = visible;
    }
    internal static Vector2 Size(string id) => id switch
    {
        "weapons" => new Vector2(420, 170),
        "countermeasures" => new Vector2(260, 135),
        "power" => new Vector2(420, 80),
        "threats" => new Vector2(460, 190),
        "ew" => new Vector2(260, 260),
        _ => new Vector2(460, 270)
    };

    private bool Ready() => XRSettings.enabled && _aircraft != null && !_aircraft.disabled &&
        _aircraft.cockpit != null && !_aircraft.cockpit.IsDetached() && _aircraft.cockpitViewPoint != null &&
        CameraStateManager.cameraMode == CameraMode.cockpit && APIBus.MainCamera != null && APIBus.CockpitHudCamera != null &&
        SceneSingleton<CameraStateManager>.i?.followingUnit == _aircraft &&
        !PlayerSettings.cinematicMode && !DynamicMap.mapMaximized && !CursorManager.GetFlag(CursorFlags.GameMenu);

    private void Update()
    {
        GameManager.GetLocalAircraft(out var aircraft);
        if (!XRSettings.enabled || aircraft == null || aircraft.disabled || aircraft.cockpit == null || aircraft.cockpit.IsDetached()) aircraft = null;
        if (aircraft != _aircraft)
        {
            Finish(false);
            ReleaseWidgets();
            _aircraft = aircraft;
            if (aircraft != null && aircraft.definition != null)
            {
                _key = string.IsNullOrEmpty(aircraft.definition.jsonKey) ? aircraft.definition.name : aircraft.definition.jsonKey;
                _poses = _profiles.Load(_key);
                foreach (var id in HudWidgetProfiles.Ids) CreateWidget(id);
            }
        }
        Place();
        if (!_editing) return;
        if (!Ready() || CameraStateManager.cameraMode != CameraMode.cockpit) { Finish(false); return; }
        if (_seat != CockpitProfiles.SeatOffset || _anchor != NOVRHeadsetData.TranslationAnchor ||
            _calibration != NOVRHeadsetData.TranslationCalibrationOffset || _recenter != NOVRHeadsetData.RotationCalibrationOffset)
        { ReleaseHands(); CaptureCalibration(); }
        if (!TrackedControllers.Available || WristMenu.IsOpen || Time.timeScale <= 0 || !GameManager.flightControlsEnabled ||
            (SceneSingleton<RadialMenuMain>.i != null && RadialMenuMain.IsInUse()) ||
            (GameManager.controlMapper != null && GameManager.controlMapper.isOpen)) { ReleaseHands(); return; }
        for (var i = 0; i < 2; i++) MoveHand(i, TrackedControllers.Read(i == 0));
    }

    internal void Begin()
    {
        if (_editing || !Available) return;
        CockpitLayout.Instance?.Finish(false);
        MotionFlightController.Instance?.CancelPlacement();
        MotionFlightController.Instance?.ResetGrips();
        ControllerActions.Instance?.ResetButtons();
        _poses = _profiles.Load(_key);
        _resetToDefault = false;
        _editing = true;
        CaptureCalibration(); ReleaseHands(); Place();
    }
    internal void Finish(bool save)
    {
        if (!_editing) return;
        if (save && GameManager.GetLocalAircraft(out var aircraft) && aircraft == _aircraft)
        {
            if (_resetToDefault) _profiles.ResetAircraft(_key);
            else _profiles.Save(_key, _poses);
        }
        _editing = false; ReleaseHands();
        if (_key.Length > 0) _poses = _profiles.Load(_key);
        MotionFlightController.Instance?.ResetGrips(); ControllerActions.Instance?.ResetButtons();
        Place();
    }
    internal void ResetPreview()
    {
        if (!_editing) return;
        _poses = _profiles.Load(""); _resetToDefault = true; ReleaseHands(); Place();
    }
    internal void UseDefaultForAll()
    {
        if (!_editing || !Available) return;
        _profiles.SaveDefault(_poses);
        // This aircraft follows its newly saved shared default as well.
        _resetToDefault = true; Finish(true);
    }
    internal HudWidgetPose GetPose(string id) => _poses.TryGetValue(id, out var pose) ? pose : HudWidgetProfiles.FactoryDefaults[id];
    internal void SetPose(string id, HudWidgetPose pose)
    {
        if (!_editing || !_poses.ContainsKey(id)) return;
        _poses[id] = HudWidgetProfiles.Sanitize(id, pose); _resetToDefault = false; Place();
    }
    internal void Select(string id) { if (_widgets.ContainsKey(id)) SelectedId = id; }

    private void CaptureCalibration()
    {
        _seat = CockpitProfiles.SeatOffset; _anchor = NOVRHeadsetData.TranslationAnchor;
        _calibration = NOVRHeadsetData.TranslationCalibrationOffset; _recenter = NOVRHeadsetData.RotationCalibrationOffset;
    }
    private bool HandPosition(ControllerSample hand, out Vector3 position)
    {
        position = default;
        var gameRoot = APIBus.MainCamera?.transform.parent;
        var eye = _aircraft?.cockpitViewPoint;
        if (!hand.Valid || gameRoot == null || eye == null) return false;
        position = eye.InverseTransformPoint(gameRoot.TransformPoint(hand.Position));
        return TrackedControllers.Finite(position);
    }
    private void MoveHand(int index, ControllerSample hand)
    {
        var drag = _hands[index];
        var valid = HandPosition(hand, out var point);
        drag.Gate.Update(valid, hand.Grip);
        if (!drag.Gate.Held && drag.Id != null)
        {
            if (drag.Gate.Released)
            {
                if (SnapEnabled) Snap(drag.Id);
                InteractionHaptics.Pulse(hand.Device, InteractionCue.Release);
            }
            drag.Id = null;
        }
        if (drag.Gate.Pressed)
        {
            var id = SelectAt(point, index);
            if (id != null)
            {
                drag.Id = id; SelectedId = id;
                var pose = _poses[id];
                drag.Offset = pose.Position - point;
                InteractionHaptics.Pulse(hand.Device, InteractionCue.Grab);
            }
        }
        if (drag.Id == null || !drag.Gate.Held) return;
        var value = _poses[drag.Id];
        // Controller orientation never changes the widget angle or pivots its
        // offset around the hand. This is a translation-only layout gesture.
        value.Position = point + drag.Offset;
        if (hand.HasAxis && Mathf.Abs(hand.Axis.y) > .25f)
            value.Scale *= Mathf.Exp(hand.Axis.y * Time.unscaledDeltaTime * .75f);
        SetPose(drag.Id, value);
    }
    private string? SelectAt(Vector3 point, int hand)
    {
        string? result = null; var closest = float.PositiveInfinity;
        foreach (var pair in _poses)
        {
            if (_hands[1 - hand].Id == pair.Key) continue;
            var local = Quaternion.Inverse(Quaternion.Euler(pair.Value.Euler)) * (point - pair.Value.Position);
            var half = Size(pair.Key) * (PixelSize * pair.Value.Scale * .5f);
            if (Mathf.Abs(local.x) > half.x + .025f || Mathf.Abs(local.y) > half.y + .025f || Mathf.Abs(local.z) > .075f) continue;
            var distance = local.sqrMagnitude;
            if (distance < closest) { closest = distance; result = pair.Key; }
        }
        return result;
    }
    private void Snap(string id)
    {
        var pose = _poses[id];
        foreach (var other in _poses)
        {
            if (other.Key == id || _hands[0].Id == other.Key || _hands[1].Id == other.Key) continue;
            var basis = Quaternion.Euler(other.Value.Euler);
            if (Quaternion.Angle(Quaternion.Euler(pose.Euler), basis) > .1f) continue;
            var delta = Quaternion.Inverse(basis) * (pose.Position - other.Value.Position);
            if (Mathf.Abs(delta.z) > .025f) continue;
            var size = Size(id) * (PixelSize * pose.Scale);
            var otherSize = Size(other.Key) * (PixelSize * other.Value.Scale);
            var edgeX = (size.x + otherSize.x) * .5f + .012f;
            var edgeY = (size.y + otherSize.y) * .5f + .012f;
            var horizontal = Mathf.Abs(Mathf.Abs(delta.x) - edgeX) < .025f && Mathf.Abs(delta.y) < .025f;
            var vertical = Mathf.Abs(Mathf.Abs(delta.y) - edgeY) < .025f && Mathf.Abs(delta.x) < .025f;
            if (!horizontal && !vertical) continue;
            delta = horizontal ? new Vector3(Mathf.Sign(delta.x) * edgeX, 0, 0) : new Vector3(0, Mathf.Sign(delta.y) * edgeY, 0);
            pose.Position = other.Value.Position + basis * delta;
            SetPose(id, pose); break;
        }
    }

    private void CreateWidget(string id)
    {
        var root = new GameObject("NOVR HUD " + id, typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = APIBus.CockpitHudCamera;
        var widget = new Widget { Root = (RectTransform)root.transform, Canvas = canvas };
        widget.Root.sizeDelta = Size(id);
        widget.Background = Plate("Optional contrast", widget.Root, new Color(.015f, .025f, .03f, 0));
        widget.Content = Rect("Native content", widget.Root, Size(id));
        widget.Group = widget.Content.gameObject.AddComponent<CanvasGroup>();
        widget.Group.blocksRaycasts = widget.Group.interactable = false;
        widget.EditPlate = Plate("Layout preview", widget.Root, new Color(.1f, .6f, .6f, .12f));
        widget.Caption = Label(widget.Root, HudWidgetProfiles.Label(id), new Vector2(0, Size(id).y * .5f + 18), new Vector2(Size(id).x + 50, 32), 20);
        widget.Placeholder = Label(widget.Root, Preview(id), Vector2.zero, Size(id) - new Vector2(12, 12), 20);
        LayerHelper.SetLayerRecursive(root.transform, LayerHelper.GetVrUiLayer());
        _widgets.Add(id, widget);
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.sizeDelta = size; return rect;
    }
    private static Image Plate(string name, RectTransform parent, Color color)
    {
        var rect = Rect(name, parent, parent.sizeDelta); var image = rect.gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false; return image;
    }
    private Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        _font ??= Resources.GetBuiltinResource<Font>("Arial.ttf");
        var rect = Rect("Layout label", parent, size); rect.anchoredPosition = position;
        var label = rect.gameObject.AddComponent<Text>(); label.font = _font; label.text = value;
        label.fontSize = fontSize; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false; return label;
    }
    private static string Preview(string id) => id switch
    {
        "weapons" => "Selected weapon\nAmmunition · readiness",
        "countermeasures" => "Countermeasures\nCount · jammer charge",
        "power" => "Capacitor / charge",
        "threats" => "Incoming missiles\nBearing · distance · notch",
        "ew" => "Electronic warnings\nRadar / jamming bearings",
        _ => "Kill feed\nMission notifications"
    };
    private void Place()
    {
        var ready = Ready();
        var gameRoot = APIBus.MainCamera?.transform.parent;
        var uiRoot = APIBus.CockpitHudCamera?.transform.parent;
        var eye = _aircraft?.cockpitViewPoint;
        foreach (var pair in _widgets)
        {
            var widget = pair.Value;
            if (widget.Root == null) continue;
            var pose = _poses[pair.Key];
            var show = ready && (pose.Visible || _editing);
            widget.Root.gameObject.SetActive(show);
            if (!show || gameRoot == null || uiRoot == null || eye == null) continue;
            widget.Canvas.worldCamera = APIBus.CockpitHudCamera;
            widget.Root.SetPositionAndRotation(uiRoot.TransformPoint(gameRoot.InverseTransformPoint(eye.TransformPoint(pose.Position))),
                uiRoot.rotation * Quaternion.Inverse(gameRoot.rotation) * eye.rotation * Quaternion.Euler(pose.Euler));
            widget.Root.localScale = Vector3.one * (PixelSize * pose.Scale);
            widget.Group.alpha = pose.Visible ? pose.Opacity : 0;
            widget.Background.color = new Color(.015f, .025f, .03f, pose.Visible && widget.HasContent ? pose.Background * .8f : 0);
            widget.EditPlate.enabled = _editing;
            widget.EditPlate.color = pair.Key == SelectedId ? new Color(.1f, .8f, .8f, .22f) : new Color(.1f, .4f, .5f, .10f);
            widget.Caption.enabled = _editing;
            if (_editing)
            {
                var caption = HudWidgetProfiles.Label(pair.Key) + (pose.Visible ? "" : " · HIDDEN");
                if (widget.Caption.text != caption) widget.Caption.text = caption;
            }
            widget.Placeholder.enabled = _editing && (!widget.HasContent || !pose.Visible);
        }
    }
    private void ReleaseHands() { foreach (var hand in _hands) { hand.Id = null; hand.Gate.Reset(); } }
    private void ReleaseWidgets()
    {
        CockpitStatusSources.ReleaseAll();
        CockpitAlerts.ReleaseWidgetParents();
        foreach (var widget in _widgets.Values) if (widget.Root != null) Destroy(widget.Root.gameObject);
        _widgets.Clear();
    }
    private void OnDisable()
    {
        Application.onBeforeRender -= Place; Finish(false); ReleaseWidgets(); _aircraft = null;
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
