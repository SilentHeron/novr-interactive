using BepInEx.Configuration;

namespace NOVR.MotionControls;

internal sealed class OpticalHudSettings
{
    internal readonly ConfigEntry<bool> Enabled;
    internal readonly ConfigEntry<float> Width, Magnification;
    internal OpticalHudSettings(ConfigFile config)
    {
        const string section = "Helmet Optical Targeting";
        Enabled = config.Bind(section, "Enabled", false,
            "Head-directed optical camera HUD. Enable through View > EOTS HUD. Select a target to track; unlock to aim with your head again. Uses headset direction, not eye tracking.");
        Width = config.Bind(section, "Angular Width", 20f, new ConfigDescription(
            "Width of the borderless helmet optical image in degrees. Stereo depth stays at 3 meters.", new AcceptableValueRange<float>(12, 26)));
        Magnification = config.Bind(section, "Head Aim Magnification", 2f, new ConfigDescription(
            "Optical magnification in HEAD mode. TRACK uses the aircraft camera's automatic framing.", new AcceptableValueRange<float>(1, 6)));
    }
}
