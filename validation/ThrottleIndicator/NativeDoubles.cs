// Source-linked threshold discovery with the same reflected field names as the
// installed game. These doubles do not simulate the engine or Unity lifecycle.
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static System.Reflection.FieldInfo? Field(Type type, string name) => type.GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
    }
}
public sealed class Aircraft
{
    public object[] engineStates = Array.Empty<object>();
    public AircraftParameters parameters = new();
    public AircraftParameters GetAircraftParameters() => parameters;
}
public sealed class AircraftParameters { public GaugeObject? HUDExtras; }
public sealed class GaugeObject
{
    public ThrottleGauge[] gauges = Array.Empty<ThrottleGauge>();
    public T[] GetComponentsInChildren<T>(bool inactive) => gauges.Cast<T>().ToArray();
}
public sealed class ThrottleGauge
{
    public bool afterburner = true;
    public GaugeRegion[] throttleRegions = Array.Empty<GaugeRegion>();
}
public sealed class GaugeRegion(float threshold) { public float start = threshold; }
public sealed class Turbojet { public JetNozzle[] nozzles = Array.Empty<JetNozzle>(); }
public sealed class JetNozzle { public Burner[] afterburners = Array.Empty<Burner>(); }
public sealed class Burner(float threshold, float maximum = 1, float force = 35000)
{
    public float throttleStart = threshold, throttleEnd = maximum, thrust = force;
}
