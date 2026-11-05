using BepInEx.Configuration;
using NOVR.Rendering;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
    checks++;
}

var defaultsFile = new ConfigFile();
var defaults = new FixedFoveationSettings(defaultsFile);
Check(Enum.GetNames<FixedFoveationLevel>().SequenceEqual(new[] { "Off", "Mild", "Balanced", "Strong", "Extreme", "Maximum" }) &&
      Enum.GetValues<FixedFoveationLevel>().Select(value => (int)value).SequenceEqual(Enumerable.Range(0, 6)),
    "Two stronger modes append values 4/5 without renaming or renumbering existing saved strengths.");
Check(defaults.Level.Value == FixedFoveationLevel.Off && defaults.RequestedLevel == FixedFoveationLevel.Off,
    "A new installation must not enable experimental rendering.");
Check(!defaults.MeasureGpuCost.Value, "Optional GPU measurement is also disabled by default.");
Check(!defaults.UseEyeTracking.Value, "New installations leave optional eye tracking disabled.");
Check(defaultsFile.BoundKeys.SequenceEqual(new[] { ("VR Performance", "Fixed Foveated Rendering"), ("VR Performance", "Measure Foveation GPU Cost"), ("VR Performance", "Eye Tracked Foveation") }),
    "Foveation binds mode, optional measurement and gaze without changing resolution, renderer or streaming settings.");

foreach (var eyeTracking in new[] { false, true })
{
    var file = new ConfigFile();
    file.Seed("VR Performance", "Eye Tracked Foveation", eyeTracking);
    var settings = new FixedFoveationSettings(file);
    Check(settings.UseEyeTracking.Value == eyeTracking, "Persisted eye-tracking preference survives binding.");
    Check(settings.Level.Value == FixedFoveationLevel.Off, "Eye-tracking preference alone never enables scene foveation.");
}

foreach (var level in Enum.GetValues<FixedFoveationLevel>())
{
    var file = new ConfigFile();
    file.Seed("VR Performance", "Fixed Foveated Rendering", level);
    var settings = new FixedFoveationSettings(file);
    Check(settings.RequestedLevel == level && settings.Level.Value == level,
        "Saved preference survives initialization: " + level);
    Check(FixedFoveationPolicy.EffectiveLevel(level, available: true, xrActive: true) == level,
        "Available stereo rendering uses the explicitly selected mode: " + level);
    foreach (var available in new[] { false, true })
        foreach (var xrActive in new[] { false, true })
        {
            var effective = FixedFoveationPolicy.EffectiveLevel(level, available, xrActive);
            if (!available || !xrActive || level == FixedFoveationLevel.Off)
                Check(effective == FixedFoveationLevel.Off, "Unsupported, inactive or disabled rendering always resolves to Off.");
            Check(settings.Level.Value == level, "Runtime inactivity does not erase a user's saved preference.");
        }
}

foreach (var invalid in new[] { int.MinValue, -1, 6, 17, int.MaxValue })
{
    var file = new ConfigFile();
    file.Seed("VR Performance", "Fixed Foveated Rendering", (FixedFoveationLevel)invalid);
    var settings = new FixedFoveationSettings(file);
    Check(settings.RequestedLevel == FixedFoveationLevel.Off,
        "Unknown saved enum values cannot opt into foveated rendering.");
    Check(FixedFoveationPolicy.EffectiveLevel((FixedFoveationLevel)invalid, true, true) == FixedFoveationLevel.Off,
        "The runtime boundary also rejects invalid levels.");
}

foreach (var windows in new[] { false, true })
    foreach (var proton in new[] { false, true })
        foreach (var d3d11 in new[] { false, true })
            foreach (var vendor in new[] { 0, 0x1002, 0x8086, FixedFoveationPolicy.NvidiaVendorId })
            {
                var reason = FixedFoveationPolicy.UnavailableReason(windows, proton, d3d11, vendor);
                var eligible = windows && !proton && d3d11 && vendor == FixedFoveationPolicy.NvidiaVendorId;
                Check((reason == null) == eligible, "Only native Windows/NVIDIA/D3D11 may proceed to a native VRS probe.");
                if (!eligible)
                    Check(!string.IsNullOrWhiteSpace(reason), "Every ineligible platform has a visible unavailable reason.");
            }
Check(FixedFoveationPolicy.UnavailableReason(true, true, true, FixedFoveationPolicy.NvidiaVendorId)!.Contains("Proton"),
    "Wine reporting Windows and NVIDIA is still explicitly unavailable.");
Check(FixedFoveationPolicy.UnavailableReason(true, false, false, FixedFoveationPolicy.NvidiaVendorId)!.Contains("Direct3D11"),
    "An unsupported graphics backend is reported instead of being switched.");

CameraPolicyChecks.Run(Check);
GazeProjectionChecks.Run(Check);
StrengthProfileChecks.Run(Check);
Console.WriteLine($"PASS {checks} source-linked fixed-foveation policy/settings checks.");
