// Exercise the production profile/migration code with stored configuration values.
// These doubles do not simulate BepInEx file I/O or Unity rendering/tracking.
namespace BepInEx.Configuration
{
    public sealed class ConfigEntry<T>(T value)
    {
        private T _value = value;
        public int Writes;
        public T Value
        {
            get => _value; set
            {
                _value = value;
                Writes++;
            }
        }
        public object DefaultValue = value!;
    }
    public sealed class ConfigFile
    {
        private readonly Dictionary<(string, string), object> _entries = new();
        public ConfigEntry<T> Bind<T>(string section, string key, T value, string description)
        {
            if (!_entries.TryGetValue((section, key), out var entry))
                _entries[(section, key)] = entry = new ConfigEntry<T>(value);
            return (ConfigEntry<T>)entry;
        }
        public void Save()
        {
        }
    }
}
namespace UnityEngine
{
    public sealed class DefaultExecutionOrder(int order) : Attribute
    {
        public int Order => order;
    }
    public partial class MonoBehaviour
    {
    }
    public partial struct Vector3(float x, float y, float z) : IEquatable<Vector3>
    {
        public float x = x, y = y, z = z;
        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static bool operator ==(Vector3 a, Vector3 b) => a.Equals(b);
        public static bool operator !=(Vector3 a, Vector3 b) => !a.Equals(b);
        public bool Equals(Vector3 v) => x == v.x && y == v.y && z == v.z;
        public override bool Equals(object? value) => value is Vector3 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
    }
    public static class Mathf
    {
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
    }
}
public enum CameraMode
{
    cockpit, external
}
public static class CameraStateManager
{
    public static CameraMode cameraMode = CameraMode.cockpit;
}
public sealed class AircraftDefinition
{
    public string jsonKey = "SmallFighter1", name = "FS20", unitName = "FS-20";
}
public sealed partial class Aircraft
{
    public AircraftDefinition definition = new(); public bool Collective;
}
public static partial class GameManager
{
    public static Aircraft? Current;
    public static bool GetLocalAircraft(out Aircraft aircraft)
    {
        aircraft = Current!;
        return aircraft != null;
    }
}
namespace NOVR
{
    public sealed class ModConfiguration
    {
        public static ModConfiguration Instance = new();
        public readonly BepInEx.Configuration.ConfigFile Config = new();
        public readonly MotionControls.MotionControlSettings MotionControls = new();
    }
}
namespace NOVR.MotionControls
{
    using UnityEngine;
    using BepInEx.Configuration;
    public sealed partial class MotionControlSettings
    {
        public ConfigEntry<bool> LeftHanded = new(false);
        public ConfigEntry<Vector3> StickPosition = new(new(.22f, -.36f, .32f));
        public ConfigEntry<Vector3> ThrottlePosition = new(new(-.24f, -.36f, .32f));
    }
    public sealed partial class MotionFlightController
    {
        public static MotionFlightController? Instance = new();
        public int Resets;
        public void ResetGrips() => Resets++;
        public static bool UsesLiftCollective(Aircraft? aircraft) => aircraft?.Collective == true;
    }
    public static partial class TrackedControllers
    {
        public static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
