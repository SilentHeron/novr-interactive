using UnityEngine;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;

namespace NOVR.MotionControls;

// Layout edits remain previews until explicitly saved. Tracking/menu transitions
// release the grab, and never turn an already held grip into a new selection.
[DefaultExecutionOrder(-1250)]
internal sealed class CockpitLayout : MonoBehaviour
{
    internal enum Element
    {
        None, Stick, Throttle
    }
    private sealed class HandDrag
    {
        internal readonly ControllerButtonGate Gate = new();
        internal Element Element, Hover;
        internal Vector3 StartHand, StartPosition;
    }
    internal static CockpitLayout? Instance
    {
        get; private set;
    }
    internal static bool Active => Instance?._active == true;
    private bool _active, _leftHanded, _unified;
    private Element _onlyElement;
    internal static bool Unified => Active && Instance!._unified;
    internal static bool PhysicalHandleEdit => Active && Instance!._onlyElement != Element.None;
    internal static bool Unlocked(Element element) => Active && (Instance!._onlyElement == Element.None || Instance._onlyElement == element);
    internal void ToggleHandle(Element element)
    {
        if (element != Element.Stick && element != Element.Throttle)
            return;
        var close = Unlocked(element);
        if (_active)
            Finish(true);
        if (close)
            return;
        CockpitHudWidgets.Instance?.Finish(false);
        BeginCore(false, element, true);
    }
    private Aircraft? _aircraft;
    private Vector3 _stick, _throttle, _calibration, _anchor, _seatOffset;
    private Quaternion _recenterRotation;
    private readonly HandDrag[] _hands = { new(), new() };
    private readonly LineRenderer?[] _rays = new LineRenderer?[2];
    private Material? _rayMaterial;
    internal static Vector3? StickPreview => Active ? Instance!._stick : null;
    internal static Vector3? ThrottlePreview => Active ? Instance!._throttle : null;
    internal static string Hint => Unified
        ? "EDIT LAYOUT  •  GRIP HANDLES OR HUD WIDGETS TO MOVE  •  VIEW → SAVE LAYOUT"
        : "EDIT HANDLES  •  GRIP STICK OR THROTTLE TO MOVE  •  VIEW → SAVE LAYOUT";
    internal static bool Held(Element element) => Active && (Instance!._hands[0].Element == element || Instance._hands[1].Element == element);
    internal static bool Hovered(Element element) => Active && (Instance!._hands[0].Hover == element || Instance._hands[1].Hover == element);
    internal static Element HeldByHand(bool left) => Active ? Instance!._hands[left ? 0 : 1].Element : Element.None;
    private Vector3 Mirror(Vector3 p) => _leftHanded ? new Vector3(-p.x, p.y, p.z) : p;
    private void Awake() => Instance = this;

    internal void Begin()
    {
        if (CockpitHudWidgets.Instance != null)
        {
            CockpitHudWidgets.Instance.Begin();
            return;
        }
        BeginCore(false, Element.None, true);
    }

    // The HUD manager owns the combined transaction. These entry points never
    // call back into it, so aircraft changes and Save/Cancel cannot recurse.
    internal bool BeginUnified()
    {
        if (_active && _unified)
            return true;
        if (_active)
            FinishCore(false);
        BeginCore(true, Element.None, false);
        return _active && _unified;
    }
    internal bool CanSaveUnified => _active && _unified && SameContext;
    internal void FinishUnified(bool save)
    {
        if (_unified)
            FinishCore(save);
    }
    private bool SameContext => GameManager.GetLocalAircraft(out var aircraft) && aircraft == _aircraft &&
        _leftHanded == ModConfiguration.Instance.MotionControls.LeftHanded.Value;

    private void BeginCore(bool unified, Element onlyElement, bool closeMenu)
    {
        if (_active || !GameManager.GetLocalAircraft(out var aircraft) || CameraStateManager.cameraMode != CameraMode.cockpit)
            return;
        CockpitProfiles.Instance?.Refresh();
        _stick = CockpitProfiles.StickPosition;
        _throttle = CockpitProfiles.ThrottlePosition;
        _aircraft = aircraft;
        _leftHanded = ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        _calibration = NOVRHeadsetData.TranslationCalibrationOffset;
        _anchor = NOVRHeadsetData.TranslationAnchor;
        _seatOffset = CockpitProfiles.SeatOffset;
        _recenterRotation = NOVRHeadsetData.RotationCalibrationOffset;
        ModConfiguration.Instance.MotionControls.Enabled.Value = true;
        MotionFlightController.Instance?.CancelPlacement();
        ControllerActions.Instance?.ResetButtons();
        _onlyElement = onlyElement;
        _unified = unified;
        _active = true;
        Release();
        if (closeMenu)
            WristMenu.Close();
    }

