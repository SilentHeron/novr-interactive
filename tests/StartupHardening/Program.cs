using System.Reflection;
using NOVR.Compatibility;

var checks = 0;
void Check(bool condition, string label)
{
    checks++;
    if (!condition)
        throw new InvalidOperationException(label);
}

// Reflection handles: a required one that did not resolve blocks the patch; an
// optional (nullable) one does not; nested patch classes share the outer class.
Check(GameHandles.FirstUnresolved(typeof(AllResolved)) == null, "Resolved required handles and a missing optional one pass");
Check(GameHandles.FirstUnresolved(typeof(RequiredMissing)) == "RequiredMissing.Renamed", "A missing required handle is named");
Check(GameHandles.FirstUnresolved(typeof(MostlyOptional)) == "MostlyOptional.Required",
    "A required handle is still required where most of the class's handles are optional");
Check(GameHandles.FirstUnresolved(typeof(AllOptional)) == null, "A class of optional handles never blocks its patch");
Check(GameHandles.FirstUnresolved(typeof(Outer.Inner)) == "Outer.Renamed" && GameHandles.FirstUnresolved(typeof(Outer)) == "Outer.Renamed",
    "A nested patch class is blocked by its outer class's missing handle");
var throwing = GameHandles.FirstUnresolved(typeof(ThrowingInitializer));
Check(throwing != null && throwing.StartsWith("ThrowingInitializer (") && throwing.Contains("field gone"),
    "A handle whose initializer throws reports the cause instead of escaping");
Check(GameHandles.FirstUnresolved(typeof(FieldRefHandles)) == "FieldRefHandles.Missing", "Field-reference delegates count as handles; other delegates do not");
Check(GameHandles.FirstUnresolved(typeof(NoHandles)) == null && Counter.Initialized == 0,
    "A class without handles passes without running its initializer");

// Patch installation: one failing class costs that class only.
{
    var installed = new List<Type>();
    var types = new Type?[] { typeof(NotAPatch), typeof(PatchA), null, typeof(RequiredMissing), typeof(PatchThrows), typeof(PatchB), typeof(Outer.Inner) };
    var report = PatchInstaller.Apply(types!,
        type => type != typeof(NotAPatch),
        GameHandles.FirstUnresolved,
        type =>
        {
            if (type == typeof(PatchThrows))
                throw new InvalidOperationException("Patching exception in method null", new MissingMethodException("Undefined target method for patch method static void PatchThrows::Postfix()\nstack"));
            installed.Add(type);
        });
    Check(installed.SequenceEqual(new[] { typeof(PatchA), typeof(PatchB) }), "Classes after a failing one are still installed, in order");
    Check(report.Applied == 2 && report.Skipped.Count == 3, "Applied and skipped classes are both counted");
    Check(report.Skipped[0].Key == "RequiredMissing" && report.Skipped[0].Value == "game member not found: RequiredMissing.Renamed",
        "A missing member skips the class before Harmony is asked to patch it");
    Check(report.Skipped[1].Key == "PatchThrows" && report.Skipped[1].Value == "InvalidOperationException: Undefined target method for patch method static void PatchThrows::Postfix()",
        "A Harmony failure is reported with its innermost message, one line");
    Check(report.Skipped[2].Key == "Outer.Inner", "Nested classes are reported with their outer class");
    var attributeFailure = PatchInstaller.Apply(new[] { typeof(PatchA), typeof(PatchB) },
        type => type == typeof(PatchA) ? throw new TypeLoadException("unreadable attribute") : true, _ => null, installed.Add);
    Check(attributeFailure.Applied == 1 && attributeFailure.Skipped.Count == 0, "A type whose attributes cannot be read is not treated as a patch");
    var handleFailure = PatchInstaller.Apply(new[] { typeof(PatchA) }, _ => true, _ => throw new InvalidOperationException("handle check failed"), installed.Add);
    Check(handleFailure.Applied == 0 && handleFailure.Skipped.Single().Value == "InvalidOperationException: handle check failed",
        "A failing handle check skips the class instead of aborting startup");
}

