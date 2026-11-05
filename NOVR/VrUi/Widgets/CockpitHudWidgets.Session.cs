using System.Collections.Generic;
using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR.VrUi.SpecialBehavior;

// This component owns one combined HUD/seat/handle preview. Context changes
// cancel it before borrowed UI is released; calibration changes release only
// the active contacts. Rendering and gesture geometry live in the other partial.
internal sealed partial class CockpitHudWidgets
{
    private Dictionary<string, HudWidgetPose> _poses = new();
    private Dictionary<string, HudWidgetPose>? _editStart;
    private HudWidgetProfiles _profiles = null!;
    private Aircraft? _aircraft;
    private string _key = "";
    private int _slot = 1;
    internal int ProfileSlot => _slot;
    internal string ProfileAircraftName => CanSaveProfile ? CockpitProfiles.Instance?.AircraftName ?? AircraftName : "Aircraft loading";
    internal bool CanSaveProfile => _key.Length > 0 && CockpitProfiles.Instance?.AircraftIdentity == _key;
    private bool _editing, _initialPlacementDone, _pendingAuxiliaryPlacement;
    private readonly HudWidgetDefaultPlacement _defaultPlacement = new();
    private Vector3 _seat, _anchor, _calibration;
    private Quaternion _recenter;
    internal static bool Editing => Instance?._editing == true;
    internal static Aircraft? CurrentAircraft => Instance?._aircraft;
    internal string AircraftName => _aircraft?.definition?.unitName ?? "No aircraft";
    internal bool Available => _aircraft != null && _initialPlacementDone && !_pendingAuxiliaryPlacement && Ready();
    private bool PlacementReady(string id) => _initialPlacementDone && (id != "auxiliary" || !_pendingAuxiliaryPlacement);

    private void RefreshContext()
    {
        GameManager.GetLocalAircraft(out var aircraft);
        if (!XRSettings.enabled || CockpitAlerts.LostCockpit(aircraft))
            aircraft = null;
        var key = aircraft != null && aircraft.definition != null
            ? AircraftLayoutIdentity.Resolve(aircraft.definition.jsonKey, aircraft.definition.name) : "";
        if (aircraft != _aircraft || _slot != CockpitProfiles.ActiveSlot || _key != key ||
            aircraft != null && _widgets.Count != HudWidgetProfiles.Ids.Length)
        {
            Finish(false);
            ReleaseWidgets();
            _aircraft = aircraft;
            _slot = CockpitProfiles.ActiveSlot;
            _key = key;
            _initialPlacementDone = false;
            _pendingAuxiliaryPlacement = false;
            _defaultPlacement.Reset();
            if (aircraft != null)
            {
                // Every cockpit gets every widget, regardless of native HUD
                // sources or mod metadata timing. Missing sources stay editable.
                _poses = _profiles.Load(_key, _slot);
                _initialPlacementDone = _profiles.HasSavedLayout(_key, _slot);
                _pendingAuxiliaryPlacement = _profiles.NeedsAuxiliaryPlacement(_key, _slot);
                CaptureCalibration();
                foreach (var id in HudWidgetProfiles.Ids)
                    CreateWidget(id);
            }
        }
    }

    private bool CanUpdateEdit()
    {
        if (!_editing)
            return false;
        if (!Ready() || CameraStateManager.cameraMode != CameraMode.cockpit)
        {
            Finish(false);
            return false;
        }
        if (_seat != CockpitProfiles.SeatOffset || _anchor != NOVRHeadsetData.TranslationAnchor ||
            _calibration != NOVRHeadsetData.TranslationCalibrationOffset || _recenter != NOVRHeadsetData.RotationCalibrationOffset)
        {
            ReleaseHands();
            CaptureCalibration();
        }
        if (!TrackedControllers.Available || WristMenu.IsOpen || Time.timeScale <= 0 || !GameManager.flightControlsEnabled ||
            (SceneSingleton<RadialMenuMain>.i != null && RadialMenuMain.IsInUse()) ||
            (GameManager.controlMapper != null && GameManager.controlMapper.isOpen))
        {
            ReleaseHands();
            return false;
        }
        return true;
    }

