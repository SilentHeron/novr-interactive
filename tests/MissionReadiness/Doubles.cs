using System.Collections;
using System.Reflection;

// Portable adapter-boundary doubles. They do not simulate the Unity player or
// assert Harmony's patch machinery; the runtime build/optional local check does.
public class Encyclopedia { }
namespace UnityEngine
{
    public static class Debug
    {
        public static void Log(string value) { }
        public static void LogWarning(string value) { }
    }
    public static class Application
    {
        public static event Action? quitting;
        public static void Quit() => quitting?.Invoke();
        public static int QuitHandlers => quitting?.GetInvocationList().Length ?? 0;
    }
}
namespace BepInEx.Logging
{
    public enum LogLevel { Info, Error, Warning, Debug }
    public sealed class LogEventArgs(object? data, LogLevel level, ManualLogSource source) : EventArgs
    {
        public object? Data => data;
        public LogLevel Level => level;
        public ManualLogSource Source => source;
    }
    public sealed class ManualLogSource
    {
        public event EventHandler<LogEventArgs>? LogEvent;
        public int Subscribers => LogEvent?.GetInvocationList().Length ?? 0;
        public void Emit(string message, LogLevel level = LogLevel.Info, ManualLogSource? claimedSource = null) =>
            LogEvent?.Invoke(this, new LogEventArgs(message, level, claimedSource ?? this));
    }
}
namespace BepInEx.Bootstrap
{
    public sealed class PluginMetadata { public Version Version = new(2, 0, 1); }
    public sealed class PluginInfo
    {
        public object? Instance;
        public PluginMetadata Metadata = new();
    }
    public static class Chainloader { public static Dictionary<string, PluginInfo> PluginInfos = new(); }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch(Type type, string name, Type[] parameters) : Attribute
    {
        public Type Type = type;
        public string Name = name;
        public Type[] Parameters = parameters;
    }
    public sealed class HarmonyMethod(Type type, string name)
    {
        public MethodInfo Method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
    }
    public enum HarmonyPatchType { All }
    public sealed class Harmony(string id)
    {
        public static int Patches;
        public static MethodInfo? Target;
        public void Patch(MethodInfo target, HarmonyMethod prefix, HarmonyMethod postfix, HarmonyMethod finalizer)
        { Patches++; Target = target; }
        public void Unpatch(MethodInfo target, HarmonyPatchType kind, string owner)
        { if (owner != id) throw new Exception("Wrong unpatch owner"); Patches--; }
    }
}
namespace Blueprinter
{
    public class Plugin
    {
        public static BepInEx.Logging.ManualLogSource Log { get; } = new();
        public IEnumerator RunRoutine(Encyclopedia instance) => Array.Empty<object>().GetEnumerator();
    }
    public static class EncyclopediaAfterLoadPatch
    {
        // The production adapter reads the private field; the helper only sets it.
        private static bool RunOnce;
        public static bool Started { get => RunOnce; set => RunOnce = value; }
    }
}
