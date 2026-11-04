namespace BepInEx.Configuration
{
    public sealed class ConfigFile
    {
        public Action<int>? BeforeBind;
        public int BindCount { get; private set; }
        public ConfigEntry<T> Bind<T>(string section, string key, T value, string description) => Bind(value);
        public ConfigEntry<T> Bind<T>(string section, string key, T value, ConfigDescription description) => Bind(value);
        private ConfigEntry<T> Bind<T>(T value)
        {
            BindCount++;
            BeforeBind?.Invoke(BindCount);
            return new ConfigEntry<T>(value);
        }
    }
    public sealed class ConfigEntry<T>(T value) { public T Value { get; set; } = value; }
    public sealed class ConfigDescription(string description, object? values = null)
    {
        public string Description { get; } = description;
        public object? AcceptableValues { get; } = values;
    }
    public sealed class AcceptableValueRange<T>(T minimum, T maximum)
    {
        public T Minimum { get; } = minimum;
        public T Maximum { get; } = maximum;
    }
}

namespace NOVR.MotionControls
{
    using BepInEx.Configuration;
    public sealed class MotionControlSettings
    {
        public MotionControlSettings(ConfigFile config) => config.Bind("Motion", "Test boundary", true, "");
    }
    public sealed class ControllerBindings
    {
        public ControllerBindings(ConfigFile config) => config.Bind("Bindings", "Test boundary", true, "");
    }
    public sealed class GazeCueSettings
    {
        public GazeCueSettings(ConfigFile config) => config.Bind("Gaze", "Test boundary", true, "");
    }
    public enum TargetCueMode { Context, Always, Off }
}

namespace NOVR.VrUi.SpecialBehavior
{
    using BepInEx.Configuration;
    public sealed class CockpitAlertSettings
    {
        public CockpitAlertSettings(ConfigFile config) => config.Bind("Alerts", "Test boundary", true, "");
    }
}
