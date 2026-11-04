using System;
using System.Collections.Generic;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

internal sealed partial class WristMenu
{
    private GameObject? _layoutPage, _hudAppearancePage, _hudWidgetPage, _aircraftProfilesPage, _hudAnglePage;
    private Text? _layoutInfo, _hudWidgetInfo, _aircraftProfilesInfo, _hudAngleInfo;
    private Slider? _hudBackplateSlider;
    private ViewMenuControls.Check? _hudVisibility, _hudSnap;
    private readonly List<(ViewMenuControls.Scalar control, bool editing)> _hudSliders = new();
    private Text? _hudBackplateLabel;
    private readonly List<Button> _profileLoadButtons = new();
    private readonly List<Button> _profileSaveButtons = new();
    private readonly List<Button> _hudAvailableButtons = new(), _hudEditingButtons = new();
    private static CockpitHudWidgets? HudWidgets => CockpitHudWidgets.Instance;
    private string SelectedHudId => HudWidgets?.SelectedId ?? "weapons";

    private void CreateHudLayoutPages()
    {
        BuildLayoutPage();
        BuildWidgetListPage();
        BuildWidgetAppearancePage();
        BuildWidgetAnglePage();
        BuildAircraftProfilesPage();
    }

    private void BuildLayoutPage()
    {
        _layoutPage = Page("LAYOUT");
        var parent = _layoutPage.transform;
        Section(parent, "Current layout preview", 214);
        var save = AddHudButton(parent, "", 0, 0, () => { if (HudWidgets?.SaveToSlot(HudWidgets.ProfileSlot) == true) Close(); }, true);
        _profileSaveButtons.Add(save.transform.parent.GetComponent<Button>());
        _liveLabels.Add((save, () => $"Save {HudWidgets?.ProfileAircraftName ?? "aircraft"}\nSlot {HudWidgets?.ProfileSlot ?? 1}"));
        AddHudButton(parent, "Cancel", 1, 0, () => { HudWidgets?.Finish(false); Close(); }, true);
        AddHudButton(parent, "Continue moving", 0, 1, StartOrResumeLayout, true);
        AddHudButton(parent, "Bring within reach", 1, 1, () => { HudWidgets?.BringWithinReach(); Close(); }, true);
        AddHudButton(parent, "Widget appearance", 0, 2, OpenWidgetAppearance);
        AddHudButton(parent, "Reset preview", 1, 2, () => HudWidgets?.ResetPreview(), true);
        AddHudButton(parent, "Aircraft profiles", 0, 3, OpenAircraftProfiles);
        _hudSnap = ViewMenuControls.Checkbox(_layoutPage.transform, _font!, "Edge snap", new Vector2(160, -3), 300,
            () => HudWidgets?.SnapEnabled == true, value => { if (HudWidgets?.Available == true) HudWidgets.SnapEnabled = value; });
        _layoutInfo = Label(_layoutPage.transform, "", new Vector2(0, -132), new Vector2(510, 180), 18);
    }

    private void BuildWidgetListPage()
    {
        _hudAppearancePage = Page("WIDGET APPEARANCE");
        var parent = _hudAppearancePage.transform;
        Section(parent, "Choose an overlay", 214);
        for (var i = 0; i < HudWidgetProfiles.Ids.Length; i++)
        {
            var id = HudWidgetProfiles.Ids[i];
            AddHudButton(parent, HudWidgetProfiles.Label(id), i % 2, i / 2, () =>
            {
                if (HudWidgets?.Available != true)
                    return;
                HudWidgets!.Select(id);
                ShowPage(_hudWidgetPage!);
            });
        }
        Label(_hudAppearancePage.transform, "Choose a widget to change its look.\nUse Edit layout to move the HUD and handles.",
            new Vector2(0, -185), new Vector2(510, 70), 18);
    }

    private void BuildWidgetAppearancePage()
    {
        _hudWidgetPage = Page("HUD WIDGET");
        var parent = _hudWidgetPage.transform;
        Section(parent, "Selected widget", 214);
        _hudVisibility = ViewMenuControls.Checkbox(_hudWidgetPage.transform, _font!, "Visible", new Vector2(-160, 166), 300,
            () => HudWidgets?.GetPose(SelectedHudId).Visible == true,
            value => ChangeHudPose(p => { p.Visible = value; return p; }));
        ActionButton(parent, "All widgets  ›", 1, 166, Back);
        HudScalar(parent, "Size", 99, HudWidgetProfiles.MinScale, HudWidgetProfiles.MaxScale,
            () => HudWidgets?.GetPose(SelectedHudId).Scale ?? 1, value => ChangeHudPose(p => { p.Scale = value; return p; }), Percent, .01f);
        HudScalar(parent, "Readout opacity", 29, HudWidgetProfiles.MinOpacity, 1,
            () => HudWidgets?.GetPose(SelectedHudId).Opacity ?? 1, value => ChangeHudPose(p => { p.Opacity = value; return p; }), Percent, .01f);
        CreateHudBackplateSlider(_hudWidgetPage.transform);
        AddHudButton(parent, "Layout controls", 0, 5, ShowLayoutControls, true);
        AddHudButton(parent, "Angle", 1, 5, () => { ShowPage(_hudAnglePage!); RefreshHudLayoutEditor(); }, true);
        _hudWidgetInfo = Label(_hudWidgetPage.transform, "", new Vector2(0, -194), new Vector2(620, 92), 18);
    }

