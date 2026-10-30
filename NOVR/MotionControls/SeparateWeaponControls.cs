using System.Collections.Generic;
using HarmonyLib;

namespace NOVR.MotionControls;

// The game's normal Pilot.Fire path has already applied pilot strength, menu
// safety and cockpit checks. Split only the local VR request here, preserving
// selection, target lists, native station safety, reloads and network commands.
[HarmonyPatch(typeof(WeaponManager), nameof(WeaponManager.Fire))]
internal static class SeparateWeaponControls
{
    [HarmonyPrefix]
    private static bool Prefix(WeaponManager __instance, List<Unit> ___targetList)
    {
        var actions = ControllerActions.Instance;
        if (actions == null || !(actions.GunsHeld || actions.WeaponReleaseHeld) ||
            !GameManager.GetLocalAircraft(out var aircraft) || aircraft.weaponManager != __instance) return true;

        var selectedGun = __instance.currentWeaponStation != null && __instance.currentWeaponStation.WeaponInfo.gun;
        if (selectedGun) return actions.GunsHeld;

        if (actions.GunsHeld && !aircraft.remoteSim)
        {
            var target = ___targetList.Count > 0 ? ___targetList[0] : null;
            foreach (var station in aircraft.weaponStations)
                if (station.WeaponInfo.gun && station.TurretCount() == 0 && !station.SafetyIsOn(aircraft) && station.Ready())
                    station.Fire(aircraft, target);
        }
        // A never fires guns. Non-gun weapons retain the game's ordinary
        // launch/lock/salvo behavior; holding trigger cannot launch a missile.
        return actions.WeaponReleaseHeld;
    }
}
