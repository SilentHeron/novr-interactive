using HarmonyLib;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.HarmonyPatches;

// GridLabels.Maximize(false) moves only gridAircraft to the original HUD map
// anchor, outside the map borrowed by the cockpit widget. Native LateUpdate
// re-enables it every frame; suppress that detached readout after its update.
[HarmonyPatch(typeof(GridLabels), "LateUpdate")]
internal static class GridAircraftReadoutPatch
{
    [HarmonyPostfix]
    internal static void Postfix(Text ___gridAircraft)
    {
        if (!XRSettings.enabled || DynamicMap.mapMaximized || ___gridAircraft == null)
            return;
        var map = SceneSingleton<DynamicMap>.i;
        if (map != null && map.hudMapAnchor != null && ___gridAircraft.transform.parent == map.hudMapAnchor)
            ___gridAircraft.enabled = false;
        // Full-map and desktop visibility remain owned by native LateUpdate,
        // including its aircraft-null and show-grid-labels conditions. The
        // tooltip and map's major/minor grid labels are never touched here.
    }
}
