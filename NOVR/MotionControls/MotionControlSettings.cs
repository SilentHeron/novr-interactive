using BepInEx.Configuration;
using UnityEngine;

namespace NOVR.MotionControls;

public sealed class MotionControlSettings
{
    internal const float MinimumStickAngle = 5f, MaximumStickAngle = 60f, DefaultStickAngle = 25f;
    internal const float DefaultYawDeadzone = .15f;
    public readonly ConfigEntry<bool> Enabled;
    public readonly ConfigEntry<bool> LeftHanded, ViewMenuFollowsHand;
    public readonly ConfigEntry<Vector3> StickPosition;
    public readonly ConfigEntry<Vector3> ThrottlePosition;
    public readonly ConfigEntry<float> StickAngle;
    public readonly ConfigEntry<float> Deadzone, YawDeadzone;
    public readonly ConfigEntry<float> ResponseExponent;
    public readonly ConfigEntry<float> ThrottleTravel;
    public readonly ConfigEntry<float> GrabRadius;
    public readonly ConfigEntry<bool> ThumbstickYaw, TwistYaw;
    public readonly ConfigEntry<float> YawAngle, HapticStrength, InteractionVolume, SleeveThickness;
    public readonly ConfigEntry<bool> MilDetent, HelmetNightVision, RiggedGloves, PilotArms;
    public readonly ConfigEntry<Vector3> GloveOffset, GloveRotation;
    public readonly ConfigEntry<Vector3> LeftGloveOffset, RightGloveOffset, LeftGloveRotation, RightGloveRotation;
    public readonly ConfigEntry<bool> StickTapGrip, ThrottleTapGrip;

