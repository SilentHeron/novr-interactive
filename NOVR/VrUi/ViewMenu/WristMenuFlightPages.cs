using System;
using System.Collections.Generic;
using NOVR.MotionControls;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

internal sealed partial class WristMenu
{
    private void BuildResetPage()
    {
        _resetPage = Page("RESET SETUP");
        var parent = _resetPage.transform;
        Label(_resetPage.transform, "Restore the default seat, stick and throttle\nfor this aircraft and handedness?\n\nYour saved handle positions will be replaced.\nOriginal cockpit instruments stay in place.", new Vector2(0, 45), new Vector2(510, 180), 21);
        AddButton(parent, "Keep my setup", 0, 4, Back);
        ActionButton(parent, "Reset seat & handles", 1, -51, ConfirmReset, true);
    }

    private void BuildFlightActionsPage()
    {
        _flightPage = Page("FLIGHT ACTIONS");
        var parent = _flightPage.transform;
        Section(parent, "Aircraft commands", 214);
        ActionButton(parent, "Next weapon", 0, 168, () => ControllerActions.Instance?.QueueWristAction(NativeControllerAction.NextWeapon));
        ActionButton(parent, "Next countermeasure", 1, 168, () => ControllerActions.Instance?.QueueWristAction(NativeControllerAction.NextCountermeasure));
        ActionButton(parent, "Unlock last target", 0, 120, () => { SceneSingleton<CombatHUD>.i?.DeselectLast(); Close(); });
        ActionButton(parent, "Clear targets", 1, 120, () => { SceneSingleton<CombatHUD>.i?.DeselectAll(); Close(); });
        ActionButton(parent, "Toggle gear", 0, 72, () => ControllerActions.Instance?.QueueWristAction(NativeControllerAction.Gear));
        ActionButton(parent, "Toggle radar", 1, 72, () => ControllerActions.Instance?.QueueWristAction(NativeControllerAction.Radar));
        ActionButton(parent, "Toggle engine", 0, 24, () => ControllerActions.Instance?.QueueWristAction(NativeControllerAction.ToggleEngine));
        ActionButton(parent, "Tactical map", 1, 24, ControllerActions.ToggleMap);
        var assistLabel = ActionButton(parent, "Flight assist", 0, -24, () => ControllerActions.Instance?.QueueWristAction(NativeControllerAction.FlightAssist));
        _assistButton = assistLabel.transform.parent.GetComponent<Button>();
        _liveLabels.Add((assistLabel, () => GameManager.GetLocalAircraft(out var a) && a.GetControlsFilter()?.HasFlightAssist() == true ? "Toggle flight assist" : "Assist: not switchable"));
        var hoverLabel = ActionButton(parent, "Hover assist", 1, -24, () => ControllerActions.Instance?.QueueWristAction(NativeControllerAction.AutoHover));
        _hoverButton = hoverLabel.transform.parent.GetComponent<Button>();
        _liveLabels.Add((hoverLabel, () => !GameManager.GetLocalAircraft(out var a) || a.GetControlsFilter()?.HasAutoHover() != true
            ? "Hover: not fitted" : a.radarAlt < 1f ? "Hover: ground locked" : $"Hover assist: {(a.IsAutoHoverEnabled() ? "ON" : "OFF")}"));
        Section(parent, "Emergency", -158);
        ActionButton(parent, "Eject…", 0, -213, AskToEject, true);
    }

    private void BuildEjectPage()
    {
        _ejectPage = Page("EJECT");
        var parent = _ejectPage.transform;
        Label(_ejectPage.transform, "Eject from this aircraft?\nThis starts the game's ejection sequence.", new Vector2(0, 65), new Vector2(510, 120), 24);
        AddButton(parent, "Cancel", 0, 4, Back);
        ActionButton(parent, "Confirm eject", 1, -51, ConfirmEject, true);
    }

    private void BuildFlightControlsPage()
    {
        _controlsPage = Page("FLIGHT CONTROLS");
        var parent = _controlsPage.transform;
        var motion = ModConfiguration.Instance.MotionControls;
        Section(parent, "Grip & steering", 214);
        Setting(parent, "Motion controls", motion.Enabled, 0, 170);
        Setting(parent, "Twist for yaw", motion.TwistYaw, 1, 170, () => MotionFlightController.Instance?.ResetGrips());
        Setting(parent, "Toggle stick grip", motion.StickTapGrip, 0, 123, () => MotionFlightController.Instance?.ResetGrips());
        Setting(parent, "Thumbstick for yaw", motion.ThumbstickYaw, 1, 123);
        Section(parent, "Stick response", 77);
        Setting(parent, "Stick sensitivity", 23, 0, 1,
            () => Mathf.InverseLerp(MotionControlSettings.MaximumStickAngle, MotionControlSettings.MinimumStickAngle, motion.StickAngle.Value),
            value => motion.StickAngle.Value = Mathf.Round(Mathf.Lerp(MotionControlSettings.MaximumStickAngle, MotionControlSettings.MinimumStickAngle, value) * 2) * .5f,
            _ => $"{MotionControlSettings.DefaultStickAngle / motion.StickAngle.Value:0.00}× / {motion.StickAngle.Value:0.#}°");
        Setting(parent, "Pitch / roll deadzone", motion.Deadzone, -45, Percent, .01f);
        Setting(parent, "Twist yaw deadzone", motion.YawDeadzone, -113, Percent, .01f);
        ActionButton(parent, "Throttle / collective  ›", 0, -175, () => ShowPage(_throttlePage!));
        ActionButton(parent, "Controller guide  ›", 1, -175, () => ShowPage(_helpPage!));
        ActionButton(parent, "Reset response", 0, -223, () => { motion.StickAngle.Value = 25; motion.YawAngle.Value = 30; motion.Deadzone.Value = .06f; motion.YawDeadzone.Value = MotionControlSettings.DefaultYawDeadzone; motion.ResponseExponent.Value = 1.5f; motion.ThrottleTravel.Value = .12f; });
        ViewMenuControls.Text(_controlsPage.transform, _font!, "Pedals: turn both yaw options off.", new Vector2(160, -223), new Vector2(300, 42), 16);
    }

    private void BuildThrottlePage()
    {
        _throttlePage = Page("THROTTLE / COLLECTIVE");
        var parent = _throttlePage.transform;
        var motion = ModConfiguration.Instance.MotionControls;
        Section(parent, "Handle behavior", 214);
        Setting(parent, "Toggle throttle grip", motion.ThrottleTapGrip, 0, 168, () => MotionFlightController.Instance?.ResetGrips());
        Setting(parent, "MIL power detent", motion.MilDetent, 1, 168);
        Setting(parent, "Throttle travel / collective lift", motion.ThrottleTravel, 94, Centimeters, .005f);
        Note(parent, "Move forward to increase throttle; lift up for collective.\nMIL detent applies to afterburning aircraft.", 19, 60);
        Section(parent, "Placement", -47);
        Note(parent, "Use Edit layout to reposition the handle.\nGrip modes use one squeeze to hold, another to release.\nWith toggle grip off, keep squeezing to hold.", -113, 95);
        ActionButton(parent, "Edit layout", 0, -202, StartOrResumeLayout);
    }
}
