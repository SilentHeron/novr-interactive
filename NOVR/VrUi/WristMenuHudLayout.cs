using System;
using System.Collections.Generic;
using NOVR.MotionControls;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

internal sealed partial class WristMenu
{
    private GameObject? _hudLayoutPage, _hudWidgetPage, _hudDefaultPage;
    private Text? _hudLayoutInfo, _hudWidgetInfo, _hudDefaultInfo;
    private readonly List<Button> _hudAvailableButtons = new(), _hudEditingButtons = new();
    private static CockpitHudWidgets? HudWidgets => CockpitHudWidgets.Instance;
    private string SelectedHudId => HudWidgets?.SelectedId ?? "weapons";

    private void CreateHudLayoutPages()
    {
        _hudLayoutPage = Page("HUD LAYOUT");
        for (var i = 0; i < HudWidgetProfiles.Ids.Length; i++)
        {
            var id = HudWidgetProfiles.Ids[i];
            AddHudButton(HudWidgetProfiles.Label(id), i % 2, i / 2, () =>
            {
                if (!BeginHudEdit()) return;
                HudWidgets!.Select(id); ShowPage(_hudWidgetPage!);
            });
        }
        AddHudButton("Move by hand", 0, 3, MoveHudByHand);
        AddHudButton("Save & close", 1, 3, SaveHudEdit, true);
        AddHudButton("Cancel preview", 0, 4, () => { HudWidgets?.Finish(false); Close(); }, true);
        AddHudButton("Reset preview", 1, 4, () => { if (BeginHudEdit()) HudWidgets!.ResetPreview(); });
        AddHudButton("Save as default…", 0, 5, () => { if (BeginHudEdit()) ShowPage(_hudDefaultPage!); });
        var snap = AddHudButton("", 1, 5, () => { if (HudWidgets != null) HudWidgets.SnapEnabled = !HudWidgets.SnapEnabled; });
        _liveLabels.Add((snap, () => "Edge snap: " + (HudWidgets?.SnapEnabled == true ? "ON" : "OFF")));
        _hudLayoutInfo = Label(_hudLayoutPage.transform, "", new Vector2(0, -194), new Vector2(510, 110), 18);

        _hudWidgetPage = Page("HUD WIDGET");
        var visibility = AddHudButton("", 0, 0, () => ChangeHudPose(p => { p.Visible = !p.Visible; return p; }));
        _liveLabels.Add((visibility, () => "Visible: " + (HudWidgets?.GetPose(SelectedHudId).Visible == true ? "ON" : "OFF")));
        AddHudButton("Move by hand", 1, 0, MoveHudByHand);
        AddHudButton("Smaller", 0, 1, () => ChangeHudPose(p => { p.Scale -= .1f; return p; }));
        AddHudButton("Larger", 1, 1, () => ChangeHudPose(p => { p.Scale += .1f; return p; }));
        AddHudButton("More transparent", 0, 2, () => ChangeHudPose(p => { p.Opacity -= .1f; return p; }));
        AddHudButton("More opaque", 1, 2, () => ChangeHudPose(p => { p.Opacity += .1f; return p; }));
        AddHudButton("Less background", 0, 3, () => ChangeHudPose(p => { p.Background -= .1f; return p; }));
        AddHudButton("More background", 1, 3, () => ChangeHudPose(p => { p.Background += .1f; return p; }));
        AddButton("All widgets", 0, 4, Back);
        AddHudButton("Save & close", 1, 4, SaveHudEdit, true);
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
    private static bool BeginHudEdit()
    {
        if (HudWidgets?.Available != true) return false;
        if (!CockpitHudWidgets.Editing)
        {
            CockpitLayout.Instance?.Finish(false);
            HudWidgets.Begin();
        }
        return CockpitHudWidgets.Editing;
    }
    private static void MoveHudByHand() { if (BeginHudEdit()) Close(); }
    private static void SaveHudEdit() { HudWidgets?.Finish(true); Close(); }
    private void ChangeHudPose(Func<HudWidgetPose, HudWidgetPose> change)
    {
        if (BeginHudEdit()) HudWidgets!.SetPose(SelectedHudId, HudWidgetProfiles.Sanitize(SelectedHudId, change(HudWidgets.GetPose(SelectedHudId))));
    }
    private void OpenHudLayout()
    {
        ShowPage(_hudLayoutPage!);
        RefreshHudLayoutEditor();
    }
    private void RefreshHudLayoutEditor()
    {
        var manager = HudWidgets;
        var available = manager?.Available == true;
        foreach (var button in _hudAvailableButtons) if (button != null) button.interactable = available;
        foreach (var button in _hudEditingButtons) if (button != null) button.interactable = available && CockpitHudWidgets.Editing;
        if (_hudLayoutInfo != null && _hudLayoutPage?.activeSelf == true)
            _hudLayoutInfo.text = !available ? "Enter a cockpit to arrange its HUD widgets." :
                "Grip a widget to move it; its angle stays fixed.\nHold grip + thumbstick up/down to resize.\nView returns here. Save or cancel when finished.\nReset preview uses your shared default layout.";
        if (_hudWidgetInfo != null && _hudWidgetPage?.activeSelf == true && manager != null)
        {
            var pose = manager.GetPose(SelectedHudId);
            _hudWidgetInfo.text = HudWidgetProfiles.Label(SelectedHudId) +
                $"\nSize {pose.Scale * 100:0}% · Opacity {pose.Opacity * 100:0}% · Background {pose.Background * 100:0}%\n\nChanges are a preview until saved.\nEmpty / hidden widgets show while editing.\nSeat adjustments leave the widgets in place.";
        }
        if (_hudDefaultInfo != null && _hudDefaultPage?.activeSelf == true)
            _hudDefaultInfo.text = "Use this preview as the shared HUD layout?\n\nThis aircraft and uncustomized aircraft\nwill follow it. Other saved aircraft layouts\nstay unchanged.\n\nSaving ends this preview.";
    }
}
