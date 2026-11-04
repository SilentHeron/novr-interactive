using System;
using System.Collections;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace NOVR.VrUi.Native;

// Optional adapter for the locally verified Blueprinter 2.0.1 contract. That
// loader opens its own offline mission during startup, so the existence of a
// NetworkManager, a bundle signature or a vanished loading screen is NOT ready.
// Its terminal success message is emitted only after patches/ops and host cleanup.
[HarmonyPatch(typeof(Encyclopedia), "AfterLoad", new Type[0])]
internal static class BlueprinterContentReadiness
{
    private const string PluginId = "com.nikkorap.blueprinter";
    private const string PatchId = "deltawing.novr.content-readiness";
    private static readonly MissionContentState State = new();
    private static bool _initialized, _routineObserved;
    private static ManualLogSource? _source;
    private static EventHandler<LogEventArgs>? _handler;
    private static Harmony? _harmony;
    private static MethodInfo? _routine;
    private static bool _quittingHook;

    // Runs before the loader's AfterLoad postfix, even if BepInEx constructed
    // NOVR before Blueprinter. No dependency or loader assembly is required.
    private static void Prefix() => Initialize();

    internal static MissionContentState Read()
    {
        Initialize();
        return State;
    }

    private static void Initialize()
    {
        if (_initialized)
            return;
        // Main-menu initialization occurs after the chainloader. Encyclopedia's
        // prefix is the earlier normal path; both precede the first mission click.
        if (!Chainloader.PluginInfos.TryGetValue(PluginId, out var plugin))
            return;
        _initialized = true;
        try
        {
            var type = plugin.Instance?.GetType();
            if (plugin.Metadata.Version != new Version(2, 0, 1) || type?.FullName != "Blueprinter.Plugin")
            {
                Unknown("the installed Blueprinter version is not supported by the readiness adapter");
                return;
            }
            _source = type.GetProperty("Log", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as ManualLogSource;
            _routine = type.GetMethod("RunRoutine", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Encyclopedia) }, null);
            var runOnce = type.Assembly.GetType("Blueprinter.EncyclopediaAfterLoadPatch")?.GetField("RunOnce", BindingFlags.NonPublic | BindingFlags.Static);
            if (_source == null || _routine?.ReturnType != typeof(IEnumerator) || runOnce?.FieldType != typeof(bool))
            {
                Unknown("the loader's readiness contract was not found");
                return;
            }
            // A hot-loaded NOVR cannot reconstruct an earlier operation from an
            // empty loading screen or old log file. Never falsely call it ready or
            // permanently lock a previously completed loader on that evidence.
            if (runOnce.GetValue(null) is not false)
            {
                Unknown("NOVR attached after Blueprinter's startup operation began; restart to enable readiness tracking");
                return;
            }
            _harmony = new Harmony(PatchId);
            _harmony.Patch(_routine,
                prefix: new HarmonyMethod(typeof(BlueprinterContentReadiness), nameof(RoutineStarting)),
                postfix: new HarmonyMethod(typeof(BlueprinterContentReadiness), nameof(RoutineCreated)),
                finalizer: new HarmonyMethod(typeof(BlueprinterContentReadiness), nameof(RoutineCreationFailed)));
            State.Preparing();
            Application.quitting += Shutdown;
            _quittingHook = true;
            Debug.Log("[NOVR] Blueprinter 2.0.1 startup readiness tracking enabled.");
        }
        catch (Exception exception)
        {
            Shutdown();
            Unknown("readiness adapter initialization failed: " + exception.Message);
        }
    }

    private static void Unknown(string reason)
    {
        State.Unverified();
        Debug.LogWarning("[NOVR] Content readiness unverified: " + reason + ". Existing mission behavior is retained.");
    }

    private static void RoutineStarting(out bool __state)
    {
        __state = !_routineObserved;
        if (!__state)
            return;
        _routineObserved = true;
        State.Begin();
        if (_source != null)
        {
            // Subscribe to this exact instance, not the global log bus. Only a
            // verified success message during this operation can mark completion.
            _handler = (_, args) =>
            {
                if (ReferenceEquals(args.Source, _source) && args.Level == LogLevel.Info &&
                    BlueprinterCompletionMessage.IsSuccess(args.Data as string))
                    State.ObserveSuccess();
            };
            _source.LogEvent += _handler;
        }
    }

    private static void RoutineCreated(ref IEnumerator __result, bool __state)
    {
        if (!__state)
            return;
        if (__result == null)
        {
            Finished(false);
            return;
        }
        __result = new ObservedContentRoutine(__result, Finished);
    }

    private static void RoutineCreationFailed(Exception? __exception, bool __state)
    {
        if (__state && __exception != null)
            Finished(false);
    }

    private static void Finished(bool normal)
    {
        State.End(normal);
        _routineObserved = false;
        Unsubscribe();
        Debug.Log(State.Phase == MissionContentPhase.Ready
            ? "[NOVR] Additional content ready; mission launch unlocked."
            : "[NOVR] Additional content did not complete; mission launch remains blocked.");
    }

    private static void Unsubscribe()
    {
        if (_source != null && _handler != null)
            _source.LogEvent -= _handler;
        _handler = null;
    }

    private static void Shutdown()
    {
        Unsubscribe();
        if (_quittingHook)
            Application.quitting -= Shutdown;
        _quittingHook = false;
        if (_harmony != null && _routine != null)
            _harmony.Unpatch(_routine, HarmonyPatchType.All, PatchId);
        _harmony = null;
    }
}
