using System.Collections.Generic;
using HarmonyLib;
using NOVR.VrUi.SpecialBehavior;
using NuclearOption.UIStyleSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HudView = NOVR.VrUi.HarmonyPatches.VrHudProjection.HudView;

namespace NOVR.VrUi.HarmonyPatches;


// Ensures our hud markers are in our VR UI camera's space.
//
// The game calls UpdatePosition for every unit marker on every frame, so this
// is the mod's hottest patch. Private fields are read through field-reference
// delegates (no reflection or boxing), and everything that is the same for all
// markers in one pass (both camera poses, the shared target-info label) is
// done once per CombatHUD.UpdateMarkers call instead of once per marker.
internal static class HUDUnitMarkerViewPositionPatch
{
    private static readonly AccessTools.FieldRef<global::HUDUnitMarker, bool> Hidden =
        AccessTools.FieldRefAccess<global::HUDUnitMarker, bool>("hidden");
    private static readonly AccessTools.FieldRef<global::HUDUnitMarker, Transform> MarkerTransform =
        AccessTools.FieldRefAccess<global::HUDUnitMarker, Transform>("_transform");
    private static readonly AccessTools.FieldRef<global::HUDUnitMarker, Sprite> Icon =
        AccessTools.FieldRefAccess<global::HUDUnitMarker, Sprite>("icon");
    private static readonly AccessTools.FieldRef<global::HUDUnitMarker, float> TimeCreated =
        AccessTools.FieldRefAccess<global::HUDUnitMarker, float>("timeCreated");
    private static readonly AccessTools.FieldRef<global::HUDUnitMarker, Color> MarkerColor =
        AccessTools.FieldRefAccess<global::HUDUnitMarker, Color>("color");
    private static readonly AccessTools.FieldRef<global::HUDUnitMarker, bool> Flashing =
        AccessTools.FieldRefAccess<global::HUDUnitMarker, bool>("flashing");
    private static readonly AccessTools.FieldRef<global::CombatHUD, Image> TargetArrow =
        AccessTools.FieldRefAccess<global::CombatHUD, Image>("targetArrow");
    private static readonly AccessTools.FieldRef<global::CombatHUD, Transform> TargetArrowTail =
        AccessTools.FieldRefAccess<global::CombatHUD, Transform>("targetArrowTail");
    private static readonly AccessTools.FieldRef<global::CombatHUD, TextMeshProUGUI> TargetText =
        AccessTools.FieldRefAccess<global::CombatHUD, TextMeshProUGUI>("targetText");
    private static readonly AccessTools.FieldRef<global::CombatHUD, TextMeshProUGUI> TargetInfo =
        AccessTools.FieldRefAccess<global::CombatHUD, TextMeshProUGUI>("targetInfo");
    private static readonly AccessTools.FieldRef<global::CombatHUD, List<global::HUDUnitMarker>> Markers =
        AccessTools.FieldRefAccess<global::CombatHUD, List<global::HUDUnitMarker>>("markers");

    // State of the CombatHUD.UpdateMarkers call in progress, if any.
    private static bool _pass, _passHasView;
    private static HudView _passView;
    private static global::CombatHUD? _passHud;

    // Markers whose image already carries the size style, so the component
    // lookup happens once per marker instead of on every frame. Keyed by the
    // marker with the styled image as value: a replaced image is styled again.
    private static readonly Dictionary<global::HUDUnitMarker, Image> Styled = new();
    private static global::CombatHUD? _styledOwner;

    private static void EnsureStyled(global::HUDUnitMarker marker)
    {
        var image = marker.image;
        if (Styled.TryGetValue(marker, out var styled) && ReferenceEquals(styled, image))
            return;
        HudElementStyle.Add(image, HudElementKind.Marker);
        if (image is not null)
            Styled[marker] = image;
    }

    // Removed markers would otherwise keep their units alive through this map.
    private static void PruneStyled(global::CombatHUD hud)
    {
        if (!ReferenceEquals(_styledOwner, hud))
        {
            _styledOwner = hud;
            Styled.Clear();
            return;
        }
        var markers = Markers(hud);
        if (markers == null || Styled.Count <= markers.Count + 64)
            return;
        Styled.Clear();
    }

    private static void RotateTargetInfo(global::CombatHUD? hud, Quaternion rotation)
    {
        if (hud is null)
            return;
        var targetInfo = TargetInfo(hud);
        if (targetInfo != null)
            targetInfo.transform.rotation = rotation;
    }

    [HarmonyPatch(typeof(global::CombatHUD), "UpdateMarkers")]
    private static class UpdateMarkersPatch
    {
        [HarmonyPrefix]
        private static void Prefix(global::CombatHUD __instance)
        {
            _passHud = SceneSingleton<global::CombatHUD>.i;
            _passHasView = HudView.TryCapture(out _passView);
            if (_passHasView)
                RotateTargetInfo(_passHud, _passView.HudRotation);
            PruneStyled(__instance);
            _pass = true;
        }

        // A finalizer also runs when the game's loop throws, so a failed frame
        // cannot leave a stale view behind for later calls.
        [HarmonyFinalizer]
        private static void Finalizer()
        {
            _pass = false;
            _passHud = null;
        }
    }

