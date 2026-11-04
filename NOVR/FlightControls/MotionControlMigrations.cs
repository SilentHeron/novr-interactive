using BepInEx.Configuration;
using UnityEngine;

namespace NOVR.MotionControls;

// Preserve the stored flags and historical order: the comfort migration feeds
// the later compact-travel and upright-wrist migrations. Custom values survive.
internal static class MotionControlMigrations
{
    private const string Section = "Motion Controls";
    internal static void Apply(ConfigFile config, MotionControlSettings settings)
    {
        UpgradeHaptics(config, settings);
        UpgradeAudio(config, settings);
        UpgradeFrameComfort(config, settings);
        UpgradeCompactTravel(config, settings);
        UpgradeUprightWrist(config, settings);
        UpgradeGrabReach(config, settings);
    }

    private static void UpgradeGrabReach(ConfigFile config, MotionControlSettings settings)
    {
        var applied = config.Bind(Section, "Wider Handle Reach Applied", false,
            "Migrates the former stock 12 cm handle grab radius to 15 cm once, preserving custom distances.");
        if (applied.Value)
            return;
        if (Mathf.Abs(settings.GrabRadius.Value - .12f) < .0001f)
            settings.GrabRadius.Value = MotionControlSettings.DefaultGrabRadius;
        applied.Value = true;
    }

    private static void UpgradeHaptics(ConfigFile config, MotionControlSettings settings)
    {
        var hapticsUpgrade = config.Bind(Section, "Stronger Haptics Applied", false, "Migrates the former stock strength once; preserves custom levels and disabled haptics.");
        if (!hapticsUpgrade.Value)
        {
            if (Mathf.Abs(settings.HapticStrength.Value - 1f) < .0001f)
                settings.HapticStrength.Value = 1.5f;
            hapticsUpgrade.Value = true;
        }
    }

    private static void UpgradeAudio(ConfigFile config, MotionControlSettings settings)
    {
        var audioUpgrade = config.Bind(Section, "Mechanical Audio Defaults Applied", false, "Migrates the former stock cue volume once; preserves custom levels and muted audio.");
        if (!audioUpgrade.Value)
        {
            if (Mathf.Abs(settings.InteractionVolume.Value - .25f) < .0001f)
                settings.InteractionVolume.Value = .4f;
            audioUpgrade.Value = true;
        }
    }

    private static void UpgradeFrameComfort(ConfigFile config, MotionControlSettings settings)
    {
        var comfortUpgrade = config.Bind(Section, "Frame Comfort Defaults Applied", false, "Tracks the one-time shorter default throttle travel and neutral-wrist visual adjustment.");
        if (!comfortUpgrade.Value)
        {
            if (Mathf.Abs(settings.ThrottleTravel.Value - .25f) < .0001f)
                settings.ThrottleTravel.Value = .22f;
            if (settings.GloveRotation.Value == Vector3.zero)
                settings.GloveRotation.Value = new Vector3(25, 0, 0);
            comfortUpgrade.Value = true;
        }
    }

    private static void UpgradeCompactTravel(ConfigFile config, MotionControlSettings settings)
    {
        var compactUpgrade = config.Bind(Section, "Compact Travel Defaults Applied", false, "Migrates the former stock 22/25 cm travel to 12 cm once, preserving customized travel.");
        if (!compactUpgrade.Value)
        {
            if (Mathf.Abs(settings.ThrottleTravel.Value - .22f) < .0001f || Mathf.Abs(settings.ThrottleTravel.Value - .25f) < .0001f)
                settings.ThrottleTravel.Value = .12f;
            compactUpgrade.Value = true;
        }
    }

    private static void UpgradeUprightWrist(ConfigFile config, MotionControlSettings settings)
    {
        var uprightUpgrade = config.Bind(Section, "Upright Glove Defaults Applied", false, "Migrates former stock wrist angles to palms vertical, thumbs up; preserves custom alignment.");
        if (!uprightUpgrade.Value)
        {
            if (settings.GloveRotation.Value == Vector3.zero || settings.GloveRotation.Value == new Vector3(25, 0, 0))
                settings.GloveRotation.Value = new Vector3(0, 0, 90);
            uprightUpgrade.Value = true;
        }
    }
}