    public MotionControlSettings(ConfigFile config)
    {
        const string section = "Motion Controls";
        Enabled = config.Bind(section, "Enabled", false, "Experimental virtual stick and throttle. F7 toggles in flight. Requires tracked OpenXR controllers.");
        ViewMenuFollowsHand = config.Bind(section, "View Menu Follows Hand", false, "Keep the View menu steady in front of the pilot while open. Enable to follow the throttle hand instead. Either controller can point at the menu.");
        LeftHanded = config.Bind(section, "Left Handed", false, "Use the left hand for the stick and the right for the throttle. Mirror both control models and their saved positions; keep the seat in place.");
        StickPosition = config.Bind(section, "Stick Position", new Vector3(0.22f, -0.36f, 0.32f), "Neutral grip position in cockpit meters, relative to the recentered eye position. F8 places controls at your hands.");
        ThrottlePosition = config.Bind(section, "Throttle Position", new Vector3(-0.24f, -0.36f, 0.32f), "Center of throttle travel in cockpit meters. F8 places controls at your hands.");
        StickAngle = Float(config, "Stick Angle", DefaultStickAngle, MinimumStickAngle, MaximumStickAngle, "Degrees of wrist tilt for full pitch or roll. Lower angles increase motion-stick sensitivity; yaw is unchanged. Adjust the slider in View > Flight controls or Settings > Controls.");
        Deadzone = Float(config, "Stick Deadzone", 0.06f, 0f, 0.4f, "Fraction of pitch/roll travel ignored around center. Stick twist has a separate yaw deadzone.");
        YawDeadzone = Float(config, "Stick Yaw Deadzone", DefaultYawDeadzone, 0f, .4f, "Fraction of physical stick twist ignored around neutral; 15% is 4.5 degrees at the default 30-degree yaw range. Does not change pitch/roll or thumbstick yaw.");
        ResponseExponent = Float(config, "Stick Response", 1.5f, 1f, 3f, "Response curve; higher values make small movements less sensitive.");
        ThrottleTravel = Float(config, "Throttle Travel", 0.12f, 0.05f, 0.6f, "Total meters from minimum to maximum, including the MIL shelf: lift the collective in helicopters; slide the throttle forward in other aircraft.");
        RiggedGloves = config.Bind(section, "Rigged Gloves", true, "Show rigged gloves using the installed pilot material. Grip curls three fingers and thumb; trigger curls the index. Uses controller input, not optical finger tracking.");
        SleeveThickness = Float(config, "Pilot Sleeve Thickness", .82f, .6f, 1.2f, "Visual sleeve width relative to the native pilot. Changes thickness around the arm bones, preserving length, reach and controller positions.");
        PilotArms = config.Bind(section, "Pilot Sleeves", true, "Render-only two-bone arm posing using installed pilot sleeve geometry. Sleeves gently extend and fade near their reach limit; tracked gloves remain visible.");
        GloveOffset = config.Bind(section, "Glove Wrist Offset", new Vector3(0,-.015f,-.055f), "Baseline free-hand wrist offset from the runtime grip pose in meters. X mirrors for the right hand. Per-hand fit corrections are added to this baseline. Visible fingertips and physical cockpit touch points stay aligned; held grips, pointers and flight inputs are unchanged.");
        GloveRotation = config.Bind(section, "Glove Wrist Rotation", new Vector3(0,0,90), "Baseline free-hand glove rotation relative to the controller's forward aim in degrees; Y/Z mirror for the right hand. Per-hand fit corrections are added to this baseline. Direct touch follows the glove; held grips, pointers and flight inputs are unchanged.");
        LeftGloveOffset = config.Bind(section, "Left Hand Fit Position", Vector3.zero, "Independent left free-hand correction in controller-local meters, added to the legacy wrist offset. Zero preserves the existing fit. Adjust in View > Hands & arms > Fit left hand.");
        RightGloveOffset = config.Bind(section, "Right Hand Fit Position", Vector3.zero, "Independent right free-hand correction in controller-local meters, added after the legacy wrist offset is mirrored. Zero preserves the existing fit.");
        LeftGloveRotation = config.Bind(section, "Left Hand Fit Rotation", Vector3.zero, "Independent left free-hand angle correction in degrees, added to the legacy wrist angles. Zero preserves the existing fit.");
        RightGloveRotation = config.Bind(section, "Right Hand Fit Rotation", Vector3.zero, "Independent right free-hand angle correction in degrees, added after the legacy wrist angles are mirrored. Zero preserves the existing fit.");
        HelmetNightVision = config.Bind(section, "Helmet Night Vision Gesture", true, "Raise either free hand to the forehead/brow and make a fresh trigger press to toggle native night vision. A short pulse marks the gesture area. Disabled in menus, map and layout editing.");
        MilDetent = config.Bind(section, "MIL Throttle Detent", true, "Afterburning jets only: a 1.6 cm shelf at the installed engine's military-power boundary. Push through for afterburner; no timer or extra button.");
        InteractionVolume = Float(config, "Interaction Sound Volume", .4f, 0f, 1f, "Volume of spatial button, grip, detent, guard and helmet cues. Zero mutes them without disabling haptics. Uses the aircraft audio mixer when available.");
        HapticStrength = Float(config, "Interaction Haptic Strength", 1.5f, 0f, 2f, "Strength of brief handle-reach, grab, release, widget, detent and stick-limit feedback. Zero disables these cues.");
        var hapticsUpgrade = config.Bind(section, "Stronger Haptics Applied", false, "Migrates the former stock strength once; preserves custom levels and disabled haptics.");
        if (!hapticsUpgrade.Value)
        {
            if (Mathf.Abs(HapticStrength.Value - 1f) < .0001f) HapticStrength.Value = 1.5f;
            hapticsUpgrade.Value = true;
        }
        var audioUpgrade = config.Bind(section, "Mechanical Audio Defaults Applied", false, "Migrates the former stock cue volume once; preserves custom levels and muted audio.");
        if (!audioUpgrade.Value)
        {
            if (Mathf.Abs(InteractionVolume.Value - .25f) < .0001f) InteractionVolume.Value = .4f;
            audioUpgrade.Value = true;
        }
        var comfortUpgrade = config.Bind(section, "Frame Comfort Defaults Applied", false, "Tracks the one-time shorter default throttle travel and neutral-wrist visual adjustment.");
        if (!comfortUpgrade.Value)
        {
            if (Mathf.Abs(ThrottleTravel.Value - .25f) < .0001f) ThrottleTravel.Value = .22f;
            if (GloveRotation.Value == Vector3.zero) GloveRotation.Value = new Vector3(25,0,0);
            comfortUpgrade.Value = true;
        }
        var compactUpgrade = config.Bind(section, "Compact Travel Defaults Applied", false, "Migrates the former stock 22/25 cm travel to 12 cm once, preserving customized travel.");
        if (!compactUpgrade.Value)
        {
            if (Mathf.Abs(ThrottleTravel.Value - .22f) < .0001f || Mathf.Abs(ThrottleTravel.Value - .25f) < .0001f) ThrottleTravel.Value = .12f;
            compactUpgrade.Value = true;
        }
        var uprightUpgrade = config.Bind(section, "Upright Glove Defaults Applied", false, "Migrates former stock wrist angles to palms vertical, thumbs up; preserves custom alignment.");
        if (!uprightUpgrade.Value)
        {
            if (GloveRotation.Value == Vector3.zero || GloveRotation.Value == new Vector3(25,0,0)) GloveRotation.Value = new Vector3(0,0,90);
            uprightUpgrade.Value = true;
        }
        GrabRadius = Float(config, "Grab Radius", 0.12f, 0.03f, 0.3f, "Maximum hand distance from a handle when pressing grip.");
        StickTapGrip = config.Bind(section, "Stick Tap Grip", false, "Toggle grip: squeeze once to hold the stick, squeeze again to release. Off requires continuous grip pressure. Moving more than 40 cm away, tracking loss or menus releases the handle.");
        ThrottleTapGrip = config.Bind(section, "Throttle Tap Grip", false, "Toggle grip for the throttle/collective: squeeze once to hold, squeeze again to release. Off requires continuous pressure. The saved legacy key is retained.");
        TwistYaw = config.Bind(section, "Stick Twist Yaw", true, "Twist the held or latched stick left/right for native rudder and nosewheel steering. The orientation when grabbed is neutral. Disable both yaw sources to keep pedals in control.");
        YawAngle = Float(config, "Stick Yaw Angle", 30f, 5f, 60f, "Degrees of stick twist for full rudder/nosewheel input. Uses the separate yaw deadzone and the stick response curve.");
        ThumbstickYaw = config.Bind(section, "Thumbstick Yaw", true, "Also allow the horizontal stick-hand thumbstick for yaw while holding the virtual stick. Adds to twist yaw and clamps to full input. Disable both yaw sources to keep pedals in control.");
    }

