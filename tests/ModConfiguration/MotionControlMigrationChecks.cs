using BepInEx.Configuration;
using NOVR.MotionControls;
using UnityEngine;

internal static class MotionControlMigrationChecks
{
    private const string Section = "Motion Controls";
    private static readonly string[] Flags =
    {
        "Stronger Haptics Applied", "Mechanical Audio Defaults Applied", "Frame Comfort Defaults Applied",
        "Compact Travel Defaults Applied", "Upright Glove Defaults Applied"
    };

    internal static void Run(Action<bool, string> check)
    {
        foreach (var travel in new[] { .25f, .22f, .12f })
            foreach (var wrist in new[] { Vector3.zero, new Vector3(25, 0, 0), new Vector3(0, 0, 90) })
            {
                var config = Stored(1, .25f, travel, wrist);
                var settings = new MotionControlSettings(config);
                check(settings.HapticStrength.Value == 1.5f && settings.InteractionVolume.Value == .4f,
                    "Historical default feedback levels migrate to the same current values.");
                check(settings.ThrottleTravel.Value == .12f && settings.GloveRotation.Value == new Vector3(0, 0, 90),
                    "Ordered travel and wrist migrations reach the current defaults from every previous stock state.");
                check(config.BoundKeys.Where(Flags.Contains).SequenceEqual(Flags),
                    "Historical migration flags are bound and applied in their original order.");
                foreach (var key in Flags)
                    check(config.Bind(Section, key, false, "").Value, "Each historical flag keeps its persisted key.");
                settings.HapticStrength.Value = 1;
                settings.InteractionVolume.Value = .25f;
                settings.ThrottleTravel.Value = .25f;
                settings.GloveRotation.Value = Vector3.zero;
                var reloaded = new MotionControlSettings(config);
                check(reloaded.HapticStrength.Value == 1 && reloaded.InteractionVolume.Value == .25f &&
                    reloaded.ThrottleTravel.Value == .25f && reloaded.GloveRotation.Value == Vector3.zero,
                    "Already-applied migrations never overwrite later user changes, even when they match old defaults.");
            }
        foreach (var level in new[] { 0f, .6f, 1.0002f, 2f })
        {
            var customWrist = new Vector3(10, -8, 70);
            var settings = new MotionControlSettings(Stored(level, .7f, .18f, customWrist));
            check(settings.HapticStrength.Value == level && settings.InteractionVolume.Value == .7f &&
                settings.ThrottleTravel.Value == .18f && settings.GloveRotation.Value == customWrist,
                "Custom and disabled feedback, travel and wrist settings survive migration.");
        }
        var stock = new MotionControlSettings(new ConfigFile());
        check(stock.HapticStrength.Value == 1.5f && stock.InteractionVolume.Value == .4f &&
            stock.ThrottleTravel.Value == .12f && stock.GloveRotation.Value == new Vector3(0, 0, 90),
            "A new configuration retains current defaults.");
    }

    private static ConfigFile Stored(float haptic, float volume, float travel, Vector3 wrist)
    {
        var config = new ConfigFile();
        config.Seed(Section, "Interaction Haptic Strength", haptic);
        config.Seed(Section, "Interaction Sound Volume", volume);
        config.Seed(Section, "Throttle Travel", travel);
        config.Seed(Section, "Glove Wrist Rotation", wrist);
        return config;
    }
}
