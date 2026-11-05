using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class TextSizeMenuChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var manager = new CockpitHudWidgets();
        CockpitHudWidgets.Instance = manager;
        var menu = new WristMenu();
        menu.Build();
        foreach (var id in HudWidgetProfiles.Ids)
        {
            menu.Appearance();
            menu.Click("WIDGET APPEARANCE", id);
            menu.Refresh();
            var slider = menu.Scalar("Text size");
            var supported = id is "feed" or "objectives";
            check(slider.gameObject.activeSelf == supported && slider.interactable == supported,
                "Only text-only widget pages show and accept text-size adjustment: " + id);
            var writes = manager.AppearanceSaves;
            var before = manager.GetPose(id);
            slider.value = 1.25f;
            if (!supported)
            {
                check(manager.AppearanceSaves == writes && manager.GetPose(id).TextScale == 1,
                    "Late events on a hidden text slider cannot edit graphical widgets: " + id);
                continue;
            }
            check(slider.minValue == .65f && slider.maxValue == 1.5f,
                "Text size has the same bounded range in both menus");
            var changed = manager.GetPose(id);
            check(changed.TextScale == 1.25f && changed.Scale == before.Scale && changed.Background == before.Background &&
                changed.Opacity == before.Opacity && changed.Position == before.Position && changed.Euler == before.Euler,
                "Text resizing leaves panel geometry and appearance independent: " + id);
            check(manager.Begins == 0 && !CockpitHudWidgets.Editing,
                "Text size is accessible without entering layout editing");
            var track = (RectTransform)slider.transform;
            var backplate = (RectTransform)menu.Backplate.transform;
            var layout = (RectTransform)menu.Buttons[("HUD WIDGET", "Layout controls")].label.transform.parent;
            check(track.anchoredPosition.y + track.sizeDelta.y * .5f < backplate.anchoredPosition.y - backplate.sizeDelta.y * .5f &&
                layout.anchoredPosition.y < track.anchoredPosition.y - track.sizeDelta.y * .5f,
                "Extra slider has its own row above the layout actions");
            writes = manager.AppearanceSaves;
            menu.Refresh();
            check(manager.AppearanceSaves == writes && slider.value == 1.25f,
                "Refreshing a saved size does not write or start an edit");
        }

        menu.Appearance();
        menu.Click("WIDGET APPEARANCE", "feed");
        menu.Refresh();
        manager.Begin();
        menu.Refresh();
        menu.Scalar("Text size").value = 1.5f;
        check(manager.GetPose("feed").TextScale == 1.5f && manager.GetPose("objectives").TextScale == 1.25f,
            "Text-size preview changes only its selected text panel");
        manager.Finish(false);
        menu.Refresh();
        check(menu.Scalar("Text size").value == 1.25f,
            "Cancel restores the previous text size in the real menu readback");
        manager.Available = false;
        menu.Refresh();
        var saves = manager.AppearanceSaves;
        menu.Scalar("Text size").value = .65f;
        check(manager.AppearanceSaves == saves && !menu.Scalar("Text size").interactable,
            "Missing cockpit rejects stale text slider input");
    }
}
