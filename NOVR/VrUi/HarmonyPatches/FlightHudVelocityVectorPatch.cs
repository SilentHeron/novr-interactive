using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

internal static class FlightHudVelocityVectorPatch
{
    private const float VelocityVectorProjectionDistance = 1000.0f;
    private static readonly AccessTools.FieldRef<global::FlightHud, Transform> CockpitTransform =
        AccessTools.FieldRefAccess<global::FlightHud, Transform>("cockpitTransform");
    private static readonly AccessTools.FieldRef<global::FlightHud, Rigidbody> CockpitRb =
        AccessTools.FieldRefAccess<global::FlightHud, Rigidbody>("cockpitRB");

    [HarmonyPatch(typeof(global::FlightHud), "Update")]
    private static class UpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::FlightHud __instance)
        {
            var velocityVector = __instance.velocityVector;
            if (velocityVector == null)
                return;

            var cockpitTransform = CockpitTransform(__instance);
            var cockpitRb = CockpitRb(__instance);
            if (cockpitTransform == null || cockpitRb == null)
                return;

            var velocity = cockpitRb.velocity;
            velocityVector.gameObject.SetActive(velocity.magnitude > 10.0f);
            if (!velocityVector.gameObject.activeSelf)
                return;

            var mainCamera = APIBus.MainCamera;
            var cockpitHudCamera = APIBus.CockpitHudCamera;
            if (mainCamera == null || cockpitHudCamera == null)
                return;

            var velocityWorldPosition = cockpitTransform.position + velocity * VelocityVectorProjectionDistance;
            if (!VrHudProjection.TryProjectToCockpitHud(velocityWorldPosition, out var hudPosition))
            {
                velocityVector.enabled = false;
                return;
            }

            velocityVector.enabled = true;
            velocityVector.transform.position = hudPosition;
            velocityVector.transform.forward = cockpitHudCamera.transform.forward;
        }
    }
}
