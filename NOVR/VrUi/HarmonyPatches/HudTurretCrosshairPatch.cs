using HarmonyLib;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrCamera;

internal static class HudTurretCrosshairPatch
{
    // Every crosshair refreshes each frame. Its fields are read through field
    // references; Harmony's Traverse allocated objects and used reflection
    // for each one.
    private static readonly AccessTools.FieldRef<HUDTurretCrosshair, Turret> CrosshairTurret =
        AccessTools.FieldRefAccess<HUDTurretCrosshair, Turret>("turret");
    private static readonly AccessTools.FieldRef<HUDTurretCrosshair, Image> Circle =
        AccessTools.FieldRefAccess<HUDTurretCrosshair, Image>("circle");
    private static readonly AccessTools.FieldRef<HUDTurretCrosshair, Image> ReadinessCircle =
        AccessTools.FieldRefAccess<HUDTurretCrosshair, Image>("readinessCircle");
    private static readonly AccessTools.FieldRef<HUDTurretCrosshair, Image> Crosshair =
        AccessTools.FieldRefAccess<HUDTurretCrosshair, Image>("crosshair");
    private static readonly AccessTools.FieldRef<HUDTurretCrosshair, Gun> CrosshairGun =
        AccessTools.FieldRefAccess<HUDTurretCrosshair, Gun>("gun");

    [HarmonyPatch(typeof(HUDTurretCrosshair), "Refresh")]
    private static class Refresh
    {
        [HarmonyPrefix]
        private static bool Prefix(HUDTurretCrosshair __instance, ref Camera mainCamera, out Vector3 crosshairPosition)
        {
            var turret = CrosshairTurret(__instance);
            var circle = Circle(__instance);
            var readinessCircle = ReadinessCircle(__instance);
            var crosshair = Crosshair(__instance);
            HudElementStyle.Add(circle, HudElementKind.Reticle);
            HudElementStyle.Add(readinessCircle, HudElementKind.Reticle);
            HudElementStyle.Add(crosshair, HudElementKind.Reticle);
            var gun = CrosshairGun(__instance);


            var direction = turret.GetDirection().normalized;
            var onTarget = turret.IsOnTarget();


            var crosshairDirection = APIBus.MainCamera.transform.InverseTransformDirection(direction);
            crosshairDirection = APIBus.CockpitHudCamera.transform.TransformDirection(crosshairDirection);

            crosshairPosition = APIBus.CockpitHudCamera.transform.position + crosshairDirection * 1000f;
            crosshair.gameObject.transform.position = crosshairPosition;
            crosshair.gameObject.transform.rotation = Quaternion.LookRotation(crosshairDirection);
            crosshair.enabled = true;

            if (gun != null)
            {
                float reloadProgress = gun.GetReloadProgress();
                if ((double)reloadProgress > 0.0)
                {
                    if (!readinessCircle.enabled)
                    {
                        readinessCircle.enabled = true;
                        crosshair.color = Color.red + Color.green * 0.5f;
                    }
                    readinessCircle.fillAmount = reloadProgress;
                }
                else if (readinessCircle.enabled)
                {
                    readinessCircle.enabled = false;
                    crosshair.color = Color.green;
                }
                circle.enabled = onTarget && (double)reloadProgress <= 0.0;
            }
            else
                circle.enabled = onTarget;

            return false;
        }
    }

}
