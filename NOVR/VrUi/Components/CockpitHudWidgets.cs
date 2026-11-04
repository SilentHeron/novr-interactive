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
        internal Image Background = null!;
        internal CockpitWidgetAppearanceEditor Appearance = null!;
        internal readonly Image[] Corners = new Image[4];
        internal readonly Image[] Outline = new Image[4];
        internal Text Caption = null!, Placeholder = null!;
        internal bool HasContent;
    }
    private sealed class Drag
    {
        internal readonly ControllerButtonGate Gate = new();
        internal string? Id;
        internal Vector3 LocalAnchor;
        internal Vector2 StartAngles, GripAngles;
        internal Quaternion CornerRotation;
        internal int Corner = -1;
    }
    private readonly Dictionary<string, Widget> _widgets = new();
    private Dictionary<string, HudWidgetPose> _poses = new();
    private Dictionary<string, HudWidgetPose>? _editStart;
    private readonly Drag[] _hands = { new(), new() };
    private readonly ControllerSample[] _samples = new ControllerSample[2];
    private readonly Vector3[] _points = new Vector3[2];
    private readonly Vector2[] _handAngles = new Vector2[2];
    private readonly bool[] _angleValid = new bool[2];
    private readonly bool[] _appearanceBusy = new bool[2];
    private string? _menuAppearanceFocus;
    private readonly string?[] _hover = new string?[2];
    private string? _resizing;
    private HudWidgetTwoHandGesture _resizeGesture;
    private HudWidgetProfiles _profiles = null!;
    private ConfigEntry<bool> _snap = null!;
    private Aircraft? _aircraft;
    private string _key = "";
    private int _slot = 1;
    internal int ProfileSlot => _slot;
    internal string ProfileAircraftName => CockpitProfiles.Instance?.AircraftName ?? "Aircraft";
    private bool _editing, _initialPlacementDone;
    private Vector3 _seat, _anchor, _calibration;
    private Quaternion _recenter;
    private Font? _font;
    internal static CockpitHudWidgets? Instance { get; private set; }
    internal static bool Editing => Instance?._editing == true;
    internal static bool IsHandBusy(bool left) => Editing &&
        (Instance!._hands[left ? 0 : 1].Id != null || Instance._appearanceBusy[left ? 0 : 1]);
    internal static bool AppearancePointing(bool left) => Editing &&
        (Instance!._appearanceBusy[left ? 0 : 1] ||
         Instance._hands[left ? 0 : 1].Id == null && Instance._hover[left ? 0 : 1] != null);
    internal static bool TryGetCornerHandPose(bool left, out Vector3 worldPosition, out Quaternion worldRotation)
    {
        worldPosition = default; worldRotation = Quaternion.identity;
        var self = Instance;
        if (self == null || !self._editing || !self.Ready() || !self.Space(out _, out _, out var eye)) return false;
        var drag = self._hands[left ? 0 : 1];
        if (drag.Id == null || drag.Corner < 0 || !drag.Gate.Held || !self._poses.TryGetValue(drag.Id, out var pose)) return false;
        var half = Size(drag.Id) * (PixelSize * pose.Scale * .5f);
        var corner = new Vector3((drag.Corner & 1) == 0 ? -half.x : half.x, (drag.Corner & 2) == 0 ? -half.y : half.y, 0);
        var rotation = Quaternion.Euler(pose.Euler);
        worldPosition = CockpitWidgetSpace.ToWorld(eye.position, eye.rotation, pose.Position + rotation * corner);
        worldRotation = eye.rotation * rotation * drag.CornerRotation;
        return true;
    }
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
        "map" => new Vector2(300, 300),
        "damage" => new Vector2(300, 300),
        "objectives" => new Vector2(420, 150),
        _ => new Vector2(460, 270)
    };

    private bool Ready()
    {
        var cameraState = SceneSingleton<CameraStateManager>.i;
        var gameCamera = APIBus.MainCamera;
        return XRSettings.enabled && _aircraft != null && !CockpitAlerts.LostCockpit(_aircraft) &&
            _aircraft.cockpit != null && !_aircraft.cockpit.IsDetached() && _aircraft.cockpitViewPoint != null &&
            CameraStateManager.cameraMode == CameraMode.cockpit && NOUIManager.I != null &&
            gameCamera != null && APIBus.CockpitHudCamera != null &&
            cameraState != null && cameraState.followingUnit == _aircraft &&
            gameCamera.transform.parent == cameraState.transform && gameCamera.GetComponent<NOVRPoseDriver>() != null &&
            !PlayerSettings.cinematicMode && !DynamicMap.mapMaximized && !CursorManager.GetFlag(CursorFlags.GameMenu);
    }

    private bool Space(out Transform gameRoot, out Transform uiRoot, out Transform eye)
    {
        gameRoot = uiRoot = eye = null!;
        // Unity's destroyed objects are not CLR null: ?.transform still throws
        // during mission changes. Check their native lifetime before accessing.
        if (_aircraft == null || _aircraft.cockpitViewPoint == null || NOUIManager.I == null) return false;
        var gameCamera = APIBus.MainCamera; var uiCamera = APIBus.CockpitHudCamera;
        if (gameCamera == null || uiCamera == null) return false;
        var cameraState = SceneSingleton<CameraStateManager>.i;
        if (cameraState == null || cameraState.followingUnit != _aircraft ||
            gameCamera.transform.parent != cameraState.transform || gameCamera.GetComponent<NOVRPoseDriver>() == null) return false;
        gameRoot = gameCamera.transform.parent; uiRoot = uiCamera.transform.parent; eye = _aircraft.cockpitViewPoint;
        return gameRoot != null && uiRoot != null;
    }

    private void Update()
    {
        GameManager.GetLocalAircraft(out var aircraft);
        if (!XRSettings.enabled || CockpitAlerts.LostCockpit(aircraft)) aircraft = null;
        if (aircraft != _aircraft || _slot != CockpitProfiles.ActiveSlot)
        {
            Finish(false);
            ReleaseWidgets();
            _aircraft = aircraft; _slot = CockpitProfiles.ActiveSlot;
            _initialPlacementDone = false;
            if (aircraft != null && aircraft.definition != null)
            {
                _key = string.IsNullOrEmpty(aircraft.definition.jsonKey) ? aircraft.definition.name : aircraft.definition.jsonKey;
                _poses = _profiles.Load(_key, _slot);
                foreach (var id in HudWidgetProfiles.Ids) CreateWidget(id);
            }
        }
        if (!_initialPlacementDone && Ready())
        {
            // Only untouched first-use defaults may be fitted to the pilot.
            // Saved layouts stay aircraft-fixed, regardless of current reach.
            _initialPlacementDone = _profiles.HasSavedLayout(_key, _slot) || EnsureWidgetsWithinReach(false);
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
        for (var i = 0; i < 2; i++)
        {
            _samples[i] = TrackedControllers.Read(i == 0);
            UpdateAppearance(i, _samples[i]);
            UpdateHand(i, _samples[i]);
        }
        MoveHeldWidgets();
    }

    internal void Begin()
    {
        if (_editing || !Available) return;
        CockpitLayout.Instance?.Finish(false);
        MotionFlightController.Instance?.CancelPlacement();
        MotionFlightController.Instance?.ResetGrips();
        ControllerActions.Instance?.ResetButtons();

        _editing = true;
        if (CockpitLayout.Instance?.BeginUnified() != true) { _editing = false; return; }
        CockpitProfiles.Instance?.BeginPreview();
        // Begin at the exact displayed/saved layout. Reach recovery is an
        // explicit command, never a side effect of opening the editor.
        _editStart = new Dictionary<string, HudWidgetPose>(_poses);
        CaptureCalibration(); ReleaseHands(); Place();
    }
    internal void Finish(bool save)
    {
        if (!_editing) return;
        if (save) { SaveToSlot(_slot); return; }
        _editing = false; ReleaseHands();
        CockpitLayout.Instance?.FinishUnified(false);
        CockpitProfiles.Instance?.EndPreview();
        if (_editStart != null) _poses = _editStart;
        _editStart = null;
        MotionFlightController.Instance?.ResetGrips(); ControllerActions.Instance?.ResetButtons();
        Place();
    }
    internal bool SaveToSlot(int slot)
    {
        if (slot < 1 || slot > 3 || !_editing || !Available || CockpitLayout.Instance?.CanSaveUnified != true ||
            !GameManager.GetLocalAircraft(out var aircraft) || aircraft != _aircraft || CockpitProfiles.Instance == null) return false;
        var stick = CockpitLayout.StickPreview ?? CockpitProfiles.StickPosition;
        var throttle = CockpitLayout.ThrottlePreview ?? CockpitProfiles.ThrottlePosition;
        if (!CockpitProfiles.Instance.SaveSlot(slot, CockpitProfiles.SeatOffset, stick, throttle)) return false;
        _profiles.Save(_key, _poses, slot);
        Finish(false);
        CockpitProfiles.Instance.SelectSlot(slot);
        ReloadProfile();
        return true;
    }
    internal bool LoadSlot(int slot)
    {
        // Loading never silently discards a live placement preview.
        if (_editing || !Available || CockpitProfiles.Instance?.SelectSlot(slot) != true) return false;
        ReloadProfile(); return true;
    }
    internal bool HasSavedSlot(int slot) => _profiles.HasSavedLayout(_key, slot);
    private void ReloadProfile()
    {
        _slot = CockpitProfiles.ActiveSlot; _poses = _profiles.Load(_key, _slot);
        _initialPlacementDone = false; ReleaseHands(); CaptureCalibration();
        MotionFlightController.Instance?.ResetGrips(); ControllerActions.Instance?.ResetButtons(); Place();
    }
    internal void ResetPreview()
    {
        if (!_editing) return;
        _poses = HudWidgetProfiles.FactoryDefaults;
        CockpitLayout.Instance?.BringWithinReach(); ReleaseHands(); Place();
    }
    internal HudWidgetPose GetPose(string id) => _poses.TryGetValue(id, out var pose) ? pose : HudWidgetProfiles.FactoryDefaults[id];
    internal void SetPose(string id, HudWidgetPose pose)
    {
        if (!_editing || !_poses.ContainsKey(id)) return;
        _poses[id] = HudWidgetProfiles.Sanitize(id, pose);  Place();
    }
    internal void Select(string id) { if (_widgets.ContainsKey(id)) SelectedId = id; }
    internal void FocusAppearance(string? id) => _menuAppearanceFocus = id != null && _poses.ContainsKey(id) ? id : null;
    internal void SetAngles(string id, Vector2 angles)
    {
        if (!_editing || !_poses.ContainsKey(id)) return;
        var pose = _poses[id]; pose.Euler = new Vector3(angles.x, angles.y, 0);
        ReleaseHands(); SetPose(id, pose);
    }

    internal void SetAppearance(string id, HudWidgetPose value)
    {
        if (!_poses.TryGetValue(id, out var pose)) return;
        pose.Scale = value.Scale; pose.Opacity = value.Opacity; pose.Background = value.Background; pose.Visible = value.Visible;
        if (_editing) SetPose(id, pose);
        else
        {
            _poses[id] = HudWidgetProfiles.Sanitize(id, pose);
            if (Available) _profiles.Save(_key, _poses, _slot);
            Place();
        }
    }
    internal void BringWithinReach()
    {
        if (!_editing) return;
        EnsureWidgetsWithinReach(true); CockpitLayout.Instance?.BringWithinReach(); ReleaseHands(); Place();
    }
    private bool EnsureWidgetsWithinReach(bool all)
    {
        if (!Space(out var gameRoot, out _, out var eye)) return false;
        var head = CockpitWidgetSpace.ToLocal(eye.position, eye.rotation,
            gameRoot.TransformPoint(NOVRHeadsetData.Translation + CockpitProfiles.SeatOffset));
        var leftShoulder = head + new Vector3(-.16f, -.18f, 0);
        var rightShoulder = head + new Vector3(.16f, -.18f, 0);
        var defaults = HudWidgetProfiles.FactoryDefaults;
        foreach (var id in HudWidgetProfiles.Ids)
        {
            var pose = _poses[id]; var half = Size(id) * (PixelSize * pose.Scale * .5f);
            var orientation = Quaternion.Euler(pose.Euler);
            var reachable = pose.Position.z > head.z + .1f;
            for (var corner = 0; corner < 4; corner++)
            {
                var point = pose.Position + orientation * new Vector3((corner & 1) == 0 ? -half.x : half.x, (corner & 2) == 0 ? -half.y : half.y, 0);
                reachable &= Mathf.Min(Vector3.Distance(point, leftShoulder), Vector3.Distance(point, rightShoulder)) <= .60f;
            }
            if (!all && reachable) continue;
            pose.Position = head + defaults[id].Position;
            pose.Euler = defaults[id].Euler;
            pose.Scale = Mathf.Min(pose.Scale, defaults[id].Scale);
            _poses[id] = pose;
        }
        return true;
    }

    private void CaptureCalibration()
    {
        _seat = CockpitProfiles.SeatOffset; _anchor = NOVRHeadsetData.TranslationAnchor;
        _calibration = NOVRHeadsetData.TranslationCalibrationOffset; _recenter = NOVRHeadsetData.RotationCalibrationOffset;
    }
    private bool HandPosition(ControllerSample hand, int index, out Vector3 position)
    {
        position = default;
        if (!hand.Valid || !Space(out var gameRoot, out _, out var eye)) { _angleValid[index] = false; return false; }
        // The same raw tracked index fingertip drives hover, acquisition and
        // movement. A visually snapped corner pinch never feeds back into it.
        position = CockpitWidgetSpace.ToLocal(eye.position, eye.rotation,
            PilotHandVisuals.TouchPoint(index == 0, hand, gameRoot, true));
        var rotation = Quaternion.Inverse(eye.rotation) * gameRoot.rotation * hand.Rotation;
        var forward = rotation * Vector3.forward;
        var previous = _handAngles[index];
        var angles = HudWidgetAngleMath.Direction(forward.x, forward.y, forward.z, previous.x, previous.y, _angleValid[index]);
        _handAngles[index] = new Vector2(angles.pitch, angles.yaw);
        _angleValid[index] = true;
        return TrackedControllers.Finite(position);
    }
    private void UpdateHand(int index, ControllerSample hand)
    {
        var drag = _hands[index];
        if (_appearanceBusy[index]) { drag.Gate.Reset(); _hover[index] = null; return; }
        var valid = HandPosition(hand, index, out var point);
        _points[index] = point;
        drag.Gate.Update(valid, hand.Grip);
        var hover = valid && CockpitLayout.HeldByHand(index == 0) == CockpitLayout.Element.None
            ? SelectAt(point, index, out _) : null;
        _hover[index] = hover;
        if (!drag.Gate.Held && drag.Id != null)
        {
            if (drag.Gate.Released)
            {
                if (SnapEnabled && _hands[1 - index].Id != drag.Id) Snap(drag.Id);
                InteractionHaptics.Pulse(hand.Device, InteractionCue.Release);
            }
            drag.Id = null;
            drag.Corner = -1;
        }
        if (drag.Gate.Pressed)
        {
            if (CockpitLayout.HeldByHand(index == 0) != CockpitLayout.Element.None) return;
            var id = SelectAt(point, index, out var corner);
            if (id != null && _hands[1 - index].Id != id &&
                CockpitLayout.GrabDistance(index == 0, hand) < WidgetDistance(point, id)) return;
            if (id != null)
            {
                drag.Id = id; SelectedId = id;
                drag.Corner = corner;
                RebaseDrag(index);
                if (corner >= 0 && Space(out var gameRoot, out _, out var eye))
                    drag.CornerRotation = Quaternion.Inverse(eye.rotation * Quaternion.Euler(_poses[id].Euler)) *
                        PilotHandVisuals.FreeHandRotation(index == 0, hand, gameRoot);
                InteractionHaptics.Pulse(hand.Device, InteractionCue.Grab);
            }
        }
    }
    private void UpdateAppearance(int index, ControllerSample hand)
    {
        _appearanceBusy[index] = false;
        var available = hand.Valid && _hands[index].Id == null && CockpitLayout.HeldByHand(index == 0) == CockpitLayout.Element.None;
        var tip = Vector3.zero;
        if (available && Space(out var gameRoot, out _, out var eye))
            tip = CockpitWidgetSpace.ToLocal(eye.position, eye.rotation, PilotHandVisuals.TouchPoint(index == 0, hand, gameRoot, true));
        else available = false;
        Widget? selected = null; var nearest = float.PositiveInfinity;
        foreach (var pair in _widgets)
        {
            var editor = pair.Value.Appearance;
            if (!available) continue;
            if (editor.IsHandBusy(index)) { selected = pair.Value; break; }
            var pose = _poses[pair.Key]; var metres = PixelSize * pose.Scale;
            var local = Quaternion.Inverse(Quaternion.Euler(pose.Euler)) * (tip - pose.Position) / metres;
            var distance = Mathf.Abs(local.z) * metres;
            if (editor.HitTest(local, metres) && distance < nearest) { selected = pair.Value; nearest = distance; }
        }
        foreach (var pair in _widgets)
        {
            var pose = _poses[pair.Key]; var metres = PixelSize * pose.Scale;
            var local = Quaternion.Inverse(Quaternion.Euler(pose.Euler)) * (tip - pose.Position) / metres;
            _appearanceBusy[index] |= pair.Value.Appearance.UpdateHand(index, local, available && pair.Value == selected, metres, hand.Device);
        }
    }
    private void RebaseDrag(int index)
    {
        var drag = _hands[index]; if (drag.Id == null) return;
        var pose = _poses[drag.Id];
        drag.LocalAnchor = Quaternion.Inverse(Quaternion.Euler(pose.Euler)) * (_points[index] - pose.Position);
        drag.StartAngles = new Vector2(pose.Euler.x, pose.Euler.y);
        drag.GripAngles = _handAngles[index];
    }
    private float WidgetDistance(Vector3 point, string id)
    {
        var pose = _poses[id]; var local = Quaternion.Inverse(Quaternion.Euler(pose.Euler)) * (point - pose.Position);
        var half = Size(id) * (PixelSize * pose.Scale * .5f);
        return new Vector3(Mathf.Max(0, Mathf.Abs(local.x) - half.x), Mathf.Max(0, Mathf.Abs(local.y) - half.y), local.z).magnitude;
    }
    private void MoveHeldWidgets()
    {
        var both = _hands[0].Id != null && _hands[0].Id == _hands[1].Id;
        if (both)
        {
            var id = _hands[0].Id!; var pose = _poses[id];
            if (_resizing != id)
            {
                _resizing = id;
                _resizeGesture = new HudWidgetTwoHandGesture(pose, _points[0], _points[1], _handAngles[0], _handAngles[1]);
            }
            pose = _resizeGesture.Move(pose, _points[0], _points[1], _handAngles[0], _handAngles[1]);
            MovePose(id, pose); return;
        }
        if (_resizing != null)
        {
            for (var i = 0; i < 2; i++)
                if (_hands[i].Id == _resizing) RebaseDrag(i);
            _resizing = null;
        }
        for (var i = 0; i < 2; i++)
        {
            var drag = _hands[i]; if (drag.Id == null || !drag.Gate.Held) continue;
            var pose = _poses[drag.Id];
            var angles = HudWidgetAngleMath.Drag(drag.StartAngles.x, drag.StartAngles.y, drag.GripAngles.x, drag.GripAngles.y,
                _handAngles[i].x, _handAngles[i].y);
            pose.Euler = new Vector3(angles.pitch, angles.yaw, 0);
            pose.Position = _points[i] - Quaternion.Euler(pose.Euler) * drag.LocalAnchor;
            MovePose(drag.Id, pose);
        }
    }
    private void MovePose(string id, HudWidgetPose target)
    {
        var current = _poses[id];
        var blend = HudWidgetMotionMath.Blend(Time.unscaledDeltaTime);
        target.Position = Vector3.Lerp(current.Position, target.Position, blend);
        target.Euler = new Vector3(Mathf.Lerp(current.Euler.x, target.Euler.x, blend),
            HudWidgetMotionMath.Angle(current.Euler.y, target.Euler.y, blend), 0);
        target.Scale = Mathf.Lerp(current.Scale, target.Scale, blend);
        SetPose(id, target);
    }
    private string? SelectAt(Vector3 point, int hand, out int corner)
    {
        string? result = null; var closest = float.PositiveInfinity; corner = -1;
        // A second grip on the opposite corner belongs to the held widget even
        // when a neighboring panel overlaps that corner in a compact layout.
        var other = _hands[1 - hand];
        if (other.Id != null && _poses.TryGetValue(other.Id, out var held))
        {
            var local = Quaternion.Inverse(Quaternion.Euler(held.Euler)) * (point - held.Position);
            var half = Size(other.Id) * (PixelSize * held.Scale * .5f);
            var candidate = HudWidgetGestureMath.Corner(local.x, local.y, local.z, half.x, half.y);
            if (HudWidgetGestureMath.Opposite(other.Corner, candidate)) { corner = candidate; return other.Id; }
        }
        foreach (var pair in _poses)
        {
            var local = Quaternion.Inverse(Quaternion.Euler(pair.Value.Euler)) * (point - pair.Value.Position);
            var half = Size(pair.Key) * (PixelSize * pair.Value.Scale * .5f);
            var atCorner = HudWidgetGestureMath.Corner(local.x, local.y, local.z, half.x, half.y);
            if (_hands[1 - hand].Id == pair.Key && !HudWidgetGestureMath.Opposite(_hands[1 - hand].Corner, atCorner)) continue;
            if (atCorner < 0 && (Mathf.Abs(local.x) > half.x + .025f || Mathf.Abs(local.y) > half.y + .025f || Mathf.Abs(local.z) > .075f)) continue;
            var distance = WidgetDistance(point, pair.Key);
            if (distance < closest) { closest = distance; result = pair.Key; corner = atCorner; }
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
        var size = Size(id);
        for (var i = 0; i < 4; i++)
        {
            var corner = Rect("Resize corner", widget.Root, new Vector2(20, 20));
            corner.anchoredPosition = new Vector2((i & 1) == 0 ? -size.x * .5f : size.x * .5f, (i & 2) == 0 ? -size.y * .5f : size.y * .5f);
            widget.Corners[i] = corner.gameObject.AddComponent<Image>(); widget.Corners[i].raycastTarget = false;
            var edge = Rect("Edit outline", widget.Root, i < 2 ? new Vector2(size.x, 2.5f) : new Vector2(2.5f, size.y));
            edge.anchoredPosition = i < 2 ? new Vector2(0, (i == 0 ? -1 : 1) * size.y * .5f) : new Vector2((i == 2 ? -1 : 1) * size.x * .5f, 0);
            widget.Outline[i] = edge.gameObject.AddComponent<Image>(); widget.Outline[i].raycastTarget = false;
        }
        widget.Caption = Label(widget.Root, HudWidgetProfiles.Label(id), new Vector2(0, Size(id).y * .5f + 18), new Vector2(Size(id).x + 50, 32), 20);
        widget.Placeholder = Label(widget.Root, Preview(id), Vector2.zero, Size(id) - new Vector2(12, 12), 20);
        widget.Appearance = new CockpitWidgetAppearanceEditor(widget.Root, Size(id), value =>
        {
            if (!_editing) return;
            var pose = GetPose(id); pose.Background = value; Select(id); SetAppearance(id, pose);
        }, visible =>
        {
            if (!_editing) return;
            var pose = GetPose(id); pose.Visible = visible; Select(id); SetAppearance(id, pose);
        });
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
        "map" => "Tactical map\nTerrain and units",
        "damage" => "Aircraft damage\nParts and system status",
        "objectives" => "Instructions\nTaxi guidance and clearances",
        _ => "Kill feed\nMission notifications"
    };
    private static string Idle(string id) => id switch
    {
        "weapons" => "WEAPONS\nNo selection",
        "countermeasures" => "COUNTERMEASURES\nNo readout",
        "power" => "CAPACITOR · IDLE",
        "map" => "MAP\nWaiting for terrain",
        _ => ""
    };
    private void Place()
    {
        var hasSpace = Space(out var gameRoot, out var uiRoot, out var eye);
        var ready = hasSpace && Ready();
        string? focused = WristMenu.IsOpen ? _menuAppearanceFocus : null;
        if (focused == null)
            foreach (var pair in _widgets)
                if (_editing && pair.Value.Appearance.IsOpen) { focused = pair.Key; break; }
        foreach (var pair in _widgets)
        {
            var widget = pair.Value;
            if (widget.Root == null) continue;
            var pose = _poses[pair.Key];
            var show = ready && (pose.Visible || _editing);
            var emphasis = focused == null || focused == pair.Key ? 1f : .22f;
            widget.Appearance.SetState(_editing && show, pose.Visible, pose.Background, emphasis);
            widget.Root.gameObject.SetActive(show);
            if (!show || gameRoot == null || uiRoot == null || eye == null) continue;
            widget.Canvas.worldCamera = APIBus.CockpitHudCamera;
            widget.Root.SetPositionAndRotation(uiRoot.TransformPoint(gameRoot.InverseTransformPoint(
                    CockpitWidgetSpace.ToWorld(eye.position, eye.rotation, pose.Position))),
                uiRoot.rotation * Quaternion.Inverse(gameRoot.rotation) * eye.rotation * Quaternion.Euler(pose.Euler));
            widget.Root.localScale = Vector3.one * (PixelSize * pose.Scale);
            widget.Group.alpha = pose.Visible ? pose.Opacity * emphasis : 0;
            widget.Background.color = new Color(.015f, .025f, .03f, pose.Visible ? pose.Background * emphasis : 0);
            var held = _hands[0].Id == pair.Key || _hands[1].Id == pair.Key;
            var hovered = _hover[0] == pair.Key || _hover[1] == pair.Key;
            var color = held ? new Color(.3f, 1f, .8f, 1) : hovered ? new Color(.6f, 1f, 1f, .9f) : new Color(.65f, .85f, .9f, .55f);
            color.a *= emphasis;
            foreach (var edge in widget.Outline) { edge.enabled = _editing; edge.color = color; }
            foreach (var corner in widget.Corners) { corner.enabled = _editing; corner.color = color; }
            widget.Caption.enabled = _editing;
            widget.Caption.color = new Color(1, 1, 1, emphasis);
            if (_editing)
            {
                var caption = HudWidgetProfiles.Label(pair.Key) + (pose.Visible ? "" : " · HIDDEN");
                if (widget.Caption.text != caption) widget.Caption.text = caption;
            }
            var idle = Idle(pair.Key);
            widget.Placeholder.enabled = (!widget.HasContent || !pose.Visible) && (_editing || pose.Visible && idle.Length > 0);
            if (widget.Placeholder.enabled)
            {
                var text = _editing ? Preview(pair.Key) : idle;
                if (widget.Placeholder.text != text) widget.Placeholder.text = text;
                widget.Placeholder.color = _editing ? new Color(1, 1, 1, emphasis) : new Color(.56f, .9f, .68f, pose.Opacity * .65f * emphasis);
            }
        }
    }
    private void ReleaseHands()
    {
        foreach (var hand in _hands) { hand.Id = null; hand.Corner = -1; hand.Gate.Reset(); }
        _hover[0] = _hover[1] = null; _resizing = null;
        _angleValid[0] = _angleValid[1] = false;
        _appearanceBusy[0] = _appearanceBusy[1] = false;
        foreach (var widget in _widgets.Values) widget.Appearance.ReleaseHands();
    }
    private void ReleaseWidgets()
    {
        CockpitStatusSources.ReleaseAll();
        CockpitMapWidget.ReleaseAll();
        CockpitAlerts.ReleaseWidgetParents();
        foreach (var widget in _widgets.Values)
        {
            widget.Appearance.Dispose();
            if (widget.Root != null) Destroy(widget.Root.gameObject);
        }
        _widgets.Clear();
        _menuAppearanceFocus = null;
    }
    private void OnDisable()
    {
        Application.onBeforeRender -= Place; Finish(false); ReleaseWidgets(); _aircraft = null;
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
