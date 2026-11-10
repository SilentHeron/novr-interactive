using NOVR;
using NOVR.GameplayPatches;
using NOVR.VrUi;
using UnityEngine;

internal static class ImageQualityMenuChecks
{
    internal static void Run(Action<bool, string> check)
    {
        ModConfiguration.Instance = new();
        var settings = ModConfiguration.Instance.ImageQuality;
        var menu = new WristMenu();
        var page = menu.BuildImageQuality();
        menu.Refresh();
        var checks = page.transform.Checks;
        check(page.Name == "IMAGE QUALITY" && checks.Keys.Order().SequenceEqual(new[]
            {
                "Dither gradients", "Filter cockpit screens", "Hide distant cloud banks", "No edge darkening", "Smooth cockpit map"
            }),
            "The image page offers the four image options and the cloud option that moved here.");
        check(checks.Values.All(box => box.Toggle.isOn) && settings.DitherGradients.Writes == 0 &&
              settings.RemoveRestingVignette.Writes == 0 && settings.FilterCockpitScreens.Writes == 0 &&
              settings.SmoothCockpitMap.Writes == 0 && DistantCloudBanks.Calls.Count == 0,
            "Building and refreshing the page shows the saved values and writes nothing.");
        var bound = new (string Label, NOVR.Rendering.SettingValue<bool> Entry)[]
        {
            ("Dither gradients", settings.DitherGradients), ("No edge darkening", settings.RemoveRestingVignette),
            ("Filter cockpit screens", settings.FilterCockpitScreens), ("Smooth cockpit map", settings.SmoothCockpitMap)
        };
        foreach (var (label, entry) in bound)
        {
            checks[label].Write(false);
            menu.Refresh();
            check(!entry.Value && !checks[label].Toggle.isOn && bound.Count(other => !other.Entry.Value) == 1,
                label + " switches its own setting off and no other.");
            checks[label].Write(true);
            menu.Refresh();
            check(entry.Value && checks[label].Toggle.isOn, label + " switches back on.");
        }
        checks["Hide distant cloud banks"].Write(false);
        check(DistantCloudBanks.Calls.SequenceEqual(new[] { false }) && bound.All(other => other.Entry.Value),
            "The cloud option goes through its own live-applying setter.");
        DistantCloudBanks.SetHideInVr(true);
        DistantCloudBanks.Calls.Clear();
        // Nothing may overlap or leave the page.
        var widgets = page.transform.Widgets;
        check(widgets.All(w => w.Left >= -310.01f && w.Right <= 310.01f && w.Top <= 229.01f && w.Bottom >= -238.01f),
            "Every control and note stays inside the page.");
        for (var a = 0; a < widgets.Count; a++)
            for (var b = a + 1; b < widgets.Count; b++)
                check(widgets[a].Right <= widgets[b].Left + .01f || widgets[b].Right <= widgets[a].Left + .01f ||
                      widgets[a].Top <= widgets[b].Bottom + .01f || widgets[b].Top <= widgets[a].Bottom + .01f,
                    "No overlap: " + widgets[a].Label.Split('\n')[0] + " / " + widgets[b].Label.Split('\n')[0]);
    }
}
