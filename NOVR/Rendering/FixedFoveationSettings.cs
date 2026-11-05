using BepInEx.Configuration;

namespace NOVR.Rendering;

internal sealed class FixedFoveationSettings
{
    internal readonly ConfigEntry<FixedFoveationLevel> Level;
    internal readonly ConfigEntry<bool> MeasureGpuCost;
    internal readonly ConfigEntry<bool> UseEyeTracking;

    internal FixedFoveationSettings(ConfigFile config)
    {
        Level = config.Bind("VR Performance", "Fixed Foveated Rendering", FixedFoveationLevel.Off,
            "Experimental PC scene rendering: Off, Mild, Balanced, Strong, Extreme or Maximum. Extreme/Maximum are aggressive test levels with smaller protected regions and up to 4x4 outer shading (2x2 with MSAA). Requires native Windows, Direct3D11 and compatible NVIDIA VRS hardware. Unsupported platforms remain inactive. Does not change headset resolution, antialiasing, graphics backend or streaming settings.");
        MeasureGpuCost = config.Bind("VR Performance", "Measure Foveation GPU Cost", false,
            "Optional asynchronous GPU timing and pixel-shader counters for comparing Off with fixed-foveation modes. Measures the outside-world opaque pass, not total frame time. Disable after testing.");
        UseEyeTracking = config.Bind("VR Performance", "Eye Tracked Foveation", false,
            "Move the protected shading region with valid OpenXR eye gaze on supported Windows/NVIDIA/D3D11 systems. Falls back to the fixed centre if gaze is unavailable, untracked or stale; Extreme/Maximum also use Strong's larger protected region during fallback. Does not change targeting or SteamVR settings. Requires a headset/runtime exposing eye gaze, such as Steam Frame.");
    }

    // Invalid persisted enum values must never opt a user into the experiment.
    internal FixedFoveationLevel RequestedLevel => FixedFoveationPolicy.Normalize((int)Level.Value);
}