    internal void Finish(bool save)
    {
        if (_unified && CockpitHudWidgets.Editing)
        {
            CockpitHudWidgets.Instance!.Finish(save);
            return;
        }
        FinishCore(save);
    }
    private void FinishCore(bool save)
    {
        if (!_active)
            return;
        var sameAircraft = SameContext;
        _active = _unified = false;
        _onlyElement = Element.None;
        Release();
        if (save && sameAircraft)
        {
            CockpitProfiles.Instance?.SaveHandles(_stick, _throttle);
            ModConfiguration.Instance.Config.Save();
        }
        MotionFlightController.Instance?.ResetGrips();
        ControllerActions.Instance?.ResetButtons();
    }

    internal void BringWithinReach()
    {
        if (!_active || !SameContext)
            return;
        var seat = CockpitProfiles.SeatOffset;
        var stick = seat + Mirror(new Vector3(.22f, -.35f, .30f));
        var throttle = seat + Mirror(new Vector3(-.22f,
            MotionFlightController.UsesLiftCollective(_aircraft) ? -.55f : -.40f, .30f));
        _stick = Mirror(BoundHandle(stick));
        var offset = GripPosition(Element.Throttle) - Mirror(_throttle);
        _throttle = Mirror(BoundHandle(throttle - offset));
        Release();
    }

    // Compare direct contact distances before choosing a handle or widget.
    // A handle already owned by the opposite hand is never a candidate.
    internal static float GrabDistance(bool left, ControllerSample hand)
    {
        var self = Instance;
        if (self?._active != true || !self.SameContext || !hand.Valid || !TrackedControllers.Finite(hand.Position))
            return float.PositiveInfinity;
        return self.SelectNearby(hand.Position, left ? 0 : 1, out var distance) != Element.None
            ? distance : float.PositiveInfinity;
    }
    internal static bool CanGrabHand(bool left, ControllerSample hand) => !float.IsPositiveInfinity(GrabDistance(left, hand));

    private void Update()
    {
        if (!_active)
            return;
        if (!GameManager.GetLocalAircraft(out var aircraft) || aircraft != _aircraft || aircraft.cockpit == null ||
            aircraft.cockpit.IsDetached() || CameraStateManager.cameraMode != CameraMode.cockpit ||
            _leftHanded != ModConfiguration.Instance.MotionControls.LeftHanded.Value)
        {
            Finish(false);
            return;
        }
        if (_seatOffset != CockpitProfiles.SeatOffset || _calibration != NOVRHeadsetData.TranslationCalibrationOffset || _anchor != NOVRHeadsetData.TranslationAnchor ||
            _recenterRotation != NOVRHeadsetData.RotationCalibrationOffset)
        {
            // A seat step changes mapped controller coordinates, not the user's
            // physical drag. Keep the preview fixed and require a fresh grip.
            Release();
            _calibration = NOVRHeadsetData.TranslationCalibrationOffset;
            _anchor = NOVRHeadsetData.TranslationAnchor;
            _recenterRotation = NOVRHeadsetData.RotationCalibrationOffset;
            _seatOffset = CockpitProfiles.SeatOffset;
        }
        if (!TrackedControllers.Available || WristMenu.IsOpen || Time.timeScale <= 0 || DynamicMap.mapMaximized ||
            !GameManager.flightControlsEnabled || CursorManager.GetFlag(CursorFlags.GameMenu) ||
            (SceneSingleton<RadialMenuMain>.i != null && RadialMenuMain.IsInUse()) ||
            (GameManager.controlMapper != null && GameManager.controlMapper.isOpen))
        {
            Release();
            return;
        }
        for (var i = 0; i < 2; i++)
            MoveHand(i, TrackedControllers.Read(i == 0));
    }

    private Vector3 GripPosition(Element element)
    {
        if (element == Element.Stick)
            return Mirror(_stick);
        var offset = FlightControlMath.ThrottleOffset(MotionFlightController.UsesLiftCollective(_aircraft),
            MotionFlightController.Instance?.Throttle ?? .5f, ModConfiguration.Instance.MotionControls.ThrottleTravel.Value);
        return Mirror(_throttle) + (MotionFlightController.Instance?.ThrottleOffset ?? new Vector3(0, offset.height, offset.forward));
    }

