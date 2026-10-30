using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

internal sealed class TargetingHudStyle : MonoBehaviour
{
    private float _nextScan;
    private void LateUpdate()
    {
        if (Time.unscaledTime < _nextScan) return;
        _nextScan = Time.unscaledTime + .5f;
        var combat = SceneSingleton<CombatHUD>.i;
        var flight = SceneSingleton<FlightHud>.i;
        if (flight != null)
        {
            HudElementStyle.Add(flight.velocityVector, HudElementKind.Reticle);
            HudElementStyle.Add(flight.waterline, HudElementKind.Reticle);
        }
        if (combat == null) return;
        HudElementStyle.Add(combat.targetDesignator, HudElementKind.Reticle);
        foreach (var name in new[] { "targetText", "targetInfo", "targetArrow" })
            if (AccessTools.Field(typeof(CombatHUD), name)?.GetValue(combat) is Graphic graphic)
                HudElementStyle.Add(graphic, graphic is TMP_Text ? HudElementKind.Label : HudElementKind.Marker);
        if (flight == null) return;
        foreach (var state in flight.GetComponentsInChildren<HUDWeaponState>(true))
        {
            var widget = state is HUDMissileState || state is HUDLaserGuidedState || state is HUDCargoState;
            if (widget)
            {
                // Scale every part around the authored widget origin, including
                // static labels/artwork that have no serialized script field.
                // Target-designator objects are outside this hierarchy.
                foreach (var graphic in state.GetComponentsInChildren<Graphic>(true))
                    // TMP's parent callback already styles every material mesh.
                    if (graphic is not TMP_SubMeshUI)
                        HudElementStyle.Add(graphic, HudElementKind.Weapon, state.transform);
                continue;
            }
            foreach (var field in state.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                void Register(Graphic g)
                {
                    // Shared target-designator graphics belong to the flight HUD.
                    if (!g.transform.IsChildOf(state.transform)) return;
                    var kind = g is TMP_Text ? HudElementKind.Label :
                        field.Name == "line" ? HudElementKind.HorizontalLine :
                        field.Name == "ccipLine" || field.Name == "alignmentBar" ? HudElementKind.VerticalLine : HudElementKind.Reticle;
                    HudElementStyle.Add(g, kind);
                }
                if (typeof(Graphic).IsAssignableFrom(field.FieldType)) { if (field.GetValue(state) is Graphic g) Register(g); }
                else if (typeof(Graphic[]).IsAssignableFrom(field.FieldType) && field.GetValue(state) is Graphic[] graphics)
                    foreach (var g in graphics) if (g != null) Register(g);
            }
        }
    }
}
