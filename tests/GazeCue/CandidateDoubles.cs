using System.Reflection;
using System.Reflection.Emit;
using UnityEngine.UI;

namespace HarmonyLib
{
    // Only the field-ref lookup used by TargetCueCandidates; no patch dispatcher.
    public static class AccessTools
    {
        public delegate ref TField FieldRef<T, TField>(T instance);
        public static FieldRef<T, TField> FieldRefAccess<T, TField>(string name)
        {
            var field = typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
            var method = new DynamicMethod("FixtureFieldRef", typeof(TField).MakeByRefType(), new[] { typeof(T) }, typeof(T), true);
            var il = method.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldflda, field); il.Emit(OpCodes.Ret);
            return (FieldRef<T, TField>)method.CreateDelegate(typeof(FieldRef<T, TField>));
        }
    }
}

public class Unit { public bool disabled; }
public sealed class Aircraft : Unit { public WeaponManager? weaponManager = new(); }
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
}
public sealed class TargetListSelector
{
    public readonly HashSet<Unit> Excluded = new();
    public bool CheckExclusions(Unit unit) => Excluded.Contains(unit);
}
public static class SceneSingleton<T> where T : class { public static T? i; }
