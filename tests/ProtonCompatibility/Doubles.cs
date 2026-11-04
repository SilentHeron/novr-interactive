namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch(Type type, string method) : Attribute
    {
        public Type Type => type; public string Method => method;
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPrefix : Attribute
    {
    }
}
namespace UnityEngine
{
    public static class Debug
    {
        public static readonly List<string> Logs = new(), Warnings = new();
        public static void Log(object value) => Logs.Add(value.ToString()!);
        public static void LogWarning(object value) => Warnings.Add(value.ToString()!);
    }
}
namespace NOVR
{
    public sealed class ModConfiguration
    {
        public static readonly ModConfiguration Instance = new();
        public readonly Setting DisableProtonXInput = new();
        public sealed class Setting
        {
            public bool Value = true;
        }
    }
}
namespace Rewired.Platforms
{
    public enum WindowsStandalonePrimaryInputSource
    {
        RawInput, DirectInput, XInput, Unity, WindowsGamingInput
    }
}
namespace Rewired.Data
{
    public sealed class ConfigVars
    {
        public bool useXInput = true, useNativeMouse = true, useEnhancedDeviceSupport = true;
        public int updateLoop = 7;
        public Rewired.Platforms.WindowsStandalonePrimaryInputSource windowsStandalonePrimaryInputSource;
    }
    public sealed class UserData
    {
        public ConfigVars? ConfigVars = new();
        public readonly object SavedBindings = new();
    }
}
namespace Rewired
{
    public sealed class InputManager_Base
    {
        public Rewired.Data.UserData? userData = new();
        // The real native Awake/OnEnable consume this object before creating
        // a platform backend. Expose what that first creation would observe.
        public bool FirstPollUsesXInput() => userData!.ConfigVars!.useXInput ||
            userData.ConfigVars.windowsStandalonePrimaryInputSource == Platforms.WindowsStandalonePrimaryInputSource.XInput;
    }
    public static class ReInput
    {
        public static bool isReady;
        public static int RuntimeConfigurationReads, EventRegistrations, ResetCalls;
        public static event Action InitializedEvent
        {
            add
            {
                EventRegistrations++;
            }
            remove
            {
                EventRegistrations--;
            }
        }
        public static ConfigHelper configuration
        {
            get
            {
                RuntimeConfigurationReads++;
                return new ConfigHelper();
            }
        }
        public sealed class ConfigHelper
        {
            public bool useXInput
            {
                get => true; set
                {
                    ResetCalls++;
                    throw new InvalidOperationException("A runtime setter resets the active Rewired session");
                }
            }
        }
    }
}
