using HarmonyLib;
using NOVR.VrUi.SpecialBehavior;
using TMPro;

namespace NOVR.VrUi.HarmonyPatches;

// Unselected friendly aircraft use a separate native hover label, outside
// CombatHUD's selected-target text. Keep the game's hover/visibility rules;
// style only the generated lettering so it follows the normal VR label slider.
[HarmonyPatch(typeof(AllyInfo), "LateUpdate")]
internal static class AircraftLabelStylePatch
{
    [HarmonyPostfix]
    private static void Postfix(TextMeshProUGUI ___hoveredAllyInfo, Aircraft? ___hoveredAlly)
    {
        var style = HudElementStyle.Add(___hoveredAllyInfo, HudElementKind.Label);
        if (style != null) style.SetAircraftLabelAffiliation(TargetingHudStyle.LabelAffiliation(___hoveredAlly));
    }
}
