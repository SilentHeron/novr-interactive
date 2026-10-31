using System;
using System.Collections.Generic;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

internal sealed partial class WristMenu
{
    private GameObject? _layoutPage, _hudAppearancePage, _hudWidgetPage, _hudDefaultPage;
    private Text? _layoutInfo, _hudWidgetInfo, _hudDefaultInfo;
    private readonly List<Button> _hudAvailableButtons = new(), _hudEditingButtons = new();
    private static CockpitHudWidgets? HudWidgets => CockpitHudWidgets.Instance;
    private string SelectedHudId => HudWidgets?.SelectedId ?? "weapons";

    private void CreateHudLayoutPages()
    {
        _layoutPage = Page("LAYOUT");
        AddHudButton("Save layout", 0, 0, () => { HudWidgets?.Finish(true); Close(); }, true);
        AddHudButton("Cancel", 1, 0, () => { HudWidgets?.Finish(false); Close(); }, true);
        AddHudButton("Continue moving", 0, 1, StartOrResumeLayout, true);
        AddHudButton("Bring within reach", 1, 1, () => { HudWidgets?.BringWithinReach(); Close(); }, true);
        AddHudButton("Widget appearance", 0, 2, OpenWidgetAppearance);
        AddHudButton("Reset preview", 1, 2, () => HudWidgets?.ResetPreview(), true);
        AddHudButton("Save HUD default…", 0, 3, () => ShowPage(_hudDefaultPage!), true);
        var snap = AddHudButton("", 1, 3, () => { if (HudWidgets != null) HudWidgets.SnapEnabled = !HudWidgets.SnapEnabled; });
        _liveLabels.Add((snap, () => "Edge snap: " + (HudWidgets?.SnapEnabled == true ? "ON" : "OFF")));
        _layoutInfo = Label(_layoutPage.transform, "", new Vector2(0, -132), new Vector2(510, 180), 18);

        _hudAppearancePage = Page("WIDGET APPEARANCE");
        for (var i = 0; i < HudWidgetProfiles.Ids.Length; i++)
        {
            var id = HudWidgetProfiles.Ids[i];
            AddHudButton(HudWidgetProfiles.Label(id), i % 2, i / 2, () =>
            {
                if (HudWidgets?.Available != true) return;
                HudWidgets!.Select(id); ShowPage(_hudWidgetPage!);
            });
        }
        Label(_hudAppearancePage.transform, "Choose a widget to change its look.\n\nTo move the HUD, stick and throttle together,\nuse Edit layout on the View home page.",
            new Vector2(0, -140), new Vector2(510, 150), 19);

        _hudWidgetPage = Page("HUD WIDGET");
        var visibility = AddHudButton("", 0, 0, () => ChangeHudPose(p => { p.Visible = !p.Visible; return p; }));
        _liveLabels.Add((visibility, () => "Visible: " + (HudWidgets?.GetPose(SelectedHudId).Visible == true ? "ON" : "OFF")));
        AddButton("All widgets", 1, 0, Back);
        AddHudButton("Smaller", 0, 1, () => ChangeHudPose(p => { p.Scale -= .1f; return p; }));
        AddHudButton("Larger", 1, 1, () => ChangeHudPose(p => { p.Scale += .1f; return p; }));
        AddHudButton("More transparent", 0, 2, () => ChangeHudPose(p => { p.Opacity -= .1f; return p; }));
        AddHudButton("More opaque", 1, 2, () => ChangeHudPose(p => { p.Opacity += .1f; return p; }));
        AddHudButton("Less background", 0, 3, () => ChangeHudPose(p => { p.Background -= .1f; return p; }));
        AddHudButton("More background", 1, 3, () => ChangeHudPose(p => { p.Background += .1f; return p; }));
        AddHudButton("Layout controls", 0, 4, ShowLayoutControls, true);
        _hudWidgetInfo = Label(_hudWidgetPage.transform, "", new Vector2(0, -163), new Vector2(510, 140), 18);

        _hudDefaultPage = Page("SAVE SHARED HUD LAYOUT");
        _hudDefaultInfo = Label(_hudDefaultPage.transform, "", new Vector2(0, 35), new Vector2(510, 270), 21);
        AddButton("Keep editing", 0, 5, Back);
        AddHudButton("Save shared default", 1, 5, () => { HudWidgets?.UseDefaultForAll(); Close(); }, true);
    }

    private Text AddHudButton(string text, int column, int row, Action action, bool editing = false)
    {
        var label = AddButton(text, column, row, action);
        var button = label.transform.parent.GetComponent<Button>();
        _hudAvailableButtons.Add(button);
        if (editing) _hudEditingButtons.Add(button);
        return label;
    }
    // The only placement entry point. Selecting an appearance page never opens
    // a placement preview or interrupts normal flight control.
    private static void StartOrResumeLayout()
    {
        if (HudWidgets?.Available != true) return;
        if (!CockpitHudWidgets.Editing) HudWidgets.Begin();
        if (CockpitHudWidgets.Editing) Close();
    }
    private void ChangeHudPose(Func<HudWidgetPose, HudWidgetPose> change)
    {
        if (HudWidgets?.Available == true)
            HudWidgets.SetAppearance(SelectedHudId, HudWidgetProfiles.Sanitize(SelectedHudId, change(HudWidgets.GetPose(SelectedHudId))));
    }
    private void ShowLayoutControls()
    {
        ShowPage(_layoutPage!);
        RefreshHudLayoutEditor();
    }
    private void OpenWidgetAppearance() { ShowPage(_hudAppearancePage!); RefreshHudLayoutEditor(); }
    private void RefreshHudLayoutEditor()
    {
        var manager = HudWidgets;
        var available = manager?.Available == true;
        foreach (var button in _hudAvailableButtons) if (button != null) button.interactable = available;
        foreach (var button in _hudEditingButtons) if (button != null) button.interactable = available && CockpitHudWidgets.Editing;
        if (_layoutInfo != null && _layoutPage?.activeSelf == true)
            _layoutInfo.text = !available ? "Enter a cockpit to arrange its layout." :
                "Grip any HUD widget, stick or throttle to move it.\nWidget angles stay fixed.\nGrab opposite corners with both hands to resize.\nLabels and corners appear only while editing.\n\nSave and cancel apply to everything together.\nBring within reach moves distant items closer.";
        if (_hudWidgetInfo != null && _hudWidgetPage?.activeSelf == true && manager != null)
        {
            var pose = manager.GetPose(SelectedHudId);
            _hudWidgetInfo.text = HudWidgetProfiles.Label(SelectedHudId) +
                $"\nSize {pose.Scale * 100:0}% · Opacity {pose.Opacity * 100:0}% · Background {pose.Background * 100:0}%\n\n" +
                (CockpitHudWidgets.Editing ? "Preview only; save or cancel in Layout controls.\nEmpty / hidden widgets show while editing." :
                    "Appearance changes save automatically.\nUse Edit layout on View home to move things.") +
                "\nSeat adjustments leave the widgets in place.";
        }
        if (_hudDefaultInfo != null && _hudDefaultPage?.activeSelf == true)
            _hudDefaultInfo.text = "Use these widgets as the shared HUD layout?\n\nThis aircraft and uncustomized aircraft\nwill follow it. Other saved aircraft layouts\nstay unchanged.\n\nHandle positions save for this aircraft only.\nSaving ends the entire layout preview.";
    }
}
