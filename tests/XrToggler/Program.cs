using NOVR.VrTogglers;
using UnityEngine;
using UnityEngine.XR.Management;

var checks = 0;
void Check(bool condition, string reason) { checks++; if (!condition) throw new Exception(reason); }
void Throws(Action action, string contains)
{
    try
    {
        action();
        throw new Exception("Expected failure: " + contains);
    }
    catch (InvalidOperationException error) { Check(error.Message.Contains(contains), "Preserve lifecycle exception"); }
}

var basic = new ControlledToggler { SetupResult = false };
basic.SetVrEnabled(false);
Check(basic.Setups == 0 && basic.Disables == 0, "Initial off request does not initialize or disable an absent session");
basic.SetVrEnabled(true);
Check(basic.Setups == 1 && basic.Enables == 0 && !basic.IsVrEnabled, "Setup false cannot dispatch enable");
basic.SetupResult = true;
basic.SetVrEnabled(true);
basic.SetVrEnabled(true);
Check(basic.Setups == 2 && basic.Enables == 1 && basic.IsVrEnabled, "Setup can retry and repeated enabled request is idempotent");
basic.DisableResult = false;
basic.SetVrEnabled(false);
Check(basic.IsVrEnabled && basic.Disables == 1, "Failed disable does not falsely report off");
basic.DisableResult = true;
basic.SetVrEnabled(false);
basic.SetVrEnabled(false);
Check(!basic.IsVrEnabled && basic.Disables == 2, "Successful off transition happens once");
basic.SetVrEnabled(true);
Check(basic.IsVrEnabled && basic.Setups == 2 && basic.Enables == 2, "Backend configuration remains reusable after a normal off transition");
var rejected = new ControlledToggler { EnableResult = false };
rejected.SetVrEnabled(true);
rejected.SetVrEnabled(false);
rejected.EnableResult = true;
rejected.SetVrEnabled(true);
Check(rejected.Setups == 1 && rejected.Enables == 2 && rejected.Disables == 0 && rejected.IsVrEnabled,
    "Failed enable may retry without false off calls or duplicate setup");

foreach (var previousBackground in new[] { false, true })
{
    Application.runInBackground = previousBackground;
    var backend = new TestXrToggler();
    for (var cycle = 1; cycle <= 3; cycle++)
    {
        backend.SetVrEnabled(true);
        backend.SetVrEnabled(true);
        Check(backend.IsVrEnabled && backend.Manager.activeLoader == backend.Loader && backend.Manager.isInitializationComplete,
            "Every re-enable initializes an active loader before starting");
        Check(Application.runInBackground, "Active VR keeps background updates");
        Check(backend.Loader.Initializes == cycle && backend.Loader.Starts == cycle && backend.Creates == 1,
            "Repeated cycles reuse one configured loader and start once per transition");
        backend.SetVrEnabled(false);
        backend.SetVrEnabled(false);
        Check(!backend.IsVrEnabled && backend.Manager.activeLoader == null && !backend.Manager.isInitializationComplete,
            "Off clears the actual manager's initialized state");
        Check(backend.Loader.Stops == cycle && backend.Loader.Deinitializes == cycle, "Manager handles one stop per deinitialize");
        Check(Application.runInBackground == previousBackground, "Each off transition restores the user's original background setting");
    }
    Check(backend.Loader.Calls.SequenceEqual(Enumerable.Repeat(new[] { "initialize", "start", "stop", "deinitialize" }, 3).SelectMany(x => x)),
        "Initialize/start/stop/deinitialize order is preserved over three cycles");
}

Application.runInBackground = false;
var missing = new TestXrToggler();
missing.Loader.CanInitialize = false;
try
{
    missing.SetVrEnabled(true);
    throw new Exception("Missing loader should report initialization failure");
}
catch (Exception error) { Check(error.Message.StartsWith("Cannot initialize OpenXR Loader"), "Startup retains actionable headset-not-ready failure"); }
Check(!missing.IsVrEnabled && !Application.runInBackground && missing.Manager.StartCalls == 0, "Failed initialization cannot start subsystems or change background setting");
missing.Loader.CanInitialize = true;
missing.SetVrEnabled(true);
Check(missing.IsVrEnabled && missing.Creates == 1 && missing.Loader.Initializes == 2, "Headset-ready retry reuses configured manager instead of leaking settings objects");
missing.Manager.DeinitializeLoader(); // Native runtime can stop independently.
missing.SetVrEnabled(false);
Check(!missing.IsVrEnabled && !Application.runInBackground, "Off restores background setting even if native loader is already absent");

var badInit = new TestXrToggler();
badInit.Loader.ThrowInitialize = true;
Throws(() => badInit.SetVrEnabled(true), "initialize failed");
Check(!badInit.IsVrEnabled && !Application.runInBackground, "Initialization exception leaves pre-enable background policy intact");
badInit.Loader.ThrowInitialize = false;
badInit.SetVrEnabled(true);
badInit.SetVrEnabled(false);
Check(badInit.Creates == 1 && badInit.Loader.Starts == 1, "Thrown initialization can be retried without recreating loader");