    private void BuildWidgetAnglePage()
    {
        _hudAnglePage = Page("WIDGET ANGLE");
        var parent = _hudAnglePage.transform;
        Section(parent, "Layout preview only", 214);
        HudScalar(parent, "Pitch", 159, -80, 80, () => HudWidgets?.GetPose(SelectedHudId).Euler.x ?? 0,
            value => SetHudAngle(value, true), Degrees, 1, true);
        HudScalar(parent, "Yaw", 89, -180, 180, () => HudWidgets?.GetPose(SelectedHudId).Euler.y ?? 0,
            value => SetHudAngle(value, false), Degrees, 1, true);
        AddHudButton(parent, "Face forward", 0, 3, () =>
        {
            if (HudWidgets?.Available == true && CockpitHudWidgets.Editing)
                HudWidgets.SetAngles(SelectedHudId, Vector2.zero);
            RefreshHudLayoutEditor();
        }, true);
        AddHudButton(parent, "Continue moving", 1, 3, StartOrResumeLayout, true);
        _hudAngleInfo = Label(_hudAnglePage.transform, "", new Vector2(0, -142), new Vector2(620, 200), 19);
    }

    private void BuildAircraftProfilesPage()
    {
        _aircraftProfilesPage = Page("AIRCRAFT PROFILES");
        var parent = _aircraftProfilesPage.transform;
        Section(parent, "Load a saved layout", 214, 300, -160);
        Section(parent, "Save this preview", 214, 300, 160);
        for (var slot = 1; slot <= 3; slot++)
        {
            var selectedSlot = slot;
            var load = AddHudButton(parent, "", 0, slot - 1, () => { HudWidgets?.LoadSlot(selectedSlot); RefreshHudLayoutEditor(); });
            _liveLabels.Add((load, () => $"Load Slot {selectedSlot}" + (HudWidgets?.ProfileSlot == selectedSlot ? " · active" : "")));
            _profileLoadButtons.Add(load.transform.parent.GetComponent<Button>());
            var store = AddHudButton(parent, "", 1, slot - 1, () => { if (HudWidgets?.SaveToSlot(selectedSlot) == true) Close(); }, true);
            _profileSaveButtons.Add(store.transform.parent.GetComponent<Button>());
            _liveLabels.Add((store, () => $"Save {HudWidgets?.ProfileAircraftName ?? "aircraft"}\nSlot {selectedSlot}"));
        }
        AddButton(parent, "Edit current slot", 0, 3, StartOrResumeLayout);
        _aircraftProfilesInfo = Label(_aircraftProfilesPage.transform, "", new Vector2(0, -125), new Vector2(510, 200), 19);
    }

