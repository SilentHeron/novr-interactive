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
    internal readonly Rendering.ImageQualitySettings ImageQuality;
    public readonly MotionControls.MotionControlSettings MotionControls;
    public readonly ConfigEntry<float> TargetDesignatorOvershoot;
    public readonly ConfigEntry<bool> EnableNativeMenuUi;
    public readonly ConfigEntry<float> NativeMenuScale;
    public readonly ConfigEntry<float> NativeMenuDistance;
    public readonly ConfigEntry<float> NativeMenuHeightOffset;
    public readonly ConfigEntry<float> HmdFollowResponse;
    internal readonly ConfigEntry<bool> MenuControllerModels, MenuControllerTooltips, SelectionTeleport;
    internal readonly ConfigEntry<float> SnapTurnDegrees;
    internal readonly ConfigEntry<bool> ControlsHideWidgets;
    internal readonly ConfigEntry<bool> MenuControllerPresses;
    public readonly MotionControls.ControllerBindings ControllerBindings;
    internal readonly VrUi.SpecialBehavior.CockpitAlertSettings CockpitAlerts;
    internal readonly ConfigEntry<MotionControls.TargetCueMode> TargetCue;
    internal readonly MotionControls.GazeCueSettings LookCue;
    public readonly ConfigEntry<float> HudPanelScale, HudPanelDistance;
    public readonly ConfigEntry<bool> HudContrast, HudDeclutter, FocusedFlightHud, SymbolsFollowFlightHud;
    public readonly ConfigEntry<float> FlightHudScale, InstrumentBrightness;
    public readonly ConfigEntry<float> MarkerScale, MarkerLabelScale, ReticleScale, WeaponHudScale;
    public readonly ConfigEntry<bool> SeatClearanceEnabled;
    public readonly ConfigEntry<float> SeatClearanceRadius;
    public readonly ConfigEntry<float> VrFarClipDistance;
    public readonly ConfigEntry<bool> HideDistantCloudsInVr;
    public readonly ConfigEntry<bool> VrGameAntialiasing;
    internal readonly ConfigEntry<bool> ShareEyeShadows;

    public ModConfiguration(ConfigFile config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        DisableProtonXInput = config.Bind("Compatibility", "Disable Legacy XInput on Proton", true,
            "Avoid a Proton XInput controller-polling deadlock. Applies only under Wine/Proton and requires a restart. OpenXR motion controls are unaffected. Xbox-style gamepads may need alternate bindings; disable this option if legacy XInput is required.");
        FixedFoveation = new Rendering.FixedFoveationSettings(config);
        ImageQuality = new Rendering.ImageQualitySettings(config);
        MotionControls = new MotionControls.MotionControlSettings(config);
        ControllerBindings = new MotionControls.ControllerBindings(config);
        CockpitAlerts = new VrUi.SpecialBehavior.CockpitAlertSettings(config);
        SeatClearanceEnabled = config.Bind("Comfort", "Head Clearance", true,
            "Clear an identifiable opaque seat around your head. Glass and unsupported cockpits stay unchanged.");
        SeatClearanceRadius = config.Bind("Comfort", "Head Clearance Radius", Comfort.SeatClearanceRange.Default,
            new ConfigDescription("Local head-clearance radius in meters. Applies only when Head Clearance is enabled.",
                new AcceptableValueRange<float>(Comfort.SeatClearanceRange.Minimum, Comfort.SeatClearanceRange.Maximum)));
        VrFarClipDistance = config.Bind("VR View", "Far Clip Distance", VrCamera.VrFarClipPolicy.Default,
            new ConfigDescription("Minimum far clip distance in metres for the tracked VR view. The game's camera stops at 80 km; at altitude that cut shows as a flat wall of fogged terrain that pivots with your head. Only extends the game's value; 0 keeps it.",
                new AcceptableValueRange<float>(0f, VrCamera.VrFarClipPolicy.Maximum)));
        // Renamed from "Game Anti-Aliasing" (on by default in a 0.4.8.33 test
        // build) so a saved true value from that build cannot keep it on.
        VrGameAntialiasing = config.Bind("VR View", "Game SMAA (Costly)", false,
            "Opt in: apply the game camera's post-process anti-aliasing (SMAA in the stock flight scene) to the VR view, on top of MSAA. It runs for each eye at full headset resolution and can cost a large share of the frame; NOVR renders without it by default.");
        ShareEyeShadows = config.Bind("VR Performance", "Share Shadows Between Eyes", false,
            "Experimental. Draw the sun's shadow map once per frame and reuse it for the second eye, instead of once per eye. Saves the second submission of every shadow caster. Turn off if shadows flicker, shift or disappear in one eye.");
        HideDistantCloudsInVr = config.Bind("VR View", "Hide Distant Cloud Banks", true,
            "Hide the game's ring of distant cloud meshes (10-40 km out, shown when weather coverage is above 10%, as on most multiplayer servers) while in VR, where it appears as a grey wall around the horizon. Nearby clouds and the cloud layer are unchanged.");
        TargetCue = config.Bind("VR HUD", "Target Aim Cue", global::NOVR.MotionControls.TargetCueMode.Context,
            "Context shows the look-direction cue only near a selectable target that is not already in your target list. Pressing Select over empty space does not show it. Always keeps it visible during cockpit flight; Off hides it. Brightness follows the HUD instruments. Target diamonds and weapon sights are unchanged.");
        LookCue = new MotionControls.GazeCueSettings(config);
        InstrumentBrightness = config.Bind("VR HUD", "Instrument Brightness", 1f,
            new ConfigDescription("Brightness of flight instruments, compass and pitch ladder only. Target/flight-path markers and missile warnings retain their native brightness.", new AcceptableValueRange<float>(.35f, 1f)));
        FocusedFlightHud = config.Bind("VR HUD", "Focused Flight HUD", true, "Compact flight instruments and a limited forward pitch ladder. Target and missile-warning projections remain independent.");
        FlightHudScale = config.Bind("VR HUD", "Flight HUD Scale", .38f, new ConfigDescription("Size of the focused flight instruments and pitch-ladder window.", new AcceptableValueRange<float>(.22f, .75f)));
        SymbolsFollowFlightHud = config.Bind("VR HUD", "Symbols Follow Flight HUD Scale", true,
            "Scale target/location symbols, marker labels and reticles with Flight HUD Scale, relative to its 38% default, so the whole HUD resizes together. Off sizes them from their own sliders only.");
        MarkerScale = config.Bind("VR HUD", "Marker Scale", .45f, new ConfigDescription("Size of target diamonds and location symbols, preserving their world alignment.", new AcceptableValueRange<float>(.2f, 1f)));
        MarkerLabelScale = config.Bind("VR HUD", "Marker Label Scale", .55f, new ConfigDescription("Size of target, location and aiming labels.", new AcceptableValueRange<float>(.2f, 1f)));
        ReticleScale = config.Bind("VR HUD", "Reticle Scale", .45f, new ConfigDescription("Crosshair and bombing-pipper size; connecting lines retain their aim geometry.", new AcceptableValueRange<float>(.2f, 1f)));
        WeaponHudScale = config.Bind("VR HUD", "Weapon Readout Scale", .5f, new ConfigDescription("Compact weapon-range and guidance readouts. Does not alter weapon limits or lock state.", new AcceptableValueRange<float>(.2f, 1f)));
        HudPanelScale = config.Bind("VR HUD", "Status Panel Scale", 1.15f,
            new ConfigDescription("Size of cockpit status panels; aiming markers keep their angular alignment.", new AcceptableValueRange<float>(0.8f, 1.6f)));
        HudPanelDistance = config.Bind("VR HUD", "Status Panel Distance", 4f,
            new ConfigDescription("Status panel depth in meters. Angular size stays constant as depth changes.", new AcceptableValueRange<float>(2f, 10f)));
        HudContrast = config.Bind("VR HUD", "Status Panel Backplates", true, "Dark backplates behind status panels for readability over bright terrain.");
        ControlsHideWidgets = config.Bind("VR HUD", "Stick And Throttle Hide Widgets Behind Them", true,
            "The cockpit map and other widgets are hidden where the virtual stick or throttle is in front of them, as they already are behind your gloves. Off: widgets draw over the controls.");
        HudDeclutter = config.Bind("VR HUD", "Declutter Status Panels", false, "Fade the secondary lower-left status panel. Weapon, countermeasure and aiming information remain visible.");
        MenuControllerModels = config.Bind("VR Menus", "Controller Models", true,
            "Show your controllers in the VR menus and at aircraft selection, using the model SteamVR installs for them (Steam Frame, Quest 2, Quest 3, Quest Pro). Nothing is shown if SteamVR's models are not found.");
        MenuControllerTooltips = config.Bind("VR Menus", "Controller Tooltips", true,
            "Label the buttons on the shown controllers with what they do on the current screen. Labels appear while a controller is raised toward your face.");
        MenuControllerPresses = config.Bind("VR Menus", "Controller Press Highlight", true,
            "On the shown controllers, a button turns white while you press it.");
        SelectionTeleport = config.Bind("VR Menus", "Walk Around Aircraft", true,
            "At aircraft selection, push the free hand's thumbstick forward to aim and release to teleport around the aircraft; pull it back to bring the menu to you. The left thumbstick snap-turns.");
        SnapTurnDegrees = config.Bind("VR Menus", "Snap Turn Degrees", 45f,
            new ConfigDescription("How far one flick of the left thumbstick turns you at aircraft selection.", new AcceptableValueRange<float>(15f, 90f)));
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