var badStart = new TestXrToggler();
badStart.Loader.ThrowStart = true;
Throws(() => badStart.SetVrEnabled(true), "start failed");
Check(!badStart.IsVrEnabled && !Application.runInBackground && badStart.Manager.activeLoader == null,
    "Start exception cleans initialized loader and restores background policy");
badStart.Loader.ThrowStart = false;
badStart.SetVrEnabled(true);
badStart.SetVrEnabled(false);
Check(badStart.Loader.Initializes == 2 && badStart.Creates == 1, "Start failure retry initializes again on the same configured loader");

var badStop = new TestXrToggler();
badStop.SetVrEnabled(true);
badStop.Loader.ThrowDeinitialize = true;
Throws(() => badStop.SetVrEnabled(false), "deinitialize failed");
Check(badStop.IsVrEnabled && !Application.runInBackground, "Teardown error restores background policy without falsely reporting clean shutdown");
badStop.Loader.ThrowDeinitialize = false;
badStop.SetVrEnabled(false);
Check(!badStop.IsVrEnabled && !Application.runInBackground, "Teardown retry can finish without overwriting saved background state");

var manager = new VrTogglerManager();
var actual = XRManagerSettings.Last!;
var loader = actual.activeLoader!;
Check(loader is UnityEngine.XR.OpenXR.OpenXRLoader && loader.Starts == 1, "Public manager still starts configured OpenXR on construction");
manager.ToggleVr();
manager.ToggleVr();
Check(actual.activeLoader == loader && loader.Initializes == 2 && loader.Starts == 2, "Public toggle path re-enables the same OpenXR loader");
manager.ToggleVr();
Check(!Application.runInBackground && actual.activeLoader == null, "Public manager disable restores normal background policy");

// A headset that is not ready at launch must not abort startup: the manager
// records why, stays usable, and a later attempt succeeds.
{
    Application.runInBackground = false;
    var notReady = new TestXrToggler();
    notReady.Loader.CanInitialize = false;
    var late = new VrTogglerManager(notReady);
    Check(!late.IsVrEnabled && late.LastFailure != null && late.LastFailure.StartsWith("Cannot initialize OpenXR Loader") && !Application.runInBackground,
        "A runtime that is not ready is reported instead of thrown, and nothing is left half started");
    Check(!late.TryEnable() && notReady.Loader.Initializes == 2 && notReady.Creates == 1, "A retry while still not ready fails the same way without rebuilding settings");
    notReady.Loader.CanInitialize = true;
    Check(late.TryEnable() && late.IsVrEnabled && late.LastFailure == null && notReady.Loader.Starts == 1 && Application.runInBackground,
        "A retry once the headset is ready starts VR and clears the failure");
    Check(late.TryEnable() && notReady.Loader.Starts == 1, "Trying again while VR is running does nothing");
    var refused = new ControlledToggler { EnableResult = false };
    var quiet = new VrTogglerManager(refused);
    Check(!quiet.IsVrEnabled && quiet.LastFailure == "The VR runtime did not start.", "A start that fails without an exception still has a reason");
    late.ToggleVr();
    Application.runInBackground = false;
}

// Retry schedule: a few automatic attempts, then only on request.
{
    var retry = new VrStartRetry();
    Check(!retry.WaitingToRetry && !retry.Due(1000) && retry.SecondsUntilRetry(0) == -1, "Nothing is scheduled before a failure");
    var now = 10f;
    foreach (var delay in VrStartRetry.AutomaticDelays)
    {
        retry.Failed(now);
        Check(retry.WaitingToRetry && Math.Abs(retry.SecondsUntilRetry(now) - delay) < .001f && !retry.Due(now + delay - .01f), "Each failure schedules the next automatic attempt");
        now += delay;
        Check(retry.Due(now) && !retry.Due(now + 1000) && !retry.WaitingToRetry, "A scheduled attempt is due exactly once");
    }
    retry.Failed(now);
    Check(!retry.WaitingToRetry && retry.AutomaticAttemptsLeft == 0 && !retry.Due(now + 100000) && retry.SecondsUntilRetry(now) == -1,
        "After the automatic attempts the mod stops reopening the runtime on its own");
    retry.Failed(now);
    Check(!retry.WaitingToRetry, "A failed manual attempt schedules nothing further");
    retry.Succeeded();
    retry.Failed(now);
    Check(retry.WaitingToRetry && retry.AutomaticAttemptsLeft == VrStartRetry.AutomaticDelays.Length - 1, "A successful start restores the automatic attempts for a later loss");
}
Console.WriteLine($"{checks} source-linked VR toggler lifecycle checks passed (XR boundary doubles; no headset/runtime launched).");

sealed class ControlledToggler : VrToggler
{
    internal int Setups, Enables, Disables;
    internal bool SetupResult = true, EnableResult = true, DisableResult = true;
    protected override bool SetUp()
    {
        Setups++;
        return SetupResult;
    }
    protected override bool EnableVr()
    {
        Enables++;
        return EnableResult;
    }
    protected override bool DisableVr()
    {
        Disables++;
        return DisableResult;
    }
}
sealed class TestXrToggler : XrPluginToggler
{
    internal readonly XRLoader Loader = new();
    internal int Creates;
    internal XRManagerSettings Manager => _managerSetings;
    protected override XRLoader CreateLoader()
    {
        Creates++;
        return Loader;
    }
}
