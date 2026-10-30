using HarmonyLib;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrCamera;

internal static class HudTurretCrosshairPatch
{
    [HarmonyPatch(typeof(HUDTurretCrosshair), "Refresh")]
    private static class Refresh
    {
        [HarmonyPrefix]
        private static bool Prefix(HUDTurretCrosshair __instance, ref Camera mainCamera, out Vector3 crosshairPosition)
        {
            Traverse traverse = Traverse.Create(__instance);
            var turret  = traverse.Field("turret").GetValue<Turret>();
            var circle = traverse.Field("circle").GetValue<Image>();
            var readinessCircle = traverse.Field("readinessCircle").GetValue<Image>();
            var crosshair = traverse.Field("crosshair").GetValue<Image>();
            HudElementStyle.Add(circle, HudElementKind.Reticle);
            HudElementStyle.Add(readinessCircle, HudElementKind.Reticle);
            HudElementStyle.Add(crosshair, HudElementKind.Reticle);
            var gun = traverse.Field("gun").GetValue<Gun>();
            
            
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
                if ((double) reloadProgress > 0.0)
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
                circle.enabled = onTarget && (double) reloadProgress <= 0.0;
            }
            else
                circle.enabled = onTarget;
            
            return false;
        }
    }
    
}