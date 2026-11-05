using BepInEx.Configuration;

namespace NOVR.VrUi.SpecialBehavior;

internal sealed class CockpitAlertSettings
{
    internal readonly ConfigEntry<bool> Enabled, Feed, WorldNotch;
    internal readonly ConfigEntry<float> Scale, Height;
    internal CockpitAlertSettings(ConfigFile config)
    {
        const string section = "Cockpit Alerts";
        Enabled = config.Bind(section, "Compact Overlay", true,
            "Frameless cockpit-fixed threat scope and compact notifications. Radar/missile detection, map indications and warning audio remain native.");
        Feed = config.Bind(section, "Show Kill Feed", true, "Show the latest three native kill-feed entries in Notifications. Mission messages and rewards remain visible; native kill-feed filters still apply.");
        WorldNotch = config.Bind(section, "World Notch Cues", true,
            "Show the native radar-missile evasion lines in the VR HUD alongside the compact alerts. These cues apply to ARH/SARH threats, not infrared missiles.");
        Scale = config.Bind(section, "Scale", 1f, new ConfigDescription("Size of compact threats and notifications.", new AcceptableValueRange<float>(.7f, 1.3f)));
        Height = config.Bind(section, "Height", -.30f, new ConfigDescription("Overlay height relative to the original cockpit viewpoint in meters. Seat adjustments do not move it.", new AcceptableValueRange<float>(-.65f, -.12f)));
    }
}
