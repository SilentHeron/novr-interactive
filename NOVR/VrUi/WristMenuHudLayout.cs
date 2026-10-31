using System;
using System.Collections.Generic;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

internal sealed partial class WristMenu
{
    private GameObject? _layoutPage, _hudAppearancePage, _hudWidgetPage, _hudDefaultPage, _hudAnglePage;
    private Text? _layoutInfo, _hudWidgetInfo, _hudDefaultInfo, _hudAngleInfo;
    private Slider? _hudBackplateSlider;
    private Text? _hudBackplateLabel;
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
        Label(_hudAppearancePage.transform, "Choose a widget to change its look.\nUse Edit layout to move the HUD and handles.",
            new Vector2(0, -185), new Vector2(510, 70), 18);

        _hudWidgetPage = Page("HUD WIDGET");
        var visibility = AddHudButton("", 0, 0, () => ChangeHudPose(p => { p.Visible = !p.Visible; return p; }));
        _liveLabels.Add((visibility, () => "Visible: " + (HudWidgets?.GetPose(SelectedHudId).Visible == true ? "ON" : "OFF")));
        AddButton("All widgets", 1, 0, Back);
        AddHudButton("Smaller", 0, 1, () => ChangeHudPose(p => { p.Scale -= .1f; return p; }));
        AddHudButton("Larger", 1, 1, () => ChangeHudPose(p => { p.Scale += .1f; return p; }));
        AddHudButton("Fainter readouts", 0, 2, () => ChangeHudPose(p => { p.Opacity -= .1f; return p; }));
        AddHudButton("Brighter readouts", 1, 2, () => ChangeHudPose(p => { p.Opacity += .1f; return p; }));
        CreateHudBackplateSlider(_hudWidgetPage.transform);
        AddHudButton("Layout controls", 0, 4, ShowLayoutControls, true);
        AddHudButton("Angle", 1, 4, () => { ShowPage(_hudAnglePage!); RefreshHudLayoutEditor(); }, true);
        _hudWidgetInfo = Label(_hudWidgetPage.transform, "", new Vector2(0, -163), new Vector2(510, 140), 18);

