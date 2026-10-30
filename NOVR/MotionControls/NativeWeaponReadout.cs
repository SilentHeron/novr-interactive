using HarmonyLib;
using TMPro;

namespace NOVR.MotionControls;

internal sealed class NativeWeaponReadout
{
    private static readonly System.Reflection.FieldInfo? StateField = AccessTools.Field(typeof(CombatHUD), "weaponState");
    private static readonly System.Reflection.FieldInfo? MissileHint = AccessTools.Field(typeof(HUDMissileState), "hint");
    private static readonly System.Reflection.FieldInfo? LaserHint = AccessTools.Field(typeof(HUDLaserGuidedState), "hint");
    private HUDWeaponState? _state;
    private TMP_Text? _hint;

    internal (string text, bool caution) Read(Aircraft aircraft)
    {
        var station = aircraft.weaponManager?.currentWeaponStation;
        var selected = aircraft.weaponManager?.GetTargetList()?.Count ?? 0;
        var hud = SceneSingleton<CombatHUD>.i;
        var state = hud != null && hud.aircraft == aircraft && hud.GetWeaponStation() == station
            ? StateField?.GetValue(hud) as HUDWeaponState : null;
        if (state == null || state != _state)
        {
            _state = state;
            _hint = state is HUDMissileState ? MissileHint?.GetValue(state) as TMP_Text :
                state is HUDLaserGuidedState ? LaserHint?.GetValue(state) as TMP_Text : null;
        }
        // Never interpret a hidden or stale native cue as a lock/launch clearance.
        var cue = _hint != null && _hint.isActiveAndEnabled ? _hint.text : null;
        var fitted = station?.WeaponInfo != null;
        return WeaponReadout.Describe(selected, fitted, fitted && station!.SafetyIsOn(aircraft),
            station?.Ammo ?? 0, station?.Reloading == true, fitted && station!.Ready(), cue);
    }
}