    // The legacy pair remains the baseline, so adding independent calibration
    // never rewrites an existing user's carefully tuned fit or the opposite hand.
    internal Vector3 FreeHandOffset(bool left)
    {
        var baseline = GloveOffset.Value; if (!left) baseline.x = -baseline.x;
        return baseline + SafeCorrection((left ? LeftGloveOffset : RightGloveOffset).Value, .15f);
    }
    internal Vector3 FreeHandAngles(bool left)
    {
        var baseline = GloveRotation.Value;
        if (!left) { baseline.y = -baseline.y; baseline.z = -baseline.z; }
        return baseline + SafeCorrection((left ? LeftGloveRotation : RightGloveRotation).Value, 180f);
    }
    internal void MoveFreeHand(bool left, Vector3 amount)
    {
        var entry = left ? LeftGloveOffset : RightGloveOffset;
        entry.Value = SafeCorrection(entry.Value + amount, .15f);
    }
    internal void TurnFreeHand(bool left, Vector3 amount)
    {
        var entry = left ? LeftGloveRotation : RightGloveRotation;
        entry.Value = SafeCorrection(entry.Value + amount, 180f);
    }
    internal void ResetFreeHand(bool left)
    {
        (left ? LeftGloveOffset : RightGloveOffset).Value = Vector3.zero;
        (left ? LeftGloveRotation : RightGloveRotation).Value = Vector3.zero;
    }
    internal void SetFreeHandPalm(bool left, bool upright)
    {
        var entry = left ? LeftGloveRotation : RightGloveRotation;
        var baseline = GloveRotation.Value;
        if (!left) { baseline.y = -baseline.y; baseline.z = -baseline.z; }
        var desired = upright ? new Vector3(0, 0, left ? 90 : -90) : Vector3.zero;
        entry.Value = SafeCorrection(desired - baseline, 180f);
    }
    private static Vector3 SafeCorrection(Vector3 value, float limit) => new(
        HandCalibrationMath.Correction(value.x, limit), HandCalibrationMath.Correction(value.y, limit), HandCalibrationMath.Correction(value.z, limit));

    private static ConfigEntry<float> Float(ConfigFile config, string name, float value, float min, float max, string description) =>
        config.Bind("Motion Controls", name, value, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));
}
