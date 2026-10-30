using System.ComponentModel;
using BepInEx.Configuration;
using UnityEngine;

namespace NOVR;

public class ModConfiguration
{
    public static ModConfiguration Instance;
    

    public readonly ConfigFile Config;
    internal readonly ConfigEntry<bool> DisableProtonXInput;
    public readonly MotionControls.MotionControlSettings MotionControls;
    public readonly ConfigEntry<float> TargetDesignatorOvershoot;
    public readonly ConfigEntry<bool> EnableNativeMenuUi;
    public readonly ConfigEntry<float> NativeMenuScale;
    public readonly ConfigEntry<float> NativeMenuDistance;
    public readonly ConfigEntry<float> NativeMenuHeightOffset;
    public readonly ConfigEntry<float> HmdFollowResponse;
    public readonly MotionControls.ControllerBindings ControllerBindings;
    internal readonly MotionControls.OpticalHudSettings OpticalHud;
    internal readonly MotionControls.InspectionZoomSettings InspectionZoom;
    internal readonly VrUi.SpecialBehavior.CockpitAlertSettings CockpitAlerts;
    internal readonly ConfigEntry<MotionControls.TargetCueMode> TargetCue;
    public readonly ConfigEntry<float> HudPanelScale, HudPanelDistance;
    public readonly ConfigEntry<bool> HudContrast, HudDeclutter, FocusedFlightHud;
    public readonly ConfigEntry<float> FlightHudScale, InstrumentBrightness, MfdBrightness;
    public readonly ConfigEntry<float> MarkerScale, MarkerLabelScale, ReticleScale, WeaponHudScale;
    internal readonly ConfigEntry<MotionControls.MfdPage> LeftMfdPage, RightMfdPage, Fs20LeftMfdPage, Fs20RightMfdPage;
    public readonly ConfigEntry<bool> CockpitPanelEnabled, Fs20NativeCockpitEnabled;
    internal readonly ConfigEntry<bool> CockpitMapNorthUp;
    public readonly ConfigEntry<Vector3> CockpitPanelPosition;
    public readonly ConfigEntry<float> CockpitPanelTilt, CockpitPanelScale, CockpitPanelYaw, CockpitPanelRoll;

    public ModConfiguration(ConfigFile config)
    {
        Instance = this;

        Config = config;
        DisableProtonXInput = config.Bind("Compatibility", "Disable Legacy XInput on Proton", true,
            "Avoid a Proton XInput controller-polling deadlock. Applies only under Wine/Proton and requires a restart. OpenXR motion controls are unaffected. Xbox-style gamepads may need alternate bindings; disable this option if legacy XInput is required.");
        MotionControls = new MotionControls.MotionControlSettings(config);
        ControllerBindings = new MotionControls.ControllerBindings(config);
        OpticalHud = new MotionControls.OpticalHudSettings(config);
        InspectionZoom = new MotionControls.InspectionZoomSettings(config);
        CockpitAlerts = new VrUi.SpecialBehavior.CockpitAlertSettings(config);
        TargetCue = config.Bind("VR HUD", "Target Aim Cue", global::NOVR.MotionControls.TargetCueMode.Context,
            "Context shows a translucent HUD-colored hollow gaze circle only near a selectable target or while selecting. Always keeps the circle visible and brightens it during acquisition. Its brightness follows the HUD instruments. Off hides the aim cue. Target diamonds and weapon sights are unchanged.");
        Fs20NativeCockpitEnabled = config.Bind("FS-20 Native Cockpit", "Interactive Controls", true, "Operate the FS-20 original cockpit controls in VR. Its native cluster is fixed to the aircraft and always replaces the universal movable panel. Disabling this leaves the original cockpit visible.");
        CockpitPanelEnabled = config.Bind("Universal Cockpit Panel", "Enabled", true, "Show the shared 3D control panel and dock weapon status and minimap to its displays.");
        CockpitPanelPosition = config.Bind("Universal Cockpit Panel", "Position", new Vector3(0, -0.32f, 0.65f), "Fallback cockpit position if a native dashboard surface is unavailable. Automatic dashboard fit and per-aircraft saved placements take precedence. Independent of seat adjustment.");
        CockpitPanelTilt = config.Bind("Universal Cockpit Panel", "Tilt", 20f, new ConfigDescription("Upward-facing panel tilt in degrees.", new AcceptableValueRange<float>(-90, 90)));
        CockpitPanelYaw = config.Bind("Universal Cockpit Panel", "Yaw", 0f, new ConfigDescription("Panel yaw in degrees, set by grabbing it in Edit layout.", new AcceptableValueRange<float>(-180, 180)));
        CockpitPanelRoll = config.Bind("Universal Cockpit Panel", "Roll", 0f, new ConfigDescription("Panel roll in degrees, set by grabbing it in Edit layout.", new AcceptableValueRange<float>(-180, 180)));
        CockpitPanelScale = config.Bind("Universal Cockpit Panel", "Scale", 1f, new ConfigDescription("Automatic dashboard-fit size multiplier, also used for fallback placement. Manual panel sizes are saved per aircraft.", new AcceptableValueRange<float>(0.65f, 1.4f)));
        LeftMfdPage = config.Bind("Universal Cockpit Panel", "Left MFD page", global::NOVR.MotionControls.MfdPage.Weapons, "Page on the left display.");
        RightMfdPage = config.Bind("Universal Cockpit Panel", "Right MFD page", global::NOVR.MotionControls.MfdPage.Map, "Page on the right display. Selecting an occupied page swaps the displays.");
        CockpitMapNorthUp = config.Bind("Cockpit Displays", "Map North Up", false, "Keep north at the top of a docked cockpit map. Off uses aircraft heading. The FS-20 map orientation key changes this without opening a menu.");
        Fs20LeftMfdPage = config.Bind("FS-20 Cockpit", "Left MFD page", global::NOVR.MotionControls.MfdPage.Map, "Integrated left display; defaults to the live minimap.");
        Fs20RightMfdPage = config.Bind("FS-20 Cockpit", "Right MFD page", global::NOVR.MotionControls.MfdPage.Weapons, "Integrated right display. Selecting an occupied page swaps the displays.");
        InstrumentBrightness = config.Bind("VR HUD", "Instrument Brightness", 1f,
            new ConfigDescription("Brightness of flight instruments, compass and pitch ladder only. Target/flight-path markers and missile warnings retain their native brightness.", new AcceptableValueRange<float>(.35f, 1f)));
        MfdBrightness = config.Bind("Universal Cockpit Panel", "Display Brightness", 1f,
            new ConfigDescription("Brightness of MFD content, including native instrument/camera pages. Headers, page choices, weapon safety/readout and external warning indicators stay readable.", new AcceptableValueRange<float>(.35f, 1f)));
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
    }
}
