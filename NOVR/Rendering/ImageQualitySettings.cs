using BepInEx.Configuration;

namespace NOVR.Rendering;

// Image fixes that cost next to nothing to draw. Each is on by default and can
// be switched off on its own, in case one looks wrong on a particular headset.
internal sealed class ImageQualitySettings
{
    private const string Section = "VR Image";
    internal readonly ConfigEntry<bool> DitherGradients, RemoveRestingVignette, FilterCockpitScreens, SmoothCockpitMap;

    internal ImageQualitySettings(ConfigFile config)
    {
        DitherGradients = config.Bind(Section, "Dither Gradients", true,
            "Add the render pipeline's fine dithering noise to the VR image. The headset image has 8 bits per colour channel, and the game dithers none of its cameras, so smooth sky, haze and night gradients show bands. Adds one texture lookup to a pass that already runs.");
        RemoveRestingVignette = config.Bind(Section, "Remove Resting Vignette", true,
            "The game keeps the edges of the view darkened at all times and darkens them further as G-force builds. In VR, start from no darkening instead: G effects still close the view in completely. Night vision keeps its own goggle edge.");
        FilterCockpitScreens = config.Bind(Section, "Filter Cockpit Screens", true,
            "Give the cockpit's tactical screen mipmaps and anisotropic filtering while in VR, and the target window anisotropic filtering. The game ships both without, so their text and lines crawl as your head moves. The tactical screen gives up its 2x multisampling for this: a multisampled texture cannot have mipmaps.");
        SmoothCockpitMap = config.Bind(Section, "Smooth Cockpit Map", true,
            "Draw the cockpit map's terrain from a half-size copy with mipmaps whenever the map is zoomed out far enough to sparkle. The copy is made once per map and uses about 21 MB of video memory (43 MB for the largest map).");
    }
}