// Diagnostics: version comparison ignores tags other mods append.
{
    Check(StartupDiagnostics.IsSupportedGame(StartupDiagnostics.SupportedGameVersion), "The supported version matches itself");
    Check(StartupDiagnostics.IsSupportedGame(StartupDiagnostics.SupportedGameVersion + "_com.example.mod-v2.1.1_NOBUNDLES"),
        "A tag appended by another mod does not count as a different game");
    foreach (var other in new[] { "0.34.60", "0.34", "0.35.0", "1.0.34.6", "", null, "beta" })
        Check(!StartupDiagnostics.IsSupportedGame(other), "Another or unreadable version is not the supported one: " + other);
    Check(StartupDiagnostics.NumericVersion("0.35.1.") == "0.35.1" && StartupDiagnostics.NumericVersion("v2") == "", "Only the leading number is compared");

    var log = new List<string>();
    StartupDiagnostics.Log = log.Add;
    StartupDiagnostics.Reset();
    StartupDiagnostics.CheckGame(StartupDiagnostics.SupportedGameVersion + "_tag");
    StartupDiagnostics.CheckLayers(new[] { 29, 30, 31 }, layer => layer == 31 ? throw new InvalidOperationException() : "");
    StartupDiagnostics.ReportPatches(new PatchInstallReport { Applied = 80 });
    Check(StartupDiagnostics.Warnings.Count == 0 && StartupDiagnostics.Summary == "" && log.Single() == "[NOVR] Installed 80 patch classes.",
        "A normal start raises no warning and logs one line");

    StartupDiagnostics.CheckGame("0.35.0_tag");
    StartupDiagnostics.CheckGame("0.35.0_tag");
    Check(StartupDiagnostics.Warnings.Count == 1 && StartupDiagnostics.Warnings[0].Contains("0.34.6") && StartupDiagnostics.Warnings[0].Contains("0.35.0") &&
          !StartupDiagnostics.Warnings[0].Contains("tag"), "A different game version is warned about once, naming both versions");
    StartupDiagnostics.CheckLayers(new[] { 29, 30 }, layer => layer == 30 ? "OtherModUi" : null);
    Check(StartupDiagnostics.Warnings.Count == 2 && StartupDiagnostics.Warnings[1].Contains("Layer 30") && StartupDiagnostics.Warnings[1].Contains("OtherModUi"),
        "A layer the game or another mod already named is reported");
    var partial = new PatchInstallReport { Applied = 77 };
    partial.Skipped.Add(new("MarkerPatch", "game member not found: MarkerPatch.Hidden"));
    partial.Skipped.Add(new("MapPatch", "HarmonyException: Undefined target method"));
    var before = log.Count;
    StartupDiagnostics.ReportPatches(partial);
    Check(log.Skip(before).Count(line => line.Contains("was not installed")) == 2 && StartupDiagnostics.Warnings[2].StartsWith("2 of 79 VR adjustments"),
        "Each skipped patch is logged and the summary counts them");
    Check(StartupDiagnostics.Summary.Split('\n').Length == 4 && StartupDiagnostics.Summary.StartsWith("NOVR warnings:"), "The summary lists every warning for the in-headset menu");
    StartupDiagnostics.Reset();
    StartupDiagnostics.CheckGame(null);
    Check(StartupDiagnostics.Warnings.Single().Contains("an unknown version"), "An unreadable version is still reported");
    StartupDiagnostics.Reset();
    StartupDiagnostics.Log = null;
}