        _hudAnglePage = Page("WIDGET ANGLE");
        AddHudButton("Pitch −5°", 0, 0, () => ChangeHudAngles(-5, 0), true);
        AddHudButton("Pitch +5°", 1, 0, () => ChangeHudAngles(5, 0), true);
        AddHudButton("Yaw −5°", 0, 1, () => ChangeHudAngles(0, -5), true);
        AddHudButton("Yaw +5°", 1, 1, () => ChangeHudAngles(0, 5), true);
        AddHudButton("Face forward", 0, 2, () =>
        {
            if (HudWidgets?.Available == true && CockpitHudWidgets.Editing)
                HudWidgets.SetAngles(SelectedHudId, Vector2.zero);
            RefreshHudLayoutEditor();
        }, true);
        AddHudButton("Continue moving", 1, 2, StartOrResumeLayout, true);
        _hudAngleInfo = Label(_hudAnglePage.transform, "", new Vector2(0, -92), new Vector2(510, 240), 19);

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
    private void ChangeHudAngles(float pitch, float yaw)
    {
        var manager = HudWidgets;
        if (manager?.Available != true || !CockpitHudWidgets.Editing) return;
        var angles = manager.GetPose(SelectedHudId).Euler;
        manager.SetAngles(SelectedHudId, new Vector2(angles.x + pitch, angles.y + yaw));
        RefreshHudLayoutEditor();
    }
    private void CreateHudBackplateSlider(Transform parent)
    {
        var go = new GameObject("Backplate opacity", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
        var root = (RectTransform)go.transform;
        root.SetParent(parent, false); root.anchoredPosition = new Vector2(0, 6); root.sizeDelta = new Vector2(510, 49);
        go.GetComponent<Image>().color = Color.clear;
        _hudBackplateLabel = Label(root, "Backplate opacity 0%", new Vector2(0, 18), new Vector2(510, 24), 18);
        Label(root, "Clear", new Vector2(-224, -10), new Vector2(62, 26), 16);
        Label(root, "Solid", new Vector2(224, -10), new Vector2(62, 26), 16);

        var trackObject = new GameObject("Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var track = (RectTransform)trackObject.transform; track.SetParent(root, false);
        track.anchoredPosition = new Vector2(0, -10); track.sizeDelta = new Vector2(370, 12);
        var trackImage = trackObject.GetComponent<Image>(); trackImage.color = new Color(.10f, .24f, .29f); trackImage.raycastTarget = false;
        var areaObject = new GameObject("Handle area", typeof(RectTransform));
        var area = (RectTransform)areaObject.transform; area.SetParent(root, false);
        area.anchoredPosition = new Vector2(0, -10); area.sizeDelta = new Vector2(370, 30);
        var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var handle = (RectTransform)handleObject.transform; handle.SetParent(area, false); handle.sizeDelta = new Vector2(30, 30);
        var handleImage = handleObject.GetComponent<Image>(); handleImage.color = new Color(.65f, .88f, .96f);

        _hudBackplateSlider = go.GetComponent<Slider>();
        _hudBackplateSlider.minValue = 0; _hudBackplateSlider.maxValue = 100; _hudBackplateSlider.wholeNumbers = true;
        _hudBackplateSlider.direction = Slider.Direction.LeftToRight;
        _hudBackplateSlider.handleRect = handle; _hudBackplateSlider.targetGraphic = handleImage;
        _hudBackplateSlider.navigation = new Navigation { mode = Navigation.Mode.None };
        _hudBackplateSlider.onValueChanged.AddListener(value =>
        {
            var manager = HudWidgets;
            if (manager?.Available != true || Mathf.Approximately(manager.GetPose(SelectedHudId).Background, value * .01f)) return;
            ChangeHudPose(p => { p.Background = value * .01f; return p; });
            RefreshHudLayoutEditor();
        });
        LayerHelper.SetLayerRecursive(root, LayerHelper.GetVrUiLayer());
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
        manager?.FocusAppearance(_hudWidgetPage?.activeSelf == true ? SelectedHudId : null);
        var available = manager?.Available == true;
        foreach (var button in _hudAvailableButtons) if (button != null) button.interactable = available;
        foreach (var button in _hudEditingButtons) if (button != null) button.interactable = available && CockpitHudWidgets.Editing;
        if (_hudBackplateSlider != null) _hudBackplateSlider.interactable = available;
        if (_layoutInfo != null && _layoutPage?.activeSelf == true)
            _layoutInfo.text = !available ? "Enter a cockpit to arrange its layout." :
                "Grip any HUD widget, stick or throttle to move it.\nTilt a held widget for pitch / yaw; roll stays level.\nGrab opposite corners with both hands to resize.\nLabels and corners appear only while editing.\n\nSave and cancel apply to everything together.\nBring within reach moves distant items closer.";
        if (_hudWidgetInfo != null && _hudWidgetPage?.activeSelf == true && manager != null)
        {
            var pose = manager.GetPose(SelectedHudId);
            if (_hudBackplateSlider != null) _hudBackplateSlider.SetValueWithoutNotify(pose.Background * 100);
            if (_hudBackplateLabel != null) _hudBackplateLabel.text = $"Backplate opacity {pose.Background * 100:0}%";
            _hudWidgetInfo.text = HudWidgetProfiles.Label(SelectedHudId) +
                $"\nSize {pose.Scale * 100:0}% · Readouts {pose.Opacity * 100:0}%\nBackplate opacity leaves readouts unchanged.\n\n" +
                (CockpitHudWidgets.Editing ? "Preview only; save or cancel in Layout controls.\nEmpty / hidden widgets show while editing." :
                    "Appearance changes save automatically.\nUse Edit layout to move or angle this widget.");
        }
        if (_hudAngleInfo != null && _hudAnglePage?.activeSelf == true)
        {
            var angles = manager?.GetPose(SelectedHudId).Euler ?? Vector3.zero;
            _hudAngleInfo.text = !available ? "Enter a cockpit to adjust a widget." :
                HudWidgetProfiles.Label(SelectedHudId) + $"\nPitch {angles.x:0.#}° · Yaw {angles.y:0.#}°\nRoll stays level.\n\n" +
                (CockpitHudWidgets.Editing ? "Use 5° steps for precise adjustment, or\ngrip the widget and tilt your controller.\n\nSave or cancel in Layout controls." :
                    "Start Edit layout from View home first.\nAngles are saved with the layout.");
        }
        if (_hudDefaultInfo != null && _hudDefaultPage?.activeSelf == true)
            _hudDefaultInfo.text = "Use these widgets as the shared HUD layout?\n\nThis aircraft and uncustomized aircraft\nwill follow it. Other saved aircraft layouts\nstay unchanged.\n\nHandle positions save for this aircraft only.\nSaving ends the entire layout preview.";
    }
}
