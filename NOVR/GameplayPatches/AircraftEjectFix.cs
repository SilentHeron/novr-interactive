using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR.HarmonyPatches;

internal static class AircraftEjectFix
{
    [HarmonyPatch(typeof(PilotDismounted), "Setup")]
    private static class SetupPatch
    {
        [HarmonyPostfix]
        private static void Postfix(PilotDismounted __instance)
        {
            var camera = SceneSingleton<CameraStateManager>.i;
            if (!XRSettings.enabled || camera == null || __instance.pilotNumber != 0 ||
                !UnitRegistry.TryGetUnit<Aircraft>(__instance.parentUnit, out var aircraft) ||
                camera.followingUnit != aircraft) return;
            __instance.StartCoroutine(ApplyCameraPivot(__instance, aircraft));
        }

        private static IEnumerator ApplyCameraPivot(PilotDismounted pilot, Aircraft aircraft)
        {
            // Setup is asynchronous: the native camera is reparented after the
            // next physics update. Its temporary absence is not an exception.
            var deadline = Time.unscaledTime + 2f;
            while (pilot != null && Time.unscaledTime < deadline)
            {
                var camera = SceneSingleton<CameraStateManager>.i;
                if (camera == null || (camera.followingUnit != aircraft && camera.followingUnit != pilot)) yield break;
                var head = pilot.transform.Find("pilot/pilot_armature/pelvis/chest/neck/head");
                var helmet = head != null ? head.Find("helmetCamPoint") : null;
                if (camera.followingUnit == pilot && camera.cameraPivot != null &&
                    camera.cameraPivot.IsChildOf(pilot.transform) && helmet != null)
                {
                    camera.cameraPivot.SetPositionAndRotation(helmet.position, helmet.rotation);
                    head!.gameObject.SetActive(false);
                    Debug.Log("[NOVR] Ejection camera attached to the local pilot's helmet viewpoint.");
                    yield break;
                }
                yield return null;
            }
            if (pilot != null)
                Debug.LogWarning("[NOVR] Helmet viewpoint unavailable after ejection; keeping the game's camera placement.");
        }
    }
}
