namespace BepInEx.Configuration
{
    public sealed class ConfigFile
    {
        public Action<int>? BeforeBind;
        public int BindCount
        {
            get; private set;
        }
        private readonly Dictionary<(string Section, string Key), object> _entries = new();
        public List<string> BoundKeys { get; } = new();
        public void Seed<T>(string section, string key, T value) => _entries[(section, key)] = new ConfigEntry<T>(value);
        public ConfigEntry<T> Bind<T>(string section, string key, T value, string description) => Bind(section, key, value);
        public ConfigEntry<T> Bind<T>(string section, string key, T value, ConfigDescription description) => Bind(section, key, value);
        private ConfigEntry<T> Bind<T>(string section, string key, T value)
        {
            BindCount++;
            BoundKeys.Add(key);
            BeforeBind?.Invoke(BindCount);
            if (_entries.TryGetValue((section, key), out var entry))
                return (ConfigEntry<T>)entry;
            var created = new ConfigEntry<T>(value);
            _entries.Add((section, key), created);
            return created;
        }
    }
    public sealed class ConfigEntry<T>(T value)
    {
        public T Value { get; set; } = value;
    }
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
    public sealed class ControllerBindings
    {
        public ControllerBindings(ConfigFile config) => config.Bind("Bindings", "Test boundary", true, "");
    }
    public sealed class GazeCueSettings
    {
        public GazeCueSettings(ConfigFile config) => config.Bind("Gaze", "Test boundary", true, "");
    }
    public enum TargetCueMode
    {
        Context, Always, Off
    }
}

namespace NOVR.VrUi.SpecialBehavior
{
    using BepInEx.Configuration;
    public sealed class CockpitAlertSettings
    {
        public CockpitAlertSettings(ConfigFile config) => config.Bind("Alerts", "Test boundary", true, "");
    }
}

namespace UnityEngine
{
    public struct Vector3(float x, float y, float z)
    {
        public float x = x, y = y, z = z;
        public static Vector3 zero => default;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static bool operator ==(Vector3 a, Vector3 b)
        {
            var d = a - b;
            return d.x * d.x + d.y * d.y + d.z * d.z < 1e-10f;
        }
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object? other) => other is Vector3 value && this == value;
        public override int GetHashCode() => HashCode.Combine(x, y, z);
    }
    public static class Mathf
    {
        public static float Abs(float value) => MathF.Abs(value);
    }
}