    private void MoveHand(int index, ControllerSample hand)
    {
        var drag = _hands[index];
        if (_unified && CockpitHudWidgets.IsHandBusy(index == 0))
        {
            drag.Gate.Reset();
            drag.Element = drag.Hover = Element.None;
            if (_rays[index] != null)
                _rays[index]!.enabled = false;
            return;
        }
        drag.Gate.Update(hand.Valid, hand.Grip);
        if (!drag.Gate.Held)
        {
            if (drag.Element != Element.None && hand.Valid)
                InteractionHaptics.Pulse(hand.Device, .12f, .02f, InteractionCue.Release);
            drag.Element = Element.None;
        }
        var ray = new Ray(hand.AimPosition, hand.AimRotation * Vector3.forward);
        drag.Hover = hand.Valid ? Select(hand.Position, ray, index, out _) : Element.None;
        if (drag.Gate.Pressed && drag.Hover != Element.None)
        {
            drag.Element = drag.Hover;
            drag.StartHand = hand.Position;
            drag.StartPosition = GripPosition(drag.Element);
            Pulse(hand);
        }
        if (drag.Element != Element.None && hand.Valid && drag.Gate.Held)
        {
            var delta = hand.Position - drag.StartHand;
            var grip = drag.StartPosition + delta;
            if (drag.Element == Element.Stick)
                _stick = Mirror(BoundHandle(grip));
            else
                _throttle = Mirror(BoundHandle(grip - (GripPosition(Element.Throttle) - Mirror(_throttle))));
        }
        DrawRay(index, hand, drag.Element != Element.None ? drag.Element : drag.Hover);
    }

    private Element Select(Vector3 hand, Ray ray, int index, out float distance)
    {
        var best = SelectNearby(hand, index, out distance);
        if (best != Element.None || _unified)
            return best;
        foreach (var element in new[] { Element.Stick, Element.Throttle })
        {
            if (_hands[1 - index].Element == element)
                continue;
            if (_onlyElement != Element.None && element != _onlyElement)
                continue;
            var center = GripPosition(element);
            var along = Vector3.Dot(center - ray.origin, ray.direction);
            if (Vector3.Distance(ray.GetPoint(along), center) > .09f)
                continue;
            if (along > .01f && along < distance)
            {
                best = element;
                distance = along;
            }
        }
        return best;
    }

    private Element SelectNearby(Vector3 hand, int index, out float distance)
    {
        var best = Element.None;
        distance = 2.5f;
        // Reach takes precedence over pointing. Either hand may move any item.
        foreach (var element in new[] { Element.Stick, Element.Throttle })
        {
            if (_onlyElement != Element.None && element != _onlyElement)
                continue;
            if (_hands[1 - index].Element == element)
                continue;
            var d = Vector3.Distance(hand, GripPosition(element));
            if (d < ModConfiguration.Instance.MotionControls.GrabRadius.Value && d < distance)
            {
                best = element;
                distance = d;
            }
        }
        return best;
    }

    private static Vector3 BoundHandle(Vector3 p) => new(Mathf.Clamp(p.x, -1.2f, 1.2f), Mathf.Clamp(p.y, -1.25f, .7f), Mathf.Clamp(p.z, -.35f, 1.5f));
    private void DrawRay(int index, ControllerSample hand, Element element)
    {
        if (_unified || !hand.Valid || element == Element.None)
        {
            if (_rays[index] != null)
                _rays[index]!.enabled = false;
            return;
        }
        var root = APIBus.CockpitHudCamera?.transform.parent;
        if (root == null)
            return;
        if (_rays[index] == null)
        {
            _rayMaterial ??= CockpitMaterials.Opaque("NOVR layout pointer", new Color(.3f, 1f, .8f));
            var go = new GameObject("Layout pointer", typeof(LineRenderer));
            go.transform.SetParent(transform, false);
            var line = _rays[index] = go.GetComponent<LineRenderer>();
            line.sharedMaterial = _rayMaterial;
            line.positionCount = 2;
            line.startWidth = .0015f;
            line.endWidth = .001f;
            go.layer = (int)LayerHelper.GetVrUiLayer();
        }
        _rays[index]!.enabled = true;
        _rays[index]!.SetPosition(0, root.TransformPoint(hand.AimPosition));
        _rays[index]!.SetPosition(1, root.TransformPoint(GripPosition(element)));
    }
    private void Release()
    {
        foreach (var hand in _hands)
        {
            hand.Gate.Reset();
            hand.Element = hand.Hover = Element.None;
        }
        foreach (var ray in _rays)
            if (ray != null)
                ray.enabled = false;
    }
    private static void Pulse(ControllerSample hand)
    {
        InteractionHaptics.Pulse(hand.Device, cue: InteractionCue.Grab);
    }
    private void OnDisable() => Finish(false);
    private void OnDestroy()
    {
        Finish(false);
        foreach (var ray in _rays)
            if (ray != null)
                Destroy(ray.gameObject);
        if (_rayMaterial != null)
            Destroy(_rayMaterial);
        if (Instance == this)
            Instance = null;
    }
}
