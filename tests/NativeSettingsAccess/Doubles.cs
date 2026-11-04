// The native preference/event APIs are boundaries only. All write/apply ordering
// and exception handling under test come from NativePlayerSettingsAccess.
internal static class NativeCalls
{
    internal static readonly List<string> Events = new();
    internal static readonly List<string> Warnings = new();
    internal static void Reset()
    {
        Events.Clear();
        Warnings.Clear();
        UnityEngine.PlayerPrefs.Values.Clear();
        UnityEngine.PlayerPrefs.FailSave = false;
        PlayerSettings.FailApply = false;
        PlayerSettings.FailLoad = false;
        PlayerSettings.OnApplyOptions = null;
        PlayerSettings.unitSystem = PlayerSettings.UnitSystem.Metric;
        PlayerSettings.cinematicMode = false;
        PlayerSettings.debugVis = false;
        SceneSingleton<HUDOptions>.i = null;
    }
}

internal static class PlayerSettings
{
    internal enum UnitSystem
    {
        Metric, Imperial
    }
    internal static UnitSystem unitSystem;
    internal static bool cinematicMode, debugVis;
    internal static bool FailApply, FailLoad;
    internal static Action? OnApplyOptions;

    internal static void ApplyPrefs()
    {
        NativeCalls.Events.Add("apply-player");
        if (FailApply)
            throw new InvalidOperationException("native apply unavailable");
        OnApplyOptions?.Invoke();
    }

    internal static void LoadPrefs()
    {
        NativeCalls.Events.Add("load");
        if (FailLoad)
            throw new InvalidOperationException("native preferences unavailable");
    }
}

internal static class SceneSingleton<T>
{
    internal static T? i;
}

internal sealed class HUDOptions
{
    internal bool FailApply;
    internal void ApplyHUDSettings()
    {
        NativeCalls.Events.Add("apply-hud");
        if (FailApply)
            throw new InvalidOperationException("native HUD unavailable");
    }
}

namespace UnityEngine
{
    internal static class PlayerPrefs
    {
        internal static readonly Dictionary<string, object> Values = new();
        internal static bool FailSave;
        internal static void SetInt(string key, int value)
        {
            NativeCalls.Events.Add("write-int:" + key);
            Values[key] = value;
        }

        internal static void SetFloat(string key, float value)
        {
            NativeCalls.Events.Add("write-float:" + key);
            Values[key] = value;
        }

        internal static void Save()
        {
            NativeCalls.Events.Add("save");
            if (FailSave)
                throw new IOException("preference save failed");
        }
    }

    internal static class Debug
    {
        internal static void LogWarning(object message) => NativeCalls.Warnings.Add(message.ToString()!);
    }
}
