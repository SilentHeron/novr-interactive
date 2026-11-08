using System;
using System.Collections.Generic;
using NOVR.MotionControls;
using NOVR.VrUi.Native;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

internal sealed partial class WristMenu
{
    private void BuildHandsPage()
    {
        _handsPage = Page("HANDS & ARMS");
        var parent = _handsPage.transform;
        var motion = ModConfiguration.Instance.MotionControls;
        Section(parent, "Pilot appearance", 214);
        Setting(parent, "Show gloves", motion.RiggedGloves, 0, 168);
        Setting(parent, "Show sleeves", motion.PilotArms, 1, 168);
        Setting(parent, "Sleeve width", motion.SleeveThickness, 94, Percent, .01f);
        Section(parent, "Hand fit & helmet gesture", 22);
        ActionButton(parent, "Fit left hand  ›", 0, -29, () => { _fittingLeftHand = true; ShowPage(_handPositionPage!); });
        ActionButton(parent, "Fit right hand  ›", 1, -29, () => { _fittingLeftHand = false; ShowPage(_handPositionPage!); });
        Setting(parent, "Brow night vision", motion.HelmetNightVision, 0, -80);
        var browHint = ViewMenuControls.Text(_handsPage.transform, _font!, "Raise a free hand to your brow,\nthen press its trigger.", new Vector2(160, -80), new Vector2(300, 44), 17);
        browHint.color = ViewMenuControls.Muted;
        Note(parent, "Free hands follow your controllers.\nHeld hands wrap around and follow the handle.\nFit corrections leave held grip poses unchanged.\nSleeves soften near their reach limit.", -172, 112);
    }

    private void BuildHandPositionPage()
    {
        _handPositionPage = Page("HAND POSITION");
        var parent = _handPositionPage.transform;
        var motion = ModConfiguration.Instance.MotionControls;
        Section(parent, "Free-hand position correction", 214);
        HandPositionSetting(parent, "Left / right", 0, 159);
        HandPositionSetting(parent, "Down / up", 1, 89);
        HandPositionSetting(parent, "Nearer / farther", 2, 19);
        Choice(parent, "Hand", new[] { "Left hand", "Right hand" }, () => _fittingLeftHand ? 0 : 1, value => _fittingLeftHand = value == 0, -54);
        ActionButton(parent, "Wrist & palm angle  ›", 0, -125, () => { Back(); ShowPage(_handAnglesPage!); });
        ActionButton(parent, "Reset position", 1, -125, () => (_fittingLeftHand ? motion.LeftGloveOffset : motion.RightGloveOffset).Value = Vector3.zero);
        ActionButton(parent, "Reset this hand", 0, -177, () => motion.ResetFreeHand(_fittingLeftHand));
        Note(parent, "Hold thumb up; adjust with the other hand. Changes save\nautomatically. Held grips and input stay unchanged.", -223, 44);
    }

    private void BuildHandAnglesPage()
    {
        _handAnglesPage = Page("WRIST & PALM");
        var parent = _handAnglesPage.transform;
        var motion = ModConfiguration.Instance.MotionControls;
        Section(parent, "Free-hand angle correction", 214);
        HandAngleSetting(parent, "Wrist tilt", 0, 159);
        HandAngleSetting(parent, "Hand turn", 1, 89);
        HandAngleSetting(parent, "Palm roll", 2, 19);
        Choice(parent, "Palm preset", new[] { "Upright · thumbs up", "Down" }, () => WristMenuSettingsActions.PalmPreset(motion.FreeHandAngles(_fittingLeftHand), _fittingLeftHand),
            value => motion.SetFreeHandPalm(_fittingLeftHand, value == 0), -54);
        Choice(parent, "Hand", new[] { "Left hand", "Right hand" }, () => _fittingLeftHand ? 0 : 1, value => _fittingLeftHand = value == 0, -130);
        var positionLink = ActionButton(parent, "Position  ›", 0, -213, () => { Back(); ShowPage(_handPositionPage!); });
        var angleReset = ActionButton(parent, "Reset angle", 1, -213, () => (_fittingLeftHand ? motion.LeftGloveRotation : motion.RightGloveRotation).Value = Vector3.zero);
        var fullReset = ActionButton(parent, "Reset hand", 0, -213, () => motion.ResetFreeHand(_fittingLeftHand));
        CompactAction(positionLink, 0, 3);
        CompactAction(angleReset, 1, 3);
        CompactAction(fullReset, 2, 3);
    }

