using System.Reflection;
using System.Reflection.Emit;
using UnityEngine.UI;
using UnityEngine;

namespace HarmonyLib
{
    // Exact field-ref lookup and attribute shapes only; no Harmony dispatcher.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class HarmonyPatch(Type type, string method) : Attribute
    {
        public Type Type => type; public string Method => method;
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPrefix : Attribute
    {
    }
    public static class AccessTools
    {
        public delegate ref TField FieldRef<T, TField>(T instance);
        public static FieldRef<T, TField> FieldRefAccess<T, TField>(string name)
        {
            var field = typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
            var method = new DynamicMethod("FixtureFieldRef", typeof(TField).MakeByRefType(), new[] { typeof(T) }, typeof(T), true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldflda, field);
            il.Emit(OpCodes.Ret);
            return (FieldRef<T, TField>)method.CreateDelegate(typeof(FieldRef<T, TField>));
        }
    }
}

public class Unit
{
    public bool disabled;
    public FactionHQ? NetworkHQ;
}
public sealed class Aircraft : Unit
{
    public WeaponManager? weaponManager = new();
    public Cockpit? cockpit = new();
}
public sealed class Missile : Unit
{
}
public sealed class FactionHQ
{
}
public enum FactionMode
{
    NoFaction = 0, Spectator = 1, Friendly = 2, Enemy = 4
}
public sealed class Cockpit
{
    public bool Detached;
    public bool IsDetached() => Detached;
}
public sealed class WeaponManager
{
    public readonly List<Unit> Targets = new();
    public List<Unit> GetTargetList() => Targets;
}
public sealed class HUDUnitMarker
{
    public Unit? unit;
    public bool selected;
    public Image? image;
}
public sealed class CombatHUD
{
    public Aircraft? aircraft = new();
    public Image targetDesignator = null!;
    private readonly List<HUDUnitMarker> markers = new();
    public readonly List<Unit> Targets = new();
    public void Add(params HUDUnitMarker[] values) => markers.AddRange(values);
    public List<Unit> GetTargetList() => Targets;
    public readonly List<Unit> SelectedCalls = new();
    // Native endpoint boundary: preserve its immediate marker + manager state,
    // but do not simulate native sound, map icons or networking.
    public void SelectUnit(Unit? unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        SelectedCalls.Add(unit);
        markers.First(marker => marker.unit == unit).selected = true;
        aircraft!.weaponManager!.Targets.Insert(0, unit);
    }
}
public sealed class TargetListSelector
{
    public readonly HashSet<Unit> Excluded = new();
    public bool CheckExclusions(Unit unit) => Excluded.Contains(unit);
}
public static class SceneSingleton<T> where T : class
{
    public static T? i;
}

public enum CameraMode
{
    cockpit, orbit
}
public static class CameraStateManager
{
    public static CameraMode cameraMode = CameraMode.cockpit;
}
public static class DynamicMap
{
    public static bool mapMaximized;
    // Native faction classification boundary: the local HQ's allies are
    // friendly, anything else with a faction is an enemy.
    public static readonly HashSet<FactionHQ> FriendlyFactions = new();
    public static FactionMode GetFactionMode(FactionHQ? hq) =>
        hq == null ? FactionMode.NoFaction : FriendlyFactions.Contains(hq) ? FactionMode.Friendly : FactionMode.Enemy;
}
namespace UnityEngine.XR
{
    public static class XRSettings
    {
        public static bool enabled = true;
    }
}
namespace NOVR
{
    public static class APIBus
    {
        public static Camera? CockpitHudCamera;
    }
}
namespace NOVR.VrUi
{
    public class NOUIManager
    {
        public static NOUIManager? I = new();
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    public class UIRenderedCanvasBehavior : MonoBehaviour
    {
    }
}
namespace NOVR.MotionControls
{
    public static class FlightControlMath
    {
        public static bool IsFinite(float value) => float.IsFinite(value);
    }
}

namespace NOVR.VrUi.HarmonyPatches
{
    // Projection rendering is outside this fixture. The real designator uses
    // this unchanged production projection distance; candidate geometry is real.
    internal static class VrHudProjection
    {
        internal const float HudDistance = 1000;
    }
}
