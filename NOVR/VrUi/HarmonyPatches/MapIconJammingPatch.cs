using HarmonyLib;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR.VrUi.HarmonyPatches;

// While the player's radar is jammed the game shakes every unit icon on the
// map: each frame it adds a random offset of up to 2.5 to the icon's world
// position, and an icon is only put back on its unit every few frames. On a
// flat screen a world unit is a pixel, so the icons shiver by a few pixels.
// In VR the map is a panel in the cockpit and a world unit is a metre: the
// icons leave the map and fly metres around the cockpit, through the pilot's
// view, as large flickering red and blue shapes. Apply the same shake in the
// map's plane and in the map's own units instead, so it stays a shiver.
[HarmonyPatch(typeof(UnitMapIcon), nameof(UnitMapIcon.JammingDistortion))]
internal static class UnitMapIconJammingPatch
{
    [HarmonyPrefix]
    private static bool Prefix(UnitMapIcon __instance, float jammingStrength)
    {
        if (!XRSettings.enabled)
            return true;
        var image = __instance.iconImage;
        if (image == null)
            return false;
        var strength = Mathf.Min(jammingStrength, 4f);
        var icon = image.transform;
        var layer = icon.parent;
        if (layer != null)
        {
            // One map unit in the world: the map frame's scale, which does not
            // change with the map's zoom (the icon layer's does).
            var map = SceneSingleton<DynamicMap>.i;
            var frame = map != null && map.mapBackground != null ? map.mapBackground.transform : layer;
            var noise = Random.insideUnitSphere * strength * 5f / 2f;
            HudPresentationMath.MapJamOffset(noise.x, noise.y, frame.lossyScale.x, out var x, out var y);
            icon.position += layer.rotation * new Vector3(x, y, 0f);
        }
        var color = image.color;
        color.a = 1f - strength * .7f;
        image.color = color;
        return false;
    }
}