    [HarmonyPatch(typeof(global::HUDUnitMarker), nameof(global::HUDUnitMarker.UpdatePosition))]
    private static class UpdatePositionPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::HUDUnitMarker __instance, FactionHQ hq, global::GlobalPosition viewPosition, Vector3 cameraForward)
        {
            EnsureStyled(__instance);
            HudView view;
            global::CombatHUD? hud;
            if (_pass)
            {
                if (!_passHasView)
                    return true;
                view = _passView;
                hud = _passHud;
            }
            else
            {
                // Outside the per-frame pass (a marker positions itself once
                // when it is created): same work, for this one call.
                if (!HudView.TryCapture(out view))
                    return true;
                hud = SceneSingleton<global::CombatHUD>.i;
                RotateTargetInfo(hud, view.HudRotation);
            }

            var markerTransform = MarkerTransform(__instance);
            markerTransform.rotation = view.HudRotation;

            if (Hidden(__instance))
                return false;

            GlobalPosition knownPosition = __instance.unit.GlobalPosition();
            if (__instance.outdated && !hq.TryGetKnownPosition(__instance.unit, out knownPosition))
                return false;

            var knownWorldPosition = knownPosition.ToLocalPosition();
            if (__instance.selected)
            {
                if (view.PinToScreenEdge(knownWorldPosition, out var rayToScreen, out _))
                {
                    __instance.image.enabled = false;
                    if (view.TryProjectDirection(knownWorldPosition, out var targetHudPosition))
                        SetTargetArrow(hud, true, rayToScreen, targetHudPosition, view);
                }
                else
                {
                    __instance.image.enabled = true;
                    if (view.TryProject(knownWorldPosition, out var targetHudPosition))
                        markerTransform.position = targetHudPosition;
                    SetTargetArrow(hud, false, Vector3.zero, Vector3.zero, view);
                }

                if (!__instance.unit.HasRadarEmission())
                    return false;

                if (__instance.unit.radar is Radar radar && radar.IsJammed())
                {
                    if (__instance.image.sprite == GameAssets.i.targetUnitSpriteJammed)
                        return false;
                    __instance.image.sprite = GameAssets.i.targetUnitSpriteJammed;
                }
                else
                {
                    if (__instance.image.sprite != GameAssets.i.targetUnitSpriteJammed)
                        return false;
                    __instance.image.sprite = DynamicMap.GetFactionMode(__instance.unit.NetworkHQ) == FactionMode.Friendly
                        ? GameAssets.i.targetUnitSpriteFriendly
                        : Icon(__instance);
                }
            }
            else if (Vector3.Dot(knownWorldPosition - view.MainPosition, view.MainForward) < 0.0f)
            {
                if (!__instance.image.enabled)
                    return false;
                __instance.image.enabled = false;
            }
            else
            {
                if (!__instance.image.enabled)
                    __instance.image.enabled = true;
                if (view.TryProject(knownWorldPosition, out var targetHudPosition))
                    markerTransform.position = targetHudPosition;

                if (__instance.fresh)
                {
                    var markerColor = MarkerColor(__instance);
                    var warningColor = ThemeManager.Active.ColorTheme.Warning;
                    var t = Time.timeSinceLevelLoad - TimeCreated(__instance);
                    __instance.image.color = Color.Lerp(markerColor + warningColor, markerColor, t);
                    if (t > 1.0f)
                        __instance.fresh = false;
                }

                if (!Flashing(__instance))
                    return false;
                var flashingColor = MarkerColor(__instance);
                var flashingWarningColor = ThemeManager.Active.ColorTheme.Warning;
                __instance.image.color = Color.Lerp(
                    flashingColor + flashingWarningColor,
                    flashingColor,
                    Mathf.Sin(Time.timeSinceLevelLoad * 20f) + 0.5f);
            }

            return false;
        }



        // Only the selected marker reaches this. "Up" for the arrow is the
        // direction from its screen-edge position to the target's projection.
        private static void SetTargetArrow(global::CombatHUD? instance, bool enabled, Vector3 position, Vector3 targetPosition, in HudView view)
        {
            if (instance is null)
                return;
            Behaviour targetArrow = TargetArrow(instance);
            var targetArrowTail = TargetArrowTail(instance);
            Behaviour targetText = TargetText(instance);
            if (targetArrow == null || targetArrowTail == null || targetText == null)
                return;

            targetArrow.enabled = enabled;
            targetText.enabled = enabled;
            targetText.transform.rotation = view.HudRotation;
            if (!enabled)
                return;

            targetArrow.transform.position = position;
            targetText.transform.position = targetArrowTail.position;
            var desiredUp = targetPosition - position;
            if (desiredUp.sqrMagnitude <= Mathf.Epsilon)
                desiredUp = targetArrow.transform.up;
            desiredUp.Normalize();

            // The arrow faces the pilot: forward is the HUD camera's forward,
            // made perpendicular to the chosen up direction.
            var hudForward = view.HudRotation * Vector3.forward;
            var desiredForward = Vector3.ProjectOnPlane(hudForward, desiredUp);
            if (desiredForward.sqrMagnitude <= Mathf.Epsilon)
                desiredForward = Vector3.Cross(desiredUp, view.HudRotation * Vector3.right);

            targetArrow.transform.rotation = Quaternion.LookRotation(desiredForward.normalized, desiredUp);
        }
    }

}
