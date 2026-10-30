using BepInEx.Configuration;

namespace NOVR.MotionControls;

internal sealed class InspectionZoomSettings
{
    internal static readonly string[] ResolutionLabels = { "1024×640", "1280×800", "1536×960", "2048×1280" };
    internal static readonly string[] AaLabels = { "Off", "2× MSAA", "4× MSAA", "8× MSAA" };
    private static readonly int[] Widths = { 1024, 1280, 1536, 2048 };
    internal readonly ConfigEntry<int> Resolution, AntiAliasing;
    internal readonly ConfigEntry<bool> SceneEffects;
    internal int Width => Widths[Resolution.Value];
    internal int Height => Width * 5 / 8;
    internal int Samples => 1 << AntiAliasing.Value;

    internal InspectionZoomSettings(ConfigFile config)
    {
        const string section = "Inspection Zoom";
        Resolution = config.Bind(section, "Resolution", 1, new ConfigDescription(
            "Right-bumper magnifier resolution only: 0=1024×640, 1=1280×800, 2=1536×960, 3=2048×1280. Higher values cost more GPU time while held.",
            new AcceptableValueRange<int>(0, 3)));
        AntiAliasing = config.Bind(section, "Anti-Aliasing", 1, new ConfigDescription(
            "Magnifier edge smoothing only: 0=Off, 1=2×, 2=4×, 3=8× MSAA. Requires the game's main Anti-Aliasing setting to be enabled; unsupported sample counts fall back automatically. Does not change headset MSAA.",
            new AcceptableValueRange<int>(0, 3)));
        SceneEffects = config.Bind(section, "Scene Effects", false,
            "Apply the game's volume post-processing to the magnifier. Off reduces the extra camera's cost. Does not change the main view or targeting HUD.");
    }

    internal void Reset() { Resolution.Value = 1; AntiAliasing.Value = 1; SceneEffects.Value = false; }
}
