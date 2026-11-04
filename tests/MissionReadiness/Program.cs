using System.Collections;
using System.Globalization;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using NOVR.VrUi.Native;

var checks = 0;
void Assert(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
void Check(string name, Action test) { test(); checks++; Console.WriteLine("PASS " + name); }
var adapter = typeof(BlueprinterContentReadiness);
object? Call(string name, params object?[] args) => adapter.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);
void Field(string name, object value) => adapter.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, value);
void Reset()
{
    Call("Shutdown");
    Chainloader.PluginInfos.Clear();
    Field("_initialized", false); Field("_routineObserved", false);
    Blueprinter.EncyclopediaAfterLoadPatch.Started = false;
    // Finish a fresh successful operation to reset the production state without
    // adding test-only state mutation methods to shipped code.
    var state = (MissionContentState)adapter.GetField("State", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
    state.Begin(); state.ObserveSuccess(); state.End(true);
}
void Install(Version? version = null)
{
    Chainloader.PluginInfos["com.nikkorap.blueprinter"] = new PluginInfo
    { Instance = new Blueprinter.Plugin(), Metadata = new PluginMetadata { Version = version ?? new Version(2, 0, 1) } };
}
IEnumerator Wrap(IEnumerator routine)
{
    object?[] begin = { false }; Call("RoutineStarting", begin);
    object?[] created = { routine, begin[0] }; Call("RoutineCreated", created);
    return (IEnumerator)created[0]!;
}
IEnumerator Sequence(params Action[] steps)
{
    foreach (var step in steps) { step(); yield return null; }
}
void Drain(IEnumerator routine) { while (routine.MoveNext()) { } }
var source = Blueprinter.Plugin.Log;
const string success = "[Plugin] Done in 73.56s";

Check("No add-on preserves immediate mission launch without hooks", () =>
{
    Reset(); var s = BlueprinterContentReadiness.Read();
    Assert(s.CanLaunch && s.Phase == MissionContentPhase.Ready && s.Status == "");
    Assert(Harmony.Patches == 0 && source.Subscribers == 0);
});
Check("Supported loader is blocked before its first work, without a timer", () =>
{
    // The previous read found no add-on. A later chainloader entry must still
    // be discovered if Encyclopedia is requested early by another plugin.
    Install(); var s = BlueprinterContentReadiness.Read();
    Assert(!s.CanLaunch && s.Phase == MissionContentPhase.Preparing);
    Assert(Harmony.Target!.Name == "RunRoutine" && Harmony.Patches == 1);
    for (var i = 0; i < 100000; i++) Assert(!BlueprinterContentReadiness.Read().CanLaunch);
});
Check("Early hook targets the instance AfterLoad before the loader's postfix", () =>
{
    var patch = adapter.GetCustomAttribute<HarmonyPatch>()!;
    Assert(patch.Type == typeof(Encyclopedia) && patch.Name == "AfterLoad" && patch.Parameters.Length == 0);
});
Check("Actual completion event alone never unlocks an unfinished coroutine", () =>
{
    var s = BlueprinterContentReadiness.Read(); var routine = Wrap(Sequence(() => source.Emit(success)));
    Assert(source.Subscribers == 1 && routine.MoveNext() && !s.CanLaunch);
    Assert(!routine.MoveNext() && s.CanLaunch && source.Subscribers == 0);
});
Check("Early yield break is a failure even when it removes a loading screen", () =>
{
    Drain(Wrap(Array.Empty<object>().GetEnumerator()));
    Assert(BlueprinterContentReadiness.Read().Phase == MissionContentPhase.Failed && source.Subscribers == 0);
});
Check("Loader retry can succeed after an observed failure", () =>
{
    Drain(Wrap(Sequence(() => source.Emit(success))));
    Assert(BlueprinterContentReadiness.Read().CanLaunch);
});
Check("Another source cannot spoof a completion event", () =>
{
    Drain(Wrap(Sequence(() => source.Emit(success, claimedSource: new ManualLogSource()))));
    Assert(!BlueprinterContentReadiness.Read().CanLaunch);
});
Check("Warning or debug text containing Done cannot authorize launch", () =>
{
    Drain(Wrap(Sequence(() => source.Emit(success, LogLevel.Warning), () => source.Emit(success, LogLevel.Debug))));
    Assert(!BlueprinterContentReadiness.Read().CanLaunch);
});
Check("Unrelated loader warnings do not turn successful work into failure", () =>
{
    Drain(Wrap(Sequence(() => source.Emit("[Plugin] 3 deferred patches unresolved", LogLevel.Warning), () => source.Emit(success))));
    Assert(BlueprinterContentReadiness.Read().CanLaunch);
});
Check("Stale success outside the observed operation cannot authorize a later one", () =>
{
    source.Emit(success); Drain(Wrap(Array.Empty<object>().GetEnumerator()));
    Assert(!BlueprinterContentReadiness.Read().CanLaunch && source.Subscribers == 0);
});
Check("Exception propagates to the loader and releases the subscription", () =>
{
    var routine = Wrap(Sequence(() => throw new InvalidOperationException("loader failed")));
    try { routine.MoveNext(); throw new Exception("Exception suppressed"); }
    catch (InvalidOperationException) { }
    Assert(!BlueprinterContentReadiness.Read().CanLaunch && source.Subscribers == 0);
});
Check("Disposing interrupted work fails once and releases the subscription", () =>
{
    var routine = Wrap(Sequence(() => source.Emit(success)));
    Assert(routine.MoveNext()); ((IDisposable)routine).Dispose(); ((IDisposable)routine).Dispose();
    Assert(!BlueprinterContentReadiness.Read().CanLaunch && source.Subscribers == 0);
});
Check("Disposal after successful completion cannot erase the success", () =>
{
    var routine = Wrap(Sequence(() => source.Emit(success))); Drain(routine); ((IDisposable)routine).Dispose();
    Assert(BlueprinterContentReadiness.Read().CanLaunch);
});
Check("A failed coroutine factory releases its observation immediately", () =>
{
    object?[] begin = { false }; Call("RoutineStarting", begin);
    Call("RoutineCreationFailed", new InvalidOperationException("factory"), begin[0]);
    Assert(!BlueprinterContentReadiness.Read().CanLaunch && source.Subscribers == 0);
});
Check("The wrapper preserves yielded objects and order", () =>
{
    var first = new object(); var second = new object();
    var observed = new List<bool>(); var routine = new ObservedContentRoutine(new[] { first, second }.GetEnumerator(), observed.Add);
    Assert(routine.MoveNext() && ReferenceEquals(routine.Current, first));
    Assert(routine.MoveNext() && ReferenceEquals(routine.Current, second));
    Assert(!routine.MoveNext() && observed.SequenceEqual(new[] { true }));
});
Check("Shutdown detaches only its own hooks and log handler", () =>
{
    _ = Wrap(Sequence(() => { })); Assert(source.Subscribers == 1);
    Assert(UnityEngine.Application.QuitHandlers == 1);
    UnityEngine.Application.Quit();
    Assert(source.Subscribers == 0 && Harmony.Patches == 0 && UnityEngine.Application.QuitHandlers == 0);
});
Check("Late attachment remains truthfully unverified without locking a completed loader", () =>
{
    Reset(); Install(); Blueprinter.EncyclopediaAfterLoadPatch.Started = true;
    var s = BlueprinterContentReadiness.Read();
    Assert(s.CanLaunch && s.Phase == MissionContentPhase.Unverified && Harmony.Patches == 0);
});
Check("Unknown loader versions retain stock behavior with unverified status", () =>
{
    Reset(); Install(new Version(3, 0, 0)); var s = BlueprinterContentReadiness.Read();
    Assert(s.CanLaunch && s.Phase == MissionContentPhase.Unverified && Harmony.Patches == 0);
});
Check("Only the exact known terminal message shape is accepted", () =>
{
    Assert(BlueprinterCompletionMessage.IsSuccess(success));
    foreach (var text in new string?[] { null, "Done", "[Plugin] Done in -1s", "[Plugin] Done in NaNs", "[Plugin] Done in Infinitys", "[Plugin] Done in 1s but failed", "[Plugin] Bundle loading failed", "[Plugin] Done in s", "prefix " + success })
        Assert(!BlueprinterCompletionMessage.IsSuccess(text), text ?? "null");
});
Check("The completion event follows the loader's numeric culture, not an English-only timer parser", () =>
{
    var original = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        Assert(BlueprinterCompletionMessage.IsSuccess("[Plugin] Done in 73,56s"));
    }
    finally { CultureInfo.CurrentCulture = original; }
});
Reset();
Console.WriteLine($"{checks} content-readiness checks passed (portable adapter doubles; no Unity player launched).");
