using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NOVR.Compatibility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

internal sealed class TargetingHudStyle : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<CombatHUD, TextMeshProUGUI>? TargetText =
        GameField.Optional<CombatHUD, TextMeshProUGUI>("targetText");
    private static readonly AccessTools.FieldRef<CombatHUD, TextMeshProUGUI>? TargetInfo =
        GameField.Optional<CombatHUD, TextMeshProUGUI>("targetInfo");
    private static readonly AccessTools.FieldRef<CombatHUD, Image>? TargetArrow =
        GameField.Optional<CombatHUD, Image>("targetArrow");
    // The one weapon widget on the HUD. CombatHUD.ShowWeaponStation is the
    // only game code that creates one, and it stores the new widget here.
    private static readonly AccessTools.FieldRef<CombatHUD, HUDWeaponState>? WeaponState =
        GameField.Optional<CombatHUD, HUDWeaponState>("weaponState");
    // The graphic-typed fields of each weapon-state class, looked up once.
    private static readonly Dictionary<Type, FieldInfo[]> GraphicFields = new();
    private readonly List<HUDWeaponState> _states = new(), _scratch = new();
    private float _nextScan;
    private HUDWeaponState? _styledWeapon;
    private CombatHUD? _labelOwner;
    private HudElementStyle? _targetTextStyle, _targetInfoStyle;

    private void UpdateAircraftLabels(CombatHUD? combat)
    {
        if (_labelOwner != combat)
        {
            RestoreAircraftLabels();
            _labelOwner = combat;
            _targetTextStyle = combat == null || TargetText == null ? null : HudElementStyle.Add(TargetText(combat), HudElementKind.Label);
            _targetInfoStyle = combat == null || TargetInfo == null ? null : HudElementStyle.Add(TargetInfo(combat), HudElementKind.Label);
        }
        var targets = combat?.GetTargetList();
        var affiliation = LabelAffiliation(targets != null && targets.Count > 0 ? targets[0] : null);
        // These are shared native labels for the first selected target, not
        // children of each HUDUnitMarker. Do not infer affiliation from color
        // (selection changes it) or scale every marker during its update loop.
        if (_targetTextStyle != null)
            _targetTextStyle.SetAircraftLabelAffiliation(affiliation);
        if (_targetInfoStyle != null)
            _targetInfoStyle.SetAircraftLabelAffiliation(affiliation);
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
        if (_targetTextStyle != null)
            _targetTextStyle.SetAircraftLabelAffiliation(AircraftLabelAffiliation.Other);
        if (_targetInfoStyle != null)
            _targetInfoStyle.SetAircraftLabelAffiliation(AircraftLabelAffiliation.Other);
    }
    private static int _enabledCount;
    internal static bool Styling => _enabledCount > 0;
    private void OnEnable() => _enabledCount++;
    private void OnDisable()
    {
        _enabledCount--;
        RestoreAircraftLabels();
    }

    private void LateUpdate()
    {
        var combat = SceneSingleton<CombatHUD>.i;
        UpdateAircraftLabels(combat);
        // The game replaces the weapon widget on every weapon switch. Comparing
        // one reference each frame styles a new widget before it is first
        // drawn, even if the switch patch could not be installed.
        var weapon = combat != null && WeaponState != null ? WeaponState(combat) : null;
        if (!ReferenceEquals(weapon, _styledWeapon))
        {
            _styledWeapon = weapon;
            if (weapon != null)
                StyleWeaponState(weapon);
        }
        if (Time.unscaledTime < _nextScan)
            return;
        _nextScan = Time.unscaledTime + .5f;
        var flight = SceneSingleton<FlightHud>.i;
        if (flight != null)
        {
            HudElementStyle.Add(flight.velocityVector, HudElementKind.Reticle);
            HudElementStyle.Add(flight.waterline, HudElementKind.Reticle);
        }
        if (combat == null)
            return;
        HudElementStyle.Add(combat.targetDesignator, HudElementKind.Reticle);
        HudElementStyle.Add(TargetArrow != null ? TargetArrow(combat) : null, HudElementKind.Marker);
        if (WeaponState != null)
        {
            // Re-assert the current widget's styles without looking for it.
            if (weapon != null)
                StyleWeaponState(weapon);
            return;
        }
        // This game build has no weaponState field: find widgets the old way,
        // leaving out the layer that holds one object per unit marker.
        if (flight == null)
            return;
        HudHierarchy.Collect(flight.transform, combat.iconLayer, _states, _scratch);
        foreach (var state in _states)
            StyleWeaponState(state);
        _states.Clear();
    }

    // CombatHUD.ShowWeaponStation destroys the previous weapon widget and
    // instantiates a fresh native-size prefab on every switch. Left to the
    // periodic pass it would stay unstyled (about twice the VR size) for up to
    // half a second, so the switch patch styles it before its first frame.
    internal static void StyleWeaponState(HUDWeaponState state)
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
            return;
        }
        foreach (var field in FieldsOf(state.GetType()))
        {
            var value = field.GetValue(state);
            if (value is Graphic single)
                Register(state, field, single);
            else if (value is Graphic[] graphics)
                foreach (var graphic in graphics)
                    if (graphic != null)
                        Register(state, field, graphic);
        }
    }

    private static FieldInfo[] FieldsOf(Type type)
    {
        if (GraphicFields.TryGetValue(type, out var known))
            return known;
        var fields = new List<FieldInfo>();
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            if (typeof(Graphic).IsAssignableFrom(field.FieldType) || typeof(Graphic[]).IsAssignableFrom(field.FieldType))
                fields.Add(field);
        return GraphicFields[type] = fields.ToArray();
    }

    private static void Register(HUDWeaponState state, FieldInfo field, Graphic graphic)
    {
        // Shared target-designator graphics belong to the flight HUD.
        if (!graphic.transform.IsChildOf(state.transform))
            return;
        var kind = graphic is TMP_Text ? HudElementKind.Label :
            field.Name == "line" ? HudElementKind.HorizontalLine :
            field.Name == "ccipLine" || field.Name == "alignmentBar" ? HudElementKind.VerticalLine : HudElementKind.Reticle;
        HudElementStyle.Add(graphic, kind);
    }
}