    private Text AddHudButton(Transform parent, string text, int column, int row, Action action, bool editing = false)
    {
        var label = AddButton(parent, text, column, row, action);
        var button = label.transform.parent.GetComponent<Button>();
        ((RectTransform)button.transform).anchoredPosition = new Vector2(column == 0 ? -160 : 160, 168 - row * 57);
        _hudAvailableButtons.Add(button);
        if (editing)
            _hudEditingButtons.Add(button);
        return label;
    }
    // The only placement entry point. Selecting an appearance page never opens
    // a placement preview or interrupts normal flight control.
    private static void StartOrResumeLayout()
    {
        if (HudWidgets?.Available != true)
            return;
        if (!CockpitHudWidgets.Editing)
            HudWidgets.Begin();
        if (CockpitHudWidgets.Editing)
            Close();
    }
    private void ChangeHudPose(Func<HudWidgetPose, HudWidgetPose> change)
    {
        if (HudWidgets?.Available == true)
            HudWidgets.SetAppearance(SelectedHudId, HudWidgetProfiles.Sanitize(SelectedHudId, change(HudWidgets.GetPose(SelectedHudId))));
    }
    private void ChangeHudAngles(float pitch, float yaw)
    {
        var manager = HudWidgets;
        if (manager?.Available != true || !CockpitHudWidgets.Editing)
            return;
        var angles = manager.GetPose(SelectedHudId).Euler;
        manager.SetAngles(SelectedHudId, new Vector2(angles.x + pitch, angles.y + yaw));
        RefreshHudLayoutEditor();
    }
    private ViewMenuControls.Scalar HudScalar(Transform parent, string title, float y, float min, float max, Func<float> read, Action<float> write, Func<float, string> format, float step, bool editing = false)
    {
        var control = ViewMenuControls.Slider(parent, _font!, title, y, min, max, read, value =>
        {
            if (HudWidgets?.Available != true || editing && !CockpitHudWidgets.Editing)
                return;
            write(value);
            RefreshHudLayoutEditor();
        }, format, step);
        _hudSliders.Add((control, editing));
        return control;
    }
    private void SetHudAngle(float value, bool pitch)
    {
        var manager = HudWidgets;
        if (manager?.Available != true || !CockpitHudWidgets.Editing)
            return;
        var angles = manager.GetPose(SelectedHudId).Euler;
        manager.SetAngles(SelectedHudId, new Vector2(pitch ? value : angles.x, pitch ? angles.y : value));
    }
    private void CreateHudBackplateSlider(Transform parent)
    {
        var control = HudScalar(parent, "Backplate opacity", -41, 0, 100, () => (HudWidgets?.GetPose(SelectedHudId).Background ?? 0) * 100,
            value => ChangeHudPose(p => { p.Background = value * .01f; return p; }), value => $"{value:0}%", 1);
        _hudBackplateSlider = control.Slider;
        _hudBackplateLabel = control.Value;
    }
    private void ShowLayoutControls()
    {
        ShowPage(_layoutPage!);
        RefreshHudLayoutEditor();
    }
    private void OpenAircraftProfiles()
    {
        ShowPage(_aircraftProfilesPage!);
        RefreshHudLayoutEditor();
    }
    private void OpenWidgetAppearance()
    {
        ShowPage(_hudAppearancePage!);
        RefreshHudLayoutEditor();
    }
    private void RefreshHudLayoutEditor()
    {
        var manager = HudWidgets;
        manager?.FocusAppearance(_hudWidgetPage?.activeSelf == true ? SelectedHudId : null);
        var available = manager?.Available == true;
        foreach (var button in _hudAvailableButtons)
            if (button != null)
                button.interactable = available;
        foreach (var button in _hudEditingButtons)
            if (button != null)
                button.interactable = available && CockpitHudWidgets.Editing;
        foreach (var button in _profileLoadButtons)
            if (button != null)
                button.interactable = available && manager?.CanSaveProfile == true && !CockpitHudWidgets.Editing;
        foreach (var button in _profileSaveButtons)
            if (button != null)
                button.interactable = available && manager?.CanSaveProfile == true && CockpitHudWidgets.Editing;
        foreach (var item in _hudSliders)
        {
            item.control.Slider.interactable = available && (!item.editing || CockpitHudWidgets.Editing);
            item.control.Refresh();
        }
        if (_hudVisibility != null)
        {
            _hudVisibility.Toggle.interactable = available;
            _hudVisibility.Refresh();
        }
        if (_hudSnap != null)
        {
            _hudSnap.Toggle.interactable = available;
            _hudSnap.Refresh();
        }
        if (_layoutInfo != null && _layoutPage?.activeSelf == true)
            _layoutInfo.text = !available ? "Enter a cockpit to arrange its layout." : manager?.CanSaveProfile != true ?
                "Aircraft loading. All widgets can be previewed.\nSaving becomes available when its profile is ready." :
                "Grip HUD widgets or enabled handles to move.\nTilt a held widget for pitch / yaw; roll stays level.\nUse both corners to move, tilt and resize together.\nClick either thumbstick to reopen this menu.\n\nSave and cancel apply to everything together.\nBring within reach moves distant items closer.";
        if (_hudWidgetInfo != null && _hudWidgetPage?.activeSelf == true && manager != null)
        {
            var pose = manager.GetPose(SelectedHudId);
            _hudWidgetInfo.text = HudWidgetProfiles.Label(SelectedHudId) +
                "\nBackplate opacity leaves readouts unchanged.\n" +
                (manager.CanSaveProfile != true ? "Aircraft loading; appearance is preview only." : CockpitHudWidgets.Editing ? "Preview only; save or cancel in Layout controls.\nEmpty / hidden widgets show while editing." :
                    "Appearance changes save automatically.\nUse Edit layout to move or angle this widget.");
        }
        if (_hudAngleInfo != null && _hudAnglePage?.activeSelf == true)
        {
            var angles = manager?.GetPose(SelectedHudId).Euler ?? Vector3.zero;
            _hudAngleInfo.text = !available ? "Enter a cockpit to adjust a widget." :
                HudWidgetProfiles.Label(SelectedHudId) + $"\nPitch {angles.x:0.#}° · Yaw {angles.y:0.#}°\nRoll stays level.\n\n" +
                (CockpitHudWidgets.Editing ? "Drag a slider or grip the widget and tilt it.\n\nSave or cancel in Layout controls." :
                    "Start Edit layout from View home first.\nAngles are saved with the layout.");
        }
        if (_aircraftProfilesInfo != null && _aircraftProfilesPage?.activeSelf == true)
            _aircraftProfilesInfo.text = !available ? "Enter a cockpit to use its profiles." : manager?.CanSaveProfile != true ?
                "Aircraft loading. Saving and loading will become\navailable when its profile is ready." :
                $"{manager!.ProfileAircraftName} · Slot {manager.ProfileSlot}\nThree separate layouts for this aircraft only.\nSeat, handles and HUD widgets save together.\n\n" +
                (CockpitHudWidgets.Editing ? "Save to any slot above, or cancel in Layout.\nLoading is disabled while editing." :
                    "Load a slot, then choose Edit current slot.\nExisting layouts are preserved in Slot 1.");
    }
}