// Deferred saving: one write shortly after the last change, and on demand.
{
    var saves = 0;
    var reports = new List<string>();
    var fail = false;
    var deferred = new DeferredConfigSave(() => { saves++; if (fail) throw new IOException("disk full"); }, reports.Add);
    deferred.Tick(10);
    deferred.Flush();
    Check(saves == 0 && !deferred.Pending, "Nothing is written while nothing changed");
    for (var i = 0; i < 90; i++)
    {
        deferred.Changed(10 + i / 90f);
        deferred.Tick(10 + i / 90f);
    }
    Check(saves == 0 && deferred.Pending, "A slider dragged for a second writes nothing while it moves");
    deferred.Tick(10 + 89 / 90f + DeferredConfigSave.DelaySeconds - .01f);
    Check(saves == 0, "The wait restarts with every change");
    deferred.Tick(10 + 89 / 90f + DeferredConfigSave.DelaySeconds);
    deferred.Tick(100);
    Check(saves == 1 && !deferred.Pending, "The drag is saved once after it ends");
    deferred.Changed(200);
    deferred.Flush();
    deferred.Flush();
    Check(saves == 2, "Closing the game writes a pending change immediately, once");
    fail = true;
    deferred.Changed(300);
    deferred.Flush();
    deferred.Changed(301);
    deferred.Flush();
    Check(saves == 4 && reports.Count == 1 && reports[0].Contains("disk full"), "A failing write is reported once and does not stop later attempts");
    fail = false;
    deferred.Changed(302);
    deferred.Flush();
    fail = true;
    deferred.Changed(303);
    deferred.Flush();
    Check(reports.Count == 2, "A new failure after a successful write is reported again");
    fail = false;
    var before = saves;
    deferred.Flush();
    deferred.Flush(always: true);
    Check(saves == before + 1 && !deferred.Pending, "Closing the game writes once even when only new settings were bound");
}

Console.WriteLine($"PASS: {checks} startup hardening checks (handle guard, patch isolation, diagnostics, deferred saving).");

// Stand-ins for patch classes and their cached handles.
static class Members
{
    internal static readonly FieldInfo Present = typeof(string).GetField(nameof(string.Empty))!;
    internal static FieldInfo? Missing => null;
}
static class AllResolved
{
    private static readonly FieldInfo A = Members.Present;
    private static readonly MethodInfo B = typeof(string).GetMethod(nameof(string.Trim), Type.EmptyTypes)!;
    private static readonly FieldInfo? Optional = Members.Missing;
    internal static object?[] Use => new object?[] { A, B, Optional };
}
static class RequiredMissing
{
    private static readonly FieldInfo A = Members.Present;
    private static readonly FieldInfo Renamed = Members.Missing!;
    internal static object?[] Use => new object?[] { A, Renamed };
}
static class MostlyOptional
{
    private static readonly FieldInfo? A = Members.Missing;
    private static readonly FieldInfo? B = Members.Missing;
    private static readonly MethodInfo? C = null;
    private static readonly FieldInfo Required = Members.Missing!;
    internal static object?[] Use => new object?[] { A, B, C, Required };
}
static class AllOptional
{
    private static readonly FieldInfo? A = Members.Missing;
    private static readonly PropertyInfo? B = null;
    internal static object?[] Use => new object?[] { A, B };
}
static class Outer
{
    private static readonly FieldInfo Renamed = Members.Missing!;
    internal static object? Use => Renamed;
    internal static class Inner
    {
    }
}
static class ThrowingInitializer
{
    private static readonly FieldInfo A = Fail();
    internal static object? Use => A;
    private static FieldInfo Fail() => throw new MissingFieldException("field gone");
}
delegate ref int FieldRef<T>(T instance);
static class FieldRefHandles
{
    private static readonly Func<int> Unrelated = null!;
    private static readonly FieldRef<string>? Optional = null;
    private static readonly FieldRef<string> Missing = null!;
    internal static object?[] Use => new object?[] { Unrelated, Optional, Missing };
}
static class Counter
{
    internal static int Initialized;
}
static class NoHandles
{
    private static readonly int Value = ++Counter.Initialized;
    internal static int Use => Value;
}
static class NotAPatch
{
}
static class PatchA
{
}
static class PatchB
{
}
static class PatchThrows
{
}