    private void BuildHudPage()
    {
        _displayPage = Page("HUD & MARKERS");
        var parent = _displayPage.transform;
        var config = ModConfiguration.Instance;
        Section(parent, "Flight instruments", 214);
        Setting(parent, "Flight HUD size", config.FlightHudScale, 159, Percent, .01f, value => WristMenuSettingsActions.FlightHudSize(config, value));
        Setting(parent, "Instrument brightness", config.InstrumentBrightness, 89, Percent, .01f);
        Setting(parent, "Status backplates", config.HudContrast, 0, 27);
        Setting(parent, "Declutter status", config.HudDeclutter, 1, 27);
        Section(parent, "Individual elements", -22);
        ActionButton(parent, "Symbols & labels  ›", 0, -70, () => ShowPage(_aimingPage!));
        ActionButton(parent, "Threats & feed  ›", 1, -70, () => ShowPage(_alertsPage!));
        ActionButton(parent, "Widget appearance  ›", 0, -120, OpenWidgetAppearance);
        ActionButton(parent, "Look direction cue  ›", 1, -120, () => ShowPage(_lookCuePage!));
        ActionButton(parent, "Day · 100%", 0, -168, () => config.InstrumentBrightness.Value = 1);
        ActionButton(parent, "Night · 50%", 1, -168, () => config.InstrumentBrightness.Value = .5f);
        ActionButton(parent, "Reset HUD", 0, -217, () => { config.FocusedFlightHud.Value = true; config.FlightHudScale.Value = .38f; config.MarkerScale.Value = .45f; config.MarkerLabelScale.Value = .55f; config.ReticleScale.Value = .45f; config.WeaponHudScale.Value = .5f; config.HudPanelScale.Value = 1.15f; config.HudPanelDistance.Value = 4; config.HudContrast.Value = true; config.HudDeclutter.Value = false; });
        ViewMenuControls.Text(_displayPage.transform, _font!, "Screen brightness stays native.", new Vector2(160, -217), new Vector2(300, 42), 16);
    }

    private void BuildAlertsPage()
    {
        _alertsPage = Page("THREATS & FEED");
        var parent = _alertsPage.transform;
        var config = ModConfiguration.Instance;
        var alerts = config.CockpitAlerts;
        Section(parent, "Cockpit overlays", 214);
        Setting(parent, "Compact alerts", alerts.Enabled, 0, 167);
        Setting(parent, "Kill feed", alerts.Feed, 1, 167);
        Setting(parent, "World notch cue", alerts.WorldNotch, 0, 119);
        Section(parent, "Appearance & placement", 64);
        ActionButton(parent, "Widget appearance  ›", 0, 15, OpenWidgetAppearance);
        ActionButton(parent, "Edit layout", 1, 15, StartOrResumeLayout);
        Note(parent, "M = missile · R = radar · J = jamming\n12h is the aircraft nose; 6h is behind.\nNOTCH is an evasion direction, not a threat bearing.\nQuiet scope disappears. Latest 3 feed entries only.\nWidget visibility also applies.", -117, 158);

    }

    private void BuildSymbolsPage()
    {
        _aimingPage = Page("SYMBOLS & LABELS");
        var parent = _aimingPage.transform;
        var config = ModConfiguration.Instance;
        Section(parent, "World-aligned HUD elements", 214);
        Setting(parent, "Target & location symbols", config.MarkerScale, 159, Percent, .01f);
        Setting(parent, "Marker labels", config.MarkerLabelScale, 89, Percent, .01f);
        Setting(parent, "Crosshair & bomb pipper", config.ReticleScale, 19, Percent, .01f);
        Setting(parent, "Weapon range readouts", config.WeaponHudScale, -51, Percent, .01f);
        Note(parent, "Size changes preserve each element's aim point.", -116, 42);
        ActionButton(parent, "Look direction cue  ›", 0, -185, () => ShowPage(_lookCuePage!));
        Setting(parent, "Follow Flight HUD size", config.SymbolsFollowFlightHud, 1, -185);
    }

    private void BuildLookCuePage()
    {
        _lookCuePage = Page("LOOK DIRECTION CUE");
        var parent = _lookCuePage.transform;
        var config = ModConfiguration.Instance;
        var cue = config.LookCue;
        Section(parent, "Appearance", 214);
        Setting(parent, "Size", cue.Size, 159, Percent, .05f);
        Setting(parent, "Opacity", cue.Opacity, 89, Percent, .01f);
        Choice(parent, "Shape", new[] { "Circle", "Dot", "Crosshair", "Brackets" }, () => (int)cue.CurrentShape, value => cue.Shape.Value = (GazeCueShape)value, 17);
        Choice(parent, "Show cue", new[] { "Near targets", "Always", "Off" }, () => (int)config.TargetCue.Value, value => config.TargetCue.Value = (TargetCueMode)value, -59);
        Setting(parent, "Vertical position", cue.VerticalDegrees, -140, _ => cue.PositionLabel, .25f);
        CompactAction(ActionButton(parent, "Reset cue", 0, -213, () => { cue.Reset(); config.TargetCue.Value = TargetCueMode.Context; }), 0, 3);
        CompactAction(ActionButton(parent, "Centre cue", 1, -213, () => cue.VerticalDegrees.Value = 0), 1, 3);
        CompactAction(ActionButton(parent, "Targeting  ›", 1, -213, () => ShowPage(_gazeTargetingPage!)), 2, 3);
    }

    private void BuildGazeTargetingPage()
    {
        _gazeTargetingPage = Page("GAZE TARGETING");
        var parent = _gazeTargetingPage.transform;
        var cue = ModConfiguration.Instance.LookCue;
        Section(parent, "Gaze lock", 214);
        Choice(parent, "Prefer inside the cue", new[] { "Nearest", "Units first", "Aircraft first" }, () => (int)cue.CurrentLockPriority,
            value => cue.LockPriority.Value = (GazeLockPriority)value, 159);
        Note(parent, "The preferred class wins inside the cue's cone; the nearest\nangle decides within it. Units first: aircraft, ground and\nships before missiles. A lone missile still locks.", 68, 84);
        Section(parent, "Eligible units", 2);
        Setting(parent, "Cue friendly units", cue.FriendlyUnits, 0, -42);
        Note(parent, "Off: no cue or gaze lock on allies. The game's own\ntarget filters (factions, unit types) still apply.", -108, 64);
    }
}
