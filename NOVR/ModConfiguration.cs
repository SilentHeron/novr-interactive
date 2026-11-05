using System;
using BepInEx.Configuration;

namespace NOVR;

public class ModConfiguration
{
    private static ModConfiguration? _instance;
    public static ModConfiguration Instance => _instance ??
        throw new InvalidOperationException("NOVR configuration has not finished initializing.");
    internal static ModConfiguration? Current => _instance;

    public readonly ConfigFile Config;
    internal readonly ConfigEntry<bool> DisableProtonXInput;
    internal readonly Rendering.FixedFoveationSettings FixedFoveation;
    public readonly MotionControls.MotionControlSettings MotionControls;
    public readonly ConfigEntry<float> TargetDesignatorOvershoot;
    public readonly ConfigEntry<bool> EnableNativeMenuUi;
    public readonly ConfigEntry<float> NativeMenuScale;
    public readonly ConfigEntry<float> NativeMenuDistance;
    public readonly ConfigEntry<float> NativeMenuHeightOffset;
    public readonly ConfigEntry<float> HmdFollowResponse;
    public readonly MotionControls.ControllerBindings ControllerBindings;
    internal readonly VrUi.SpecialBehavior.CockpitAlertSettings CockpitAlerts;
    internal readonly ConfigEntry<MotionControls.TargetCueMode> TargetCue;
    internal readonly MotionControls.GazeCueSettings LookCue;
    public readonly ConfigEntry<float> HudPanelScale, HudPanelDistance;
    public readonly ConfigEntry<bool> HudContrast, HudDeclutter, FocusedFlightHud;
    public readonly ConfigEntry<float> FlightHudScale, InstrumentBrightness;
    public readonly ConfigEntry<float> MarkerScale, MarkerLabelScale, ReticleScale, WeaponHudScale;
    public readonly ConfigEntry<bool> SeatClearanceEnabled;
    public readonly ConfigEntry<float> SeatClearanceRadius;

    public ModConfiguration(ConfigFile config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        DisableProtonXInput = config.Bind("Compatibility", "Disable Legacy XInput on Proton", true,
            "Avoid a Proton XInput controller-polling deadlock. Applies only under Wine/Proton and requires a restart. OpenXR motion controls are unaffected. Xbox-style gamepads may need alternate bindings; disable this option if legacy XInput is required.");
        FixedFoveation = new Rendering.FixedFoveationSettings(config);
        MotionControls = new MotionControls.MotionControlSettings(config);
        ControllerBindings = new MotionControls.ControllerBindings(config);
        CockpitAlerts = new VrUi.SpecialBehavior.CockpitAlertSettings(config);
        SeatClearanceEnabled = config.Bind("Comfort", "Head Clearance", true,
            "Clear an identifiable opaque seat around your head. Glass and unsupported cockpits stay unchanged.");
        SeatClearanceRadius = config.Bind("Comfort", "Head Clearance Radius", Comfort.SeatClearanceRange.Default,
            new ConfigDescription("Local head-clearance radius in meters. Applies only when Head Clearance is enabled.",
                new AcceptableValueRange<float>(Comfort.SeatClearanceRange.Minimum, Comfort.SeatClearanceRange.Maximum)));
        TargetCue = config.Bind("VR HUD", "Target Aim Cue", global::NOVR.MotionControls.TargetCueMode.Context,
            "Context shows the look-direction cue only near a selectable target that is not already in your target list. Pressing Select over empty space does not show it. Always keeps it visible during cockpit flight; Off hides it. Brightness follows the HUD instruments. Target diamonds and weapon sights are unchanged.");
        LookCue = new MotionControls.GazeCueSettings(config);
        InstrumentBrightness = config.Bind("VR HUD", "Instrument Brightness", 1f,
            new ConfigDescription("Brightness of flight instruments, compass and pitch ladder only. Target/flight-path markers and missile warnings retain their native brightness.", new AcceptableValueRange<float>(.35f, 1f)));
        FocusedFlightHud = config.Bind("VR HUD", "Focused Flight HUD", true, "Compact flight instruments and a limited forward pitch ladder. Target and missile-warning projections remain independent.");
        FlightHudScale = config.Bind("VR HUD", "Flight HUD Scale", .38f, new ConfigDescription("Size of the focused flight instruments and pitch-ladder window.", new AcceptableValueRange<float>(.22f, .75f)));
        MarkerScale = config.Bind("VR HUD", "Marker Scale", .45f, new ConfigDescription("Size of target diamonds and location symbols, preserving their world alignment.", new AcceptableValueRange<float>(.2f, 1f)));
        MarkerLabelScale = config.Bind("VR HUD", "Marker Label Scale", .55f, new ConfigDescription("Size of target, location and aiming labels.", new AcceptableValueRange<float>(.2f, 1f)));
        ReticleScale = config.Bind("VR HUD", "Reticle Scale", .45f, new ConfigDescription("Crosshair and bombing-pipper size; connecting lines retain their aim geometry.", new AcceptableValueRange<float>(.2f, 1f)));
        WeaponHudScale = config.Bind("VR HUD", "Weapon Readout Scale", .5f, new ConfigDescription("Compact weapon-range and guidance readouts. Does not alter weapon limits or lock state.", new AcceptableValueRange<float>(.2f, 1f)));
        HudPanelScale = config.Bind("VR HUD", "Status Panel Scale", 1.15f,
            new ConfigDescription("Size of cockpit status panels; aiming markers keep their angular alignment.", new AcceptableValueRange<float>(0.8f, 1.6f)));
        HudPanelDistance = config.Bind("VR HUD", "Status Panel Distance", 4f,
            new ConfigDescription("Status panel depth in meters. Angular size stays constant as depth changes.", new AcceptableValueRange<float>(2f, 10f)));
        HudContrast = config.Bind("VR HUD", "Status Panel Backplates", true, "Dark backplates behind status panels for readability over bright terrain.");
        HudDeclutter = config.Bind("VR HUD", "Declutter Status Panels", false, "Fade the secondary lower-left status panel. Weapon, countermeasure and aiming information remain visible.");
        HmdFollowResponse = config.Bind("VR HUD", "Head Follow Response", 10f,
            new ConfigDescription("Response of head-following status text. Higher values follow faster; 0 locks directly to the head. World target markers stay aligned independently.",
                new AcceptableValueRange<float>(0f, 30f)));
        TargetDesignatorOvershoot = config.Bind(
            "General",
            "Target Designator Overshoot",
            1.2f,
            "How much the target designator should multiply rotation to make for easier high off boresight target designation. Set to 1.0 to disable");

        EnableNativeMenuUi = config.Bind(
            "Experimental",
            "Enable Native Menu UI",
            true,
            "Use NOVR's native VR menu UI for non-flight menus. Disable to fall back to the existing patched game UI.");

        NativeMenuScale = config.Bind(
            "Experimental",
            "Native Menu Scale",
            1.25f,
            "Size multiplier for NOVR's native VR menu UI. Values from 0.75 to 2.0 are supported.");

        NativeMenuDistance = config.Bind(
            "Experimental",
            "Native Menu Distance",
            3.0f,
            "Distance in meters from the headset when NOVR's native VR menu UI is opened or recentered. Values from 1.5 to 6.0 are supported.");

        NativeMenuHeightOffset = config.Bind(
            "Experimental",
            "Native Menu Height Offset",
            0.0f,
            "Vertical offset in meters applied when NOVR's native VR menu UI is opened or recentered. Values from -0.25 to 1.0 are supported.");

        // Binding can invoke callbacks or fail. Only expose a complete settings object.
        _instance = this;
    }
}
