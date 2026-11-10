using UnityEngine;

namespace NOVR.VrUi;

internal sealed partial class WristMenu
{
    // Image options that cost next to nothing to draw. All apply live.
    private GameObject BuildImageQualityPage()
    {
        var page = Page("IMAGE QUALITY");
        var parent = page.transform;
        var image = ModConfiguration.Instance.ImageQuality;
        Section(parent, "Picture", 214);
        Setting(parent, "Dither gradients", image.DitherGradients, 0, 167);
        Setting(parent, "No edge darkening", image.RemoveRestingVignette, 1, 167);
        Note(parent, "Dither: hides colour bands in sky, haze and night.\nEdge darkening: G-force still closes the view in.", 113, 52);
        Section(parent, "Cockpit", 62);
        Setting(parent, "Filter cockpit screens", image.FilterCockpitScreens, 0, 15);
        Setting(parent, "Smooth cockpit map", image.SmoothCockpitMap, 1, 15);
        Note(parent, "Stops screen text and map terrain from sparkling\nas your head moves. The map copy uses video memory.", -39, 52);
        Section(parent, "Scenery", -90);
        Setting(parent, "Hide distant cloud banks", () => GameplayPatches.DistantCloudBanks.HideInVr,
            GameplayPatches.DistantCloudBanks.SetHideInVr, 0, -137);
        Note(parent, "None of these costs frame rate.\nSwitch one off if it looks wrong in your headset.", -195, 52);
        return page;
    }
}
