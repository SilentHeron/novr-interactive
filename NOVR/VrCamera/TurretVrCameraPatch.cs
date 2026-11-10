using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NOVR.VrCamera;

// Aims a manually controlled turret along the headset's view instead of the
// flat camera's.
//
// The game runs Turret.FixedUpdate at the physics rate for every turret that
// has a target, on every ship, vehicle and aircraft, and this prefix runs
// first each time. It used Harmony's Traverse, which allocates objects and
// makes reflection calls per field; with dozens of turrets engaging, that was
// thousands of allocations a second for turrets the player does not control.
// The fields are now read through field references, and the common case (a
// turret that is not under manual control) is decided by reading one bool.
internal static class TurretVrCameraPatch
{
    private static readonly AccessTools.FieldRef<global::Turret, bool> Manual =
        AccessTools.FieldRefAccess<global::Turret, bool>("manual");
    private static readonly AccessTools.FieldRef<global::Turret, global::Unit> Target =
        AccessTools.FieldRefAccess<global::Turret, global::Unit>("target");
    private static readonly AccessTools.FieldRef<global::Turret, global::Aircraft> TurretAircraft =
        AccessTools.FieldRefAccess<global::Turret, global::Aircraft>("aircraft");
    private static readonly AccessTools.FieldRef<global::Turret, global::Unit> AttachedUnit =
        AccessTools.FieldRefAccess<global::Turret, global::Unit>("attachedUnit");
    private static readonly AccessTools.FieldRef<global::Turret, float> LastVectorSent =
        AccessTools.FieldRefAccess<global::Turret, float>("lastVectorSent");
    private static readonly AccessTools.FieldRef<global::Turret, global::WeaponStation> CurrentWeaponStation =
        AccessTools.FieldRefAccess<global::Turret, global::WeaponStation>("currentWeaponStation");
    private static readonly AccessTools.FieldRef<global::Turret, Vector3> ManualVector =
        AccessTools.FieldRefAccess<global::Turret, Vector3>("manualVector");
    // Turret has two AimTurret overloads; this is the one taking a direction.
    private static readonly MethodInfo AimTurretMethod =
        AccessTools.Method(typeof(global::Turret), "AimTurret", new[] { typeof(Vector3) });
    private static readonly Action<global::Turret, Vector3> AimTurret =
        (Action<global::Turret, Vector3>)Delegate.CreateDelegate(typeof(Action<global::Turret, Vector3>), AimTurretMethod);

    [HarmonyPatch(typeof(global::Turret), "FixedUpdate")]
    private static class FixedUpdatePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::Turret __instance)
        {
            if (__instance == null || !Manual(__instance) || Target(__instance) != null)
            {
                return true;
            }

            var aircraft = TurretAircraft(__instance);
            var attachedUnit = AttachedUnit(__instance);
            if (aircraft == null ||
                attachedUnit == null ||
                !aircraft.LocalSim ||
                global::SceneSingleton<global::CameraStateManager>.i.currentState != global::SceneSingleton<global::CameraStateManager>.i.cockpitState)
            {
                return true;
            }

            var vrCamera = APIBus.MainCamera;
            if (vrCamera == null)
            {
                return true;
            }

            __instance.SetVector(vrCamera.transform.forward);

            if (Time.timeSinceLevelLoad - LastVectorSent(__instance) > 0.20000000298023224)
            {
                aircraft.SetTurretVector(CurrentWeaponStation(__instance).Number, ManualVector(__instance));
                LastVectorSent(__instance) = Time.timeSinceLevelLoad;
            }

            AimTurret(__instance, ManualVector(__instance));
            return false;
        }
    }
}
