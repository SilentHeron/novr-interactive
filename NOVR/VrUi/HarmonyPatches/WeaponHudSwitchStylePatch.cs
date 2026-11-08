using HarmonyLib;
using NOVR.VrUi.SpecialBehavior;

namespace NOVR.VrUi.HarmonyPatches;

// Each weapon switch replaces the weapon widget with a new native-size prefab.
// Style it as soon as the game creates it; the mesh effect then applies before
// the widget is first drawn, so no full-size targeting symbol flashes.
[HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.ShowWeaponStation))]
internal static class WeaponHudSwitchStylePatch
{
    [HarmonyPostfix]
    private static void Postfix(HUDWeaponState ___weaponState)
    {
        if (TargetingHudStyle.Styling && ___weaponState != null)
            TargetingHudStyle.StyleWeaponState(___weaponState);
    }
}