    internal void Begin()
    {
        if (_editing || !Available)
            return;
        CockpitLayout.Instance?.Finish(false);
        MotionFlightController.Instance?.CancelPlacement();
        MotionFlightController.Instance?.ResetGrips();
        ControllerActions.Instance?.ResetButtons();

        _editing = true;
        if (CockpitLayout.Instance?.BeginUnified() != true)
        {
            _editing = false;
            return;
        }
        CockpitProfiles.Instance?.BeginPreview();
        // Begin at the exact displayed/saved layout. Reach recovery is an
        // explicit command, never a side effect of opening the editor.
        _editStart = new Dictionary<string, HudWidgetPose>(_poses);
        CaptureCalibration();
        ReleaseHands();
        Place();
    }
    internal void Finish(bool save)
    {
        if (!_editing)
            return;
        if (save)
        {
            SaveToSlot(_slot);
            return;
        }
        _editing = false;
        ReleaseHands();
        CockpitLayout.Instance?.FinishUnified(false);
        CockpitProfiles.Instance?.EndPreview();
        if (_editStart != null)
            _poses = _editStart;
        _editStart = null;
        MotionFlightController.Instance?.ResetGrips();
        ControllerActions.Instance?.ResetButtons();
        Place();
    }
    internal bool SaveToSlot(int slot)
    {
        CockpitProfiles.Instance?.Refresh();
        // A slot change after Update invalidates this preview, even if the
        // requested save destination happens to be the newly selected slot.
        if (slot < 1 || slot > 3 || !_editing || !Available || !CanSaveProfile || CockpitLayout.Instance?.CanSaveUnified != true ||
            !GameManager.GetLocalAircraft(out var aircraft) || aircraft != _aircraft || CockpitProfiles.Instance == null ||
            _slot != CockpitProfiles.ActiveSlot)
            return false;
        var stick = CockpitLayout.StickPreview ?? CockpitProfiles.StickPosition;
        var throttle = CockpitLayout.ThrottlePreview ?? CockpitProfiles.ThrottlePosition;
        if (!CockpitProfiles.Instance.SaveSlot(slot, CockpitProfiles.SeatOffset, stick, throttle))
            return false;
        _profiles.Save(_key, _poses, slot);
        Finish(false);
        CockpitProfiles.Instance.SelectSlot(slot);
        ReloadProfile();
        return true;
    }
    internal bool LoadSlot(int slot)
    {
        // Loading never silently discards a live placement preview.
        if (_editing || !Available || !CanSaveProfile || CockpitProfiles.Instance?.SelectSlot(slot) != true)
            return false;
        ReloadProfile();
        return true;
    }
    internal bool HasSavedSlot(int slot) => _profiles.HasSavedLayout(_key, slot);
    private void ReloadProfile()
    {
        _slot = CockpitProfiles.ActiveSlot;
        _poses = _profiles.Load(_key, _slot);
        _initialPlacementDone = _profiles.HasSavedLayout(_key, _slot);
        _pendingAuxiliaryPlacement = _profiles.NeedsAuxiliaryPlacement(_key, _slot);
        _defaultPlacement.Reset();
        ReleaseHands();
        CaptureCalibration();
        MotionFlightController.Instance?.ResetGrips();
        ControllerActions.Instance?.ResetButtons();
        Place();
    }
    private void InitializePlacement(bool valid, Vector3 head, float now, int frame)
    {
        if (_initialPlacementDone && !_pendingAuxiliaryPlacement)
            return;
        // Calibration can settle after the aircraft camera is attached. A new
        // widget follows the same tracking gate as an entirely new layout.
        if (_seat != CockpitProfiles.SeatOffset || _anchor != NOVRHeadsetData.TranslationAnchor ||
            _calibration != NOVRHeadsetData.TranslationCalibrationOffset || _recenter != NOVRHeadsetData.RotationCalibrationOffset)
        {
            _defaultPlacement.Reset();
            CaptureCalibration();
        }
        if (!_defaultPlacement.Observe(valid, head, now, frame))
            return;
        var defaults = HudWidgetDefaultPlacement.AtHead(head);
        if (_pendingAuxiliaryPlacement)
        {
            _profiles.InitializeAuxiliary(_key, defaults["auxiliary"], _slot);
            _poses["auxiliary"] = _profiles.Load(_key, _slot)["auxiliary"];
            _pendingAuxiliaryPlacement = false;
        }
        else
            _poses = defaults;
        _initialPlacementDone = true;
    }
    private void CaptureCalibration()
    {
        _seat = CockpitProfiles.SeatOffset;
        _anchor = NOVRHeadsetData.TranslationAnchor;
        _calibration = NOVRHeadsetData.TranslationCalibrationOffset;
        _recenter = NOVRHeadsetData.RotationCalibrationOffset;
    }
    private void ReleaseHands()
    {
        foreach (var hand in _hands)
        {
            hand.Id = null;
            hand.Corner = -1;
            hand.Gate.Reset();
        }
        _hover[0] = _hover[1] = null;
        _resizing = null;
        _handRotations[0] = _handRotations[1] = Quaternion.identity;
        _appearanceBusy[0] = _appearanceBusy[1] = false;
        foreach (var widget in _widgets.Values)
            widget.Appearance.ReleaseHands();
    }
    private void ReleaseWidgets()
    {
        CockpitStatusSources.ReleaseAll();
        CockpitMapWidget.ReleaseAll();
        CockpitAlerts.ReleaseWidgetParents();
        foreach (var widget in _widgets.Values)
        {
            widget.Appearance.Dispose();
            if (widget.Root != null)
                Destroy(widget.Root.gameObject);
        }
        _widgets.Clear();
        _menuAppearanceFocus = null;
    }
    private void OnDisable()
    {
        Application.onBeforeRender -= Place;
        Finish(false);
        ReleaseWidgets();
        _aircraft = null;
    }
}
