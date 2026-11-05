using System.Reflection;
using BepInEx.Configuration;
using NOVR;
using NOVR.Rendering;

var checks = 0;
void Check(bool condition, string label)
{
    checks++;
    if (!condition)
        throw new InvalidOperationException(label);
}
void Throws<T>(Action action, string label) where T : Exception
{
    try
    {
        action();
    }
    catch (T) { Check(true, label); return; }
    throw new InvalidOperationException(label);
}

Check(ModConfiguration.Current == null, "Startup exposes no configuration before binding.");
Throws<InvalidOperationException>(() => _ = ModConfiguration.Instance, "Required access before initialization fails clearly.");
Throws<ArgumentNullException>(() => _ = new ModConfiguration(null!), "A missing ConfigFile is rejected.");
Check(ModConfiguration.Current == null, "Invalid construction cannot publish settings.");

var firstFailure = new ConfigFile { BeforeBind = _ => throw new IOException("Unreadable configuration") };
Throws<IOException>(() => _ = new ModConfiguration(firstFailure), "A binding failure propagates.");
Check(ModConfiguration.Current == null, "First binding failure leaves no partially populated singleton.");

var firstFile = new ConfigFile();
firstFile.BeforeBind = _ => Check(ModConfiguration.Current == null, "Reentrant binding callback cannot observe unfinished settings.");
var first = new ModConfiguration(firstFile);
Check(ReferenceEquals(first, ModConfiguration.Instance), "Completed settings become current.");
Check(ReferenceEquals(firstFile, first.Config), "The caller's ConfigFile is retained.");
foreach (var field in typeof(ModConfiguration).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
    Check(field.GetValue(first) != null, $"Published settings include {field.Name}.");
Check(first.NativeMenuScale.Value == 1.25f && first.FlightHudScale.Value == .38f,
    "Lifecycle changes preserve existing menu and HUD defaults.");
Check(first.FixedFoveation.RequestedLevel == FixedFoveationLevel.Off,
    "New configurations leave experimental fixed foveated rendering off.");
Check(!first.FixedFoveation.UseEyeTracking.Value,
    "Optional eye-tracked foveation is disabled for new configurations.");

Check(first.SeatClearanceEnabled.Value, "Head clearance is enabled by default for new settings.");
Check(first.SeatClearanceRadius.Value == .25f, "Head clearance starts at a 25 cm radius.");
var clearanceRange = firstFile.Descriptions[("Comfort", "Head Clearance Radius")].AcceptableValues as AcceptableValueRange<float>;
Check(clearanceRange is { Minimum: .05f, Maximum: .40f }, "The config declares the same 5–40 cm bounds used by the menu.");

// Failure late in startup is especially dangerous: earlier binds have already succeeded.
foreach (var failAt in new[] { 2, firstFile.BindCount / 2, firstFile.BindCount })
{
    var failingFile = new ConfigFile();
    failingFile.BeforeBind = count =>
    {
        Check(ReferenceEquals(first, ModConfiguration.Current), "A replacement remains private throughout construction.");
        if (count == failAt)
            throw new IOException("Configuration bind failed");
    };
    Throws<IOException>(() => _ = new ModConfiguration(failingFile), $"Failure on binding {failAt} propagates.");
    Check(ReferenceEquals(first, ModConfiguration.Instance), "A failed replacement retains the last complete settings.");
}

var nextFile = new ConfigFile();
nextFile.Seed("Comfort", "Head Clearance", false);
nextFile.Seed("Comfort", "Head Clearance Radius", .16f);
nextFile.Seed("VR Performance", "Fixed Foveated Rendering", FixedFoveationLevel.Balanced);
nextFile.Seed("VR Performance", "Eye Tracked Foveation", true);
nextFile.BeforeBind = _ => Check(ReferenceEquals(first, ModConfiguration.Current), "Successful replacement publishes only after its last bind.");
var next = new ModConfiguration(nextFile);
Check(ReferenceEquals(next, ModConfiguration.Instance), "A successful replacement becomes current.");
Check(!next.SeatClearanceEnabled.Value && next.SeatClearanceRadius.Value == .16f,
    "Saved head-clearance opt-out and custom radius survive configuration initialization.");
Check(next.FixedFoveation.RequestedLevel == FixedFoveationLevel.Balanced,
    "An explicit saved fixed-foveation preference survives configuration initialization.");
Check(next.FixedFoveation.UseEyeTracking.Value && !first.FixedFoveation.UseEyeTracking.Value,
    "Saved eye tracking survives binding without changing the previous configuration object.");
Check(ReferenceEquals(firstFile, first.Config), "Replacing current settings does not mutate the previous object.");
Check(firstFile.BindCount == nextFile.BindCount, "Every settings binding is still performed.");
foreach (var savedRadius in new[] { .05f, .12f, .25f, .40f })
{
    var savedFile = new ConfigFile();
    savedFile.Seed("Comfort", "Head Clearance Radius", savedRadius);
    var saved = new ModConfiguration(savedFile);
    Check(saved.SeatClearanceRadius.Value == savedRadius, "Existing radius choices, including the former 12 cm default, remain unchanged.");
}
foreach (var strength in Enum.GetValues<FixedFoveationLevel>())
    foreach (var eyeTracking in new[] { false, true })
    {
        var savedFile = new ConfigFile();
        savedFile.Seed("VR Performance", "Fixed Foveated Rendering", strength);
        savedFile.Seed("VR Performance", "Eye Tracked Foveation", eyeTracking);
        savedFile.Seed("Comfort", "Head Clearance Radius", .18f);
        var saved = new ModConfiguration(savedFile);
        Check(saved.FixedFoveation.RequestedLevel == strength && saved.FixedFoveation.UseEyeTracking.Value == eyeTracking,
            "Every strength, including Extreme/Maximum, retains independent fixed/eye-tracking preference through full configuration binding.");
        Check(saved.SeatClearanceRadius.Value == .18f && saved.NativeMenuScale.Value == first.NativeMenuScale.Value,
            "Adding stronger rendering modes does not migrate unrelated comfort or interface preferences.");
        Check(savedFile.BoundKeys.Count(key => key == "Fixed Foveated Rendering") == 1,
            "The original strength key remains the only persisted strength control.");
    }
MotionControlMigrationChecks.Run(Check);
Console.WriteLine($"PASS {checks} configuration lifecycle checks (production ModConfiguration, configuration boundary doubles).");
