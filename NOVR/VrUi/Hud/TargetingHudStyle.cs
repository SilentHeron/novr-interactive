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
    private static readonly FieldInfo TargetTextField = AccessTools.Field(typeof(CombatHUD), "targetText");
    private static readonly FieldInfo TargetInfoField = AccessTools.Field(typeof(CombatHUD), "targetInfo");
    private static readonly FieldInfo TargetArrowField = AccessTools.Field(typeof(CombatHUD), "targetArrow");
    private float _nextScan;
    private CombatHUD? _labelOwner;
    private HudElementStyle? _targetTextStyle, _targetInfoStyle;

    private void UpdateAircraftLabels(CombatHUD? combat)
    {
        if (_labelOwner != combat)
        {
            RestoreAircraftLabels();
            _labelOwner = combat;
            _targetTextStyle = combat == null ? null : HudElementStyle.Add(TargetTextField.GetValue(combat) as TMP_Text, HudElementKind.Label);
            _targetInfoStyle = combat == null ? null : HudElementStyle.Add(TargetInfoField.GetValue(combat) as TMP_Text, HudElementKind.Label);
        }
        var targets = combat?.GetTargetList();
        var affiliation = LabelAffiliation(targets != null && targets.Count > 0 ? targets[0] : null);
        // These are shared native labels for the first selected target, not
        // children of each HUDUnitMarker. Do not infer affiliation from color
        // (selection changes it) or scale every marker during its update loop.
        if (_targetTextStyle != null) _targetTextStyle.SetAircraftLabelAffiliation(affiliation);
        if (_targetInfoStyle != null) _targetInfoStyle.SetAircraftLabelAffiliation(affiliation);
    }

    internal static AircraftLabelAffiliation LabelAffiliation(Unit? unit) => unit is Aircraft
        ? DynamicMap.GetFactionMode(unit.NetworkHQ) switch
        {
            FactionMode.Friendly => AircraftLabelAffiliation.Friendly,
            FactionMode.Enemy => AircraftLabelAffiliation.Hostile,
            _ => AircraftLabelAffiliation.Other
        }
        : AircraftLabelAffiliation.Other;

    private void RestoreAircraftLabels()
    {
        if (_targetTextStyle != null) _targetTextStyle.SetAircraftLabelAffiliation(AircraftLabelAffiliation.Other);
        if (_targetInfoStyle != null) _targetInfoStyle.SetAircraftLabelAffiliation(AircraftLabelAffiliation.Other);
    }
    private void OnDisable() => RestoreAircraftLabels();

    private void LateUpdate()
    {
        var combat = SceneSingleton<CombatHUD>.i;
        UpdateAircraftLabels(combat);
        if (Time.unscaledTime < _nextScan) return;
        _nextScan = Time.unscaledTime + .5f;
        var flight = SceneSingleton<FlightHud>.i;
        if (flight != null)
        {
            HudElementStyle.Add(flight.velocityVector, HudElementKind.Reticle);
            HudElementStyle.Add(flight.waterline, HudElementKind.Reticle);
        }
        if (combat == null) return;
        HudElementStyle.Add(combat.targetDesignator, HudElementKind.Reticle);
        HudElementStyle.Add(TargetArrowField.GetValue(combat) as Graphic, HudElementKind.Marker);
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
