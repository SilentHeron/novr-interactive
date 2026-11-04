using System.Reflection;
using BepInEx.Configuration;
using NOVR;

var checks = 0;
void Check(bool condition, string label)
{
    checks++;
    if (!condition) throw new InvalidOperationException(label);
}
void Throws<T>(Action action, string label) where T : Exception
{
    try { action(); }
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

// Failure late in startup is especially dangerous: earlier binds have already succeeded.
foreach (var failAt in new[] { 2, firstFile.BindCount / 2, firstFile.BindCount })
{
    var failingFile = new ConfigFile();
    failingFile.BeforeBind = count =>
    {
        Check(ReferenceEquals(first, ModConfiguration.Current), "A replacement remains private throughout construction.");
        if (count == failAt) throw new IOException("Configuration bind failed");
    };
    Throws<IOException>(() => _ = new ModConfiguration(failingFile), $"Failure on binding {failAt} propagates.");
    Check(ReferenceEquals(first, ModConfiguration.Instance), "A failed replacement retains the last complete settings.");
}

var nextFile = new ConfigFile();
nextFile.BeforeBind = _ => Check(ReferenceEquals(first, ModConfiguration.Current), "Successful replacement publishes only after its last bind.");
var next = new ModConfiguration(nextFile);
Check(ReferenceEquals(next, ModConfiguration.Instance), "A successful replacement becomes current.");
Check(ReferenceEquals(firstFile, first.Config), "Replacing current settings does not mutate the previous object.");
Check(firstFile.BindCount == nextFile.BindCount, "Every settings binding is still performed.");
MotionControlMigrationChecks.Run(Check);
Console.WriteLine($"PASS {checks} configuration lifecycle checks (production ModConfiguration, configuration boundary doubles).");
