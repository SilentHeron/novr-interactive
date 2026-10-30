using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NOVR.MotionControls;

namespace NOVR.GameplayPatches;

[HarmonyPatch(typeof(CombatHUD), "TargetSelect")]
internal static class OpticalHeadSelection
{
    private static void Prefix(CombatHUD __instance, out (int count, Unit? first) __state)
    {
        OpticalTargetingHud.AlignDesignator(__instance);
        var targets = __instance.aircraft?.weaponManager?.GetTargetList();
        __state = (targets?.Count ?? 0, targets != null && targets.Count > 0 ? targets[0] : null);
    }
    private static void Postfix(CombatHUD __instance, bool paint, (int count, Unit? first) __state)
        => OpticalTargetingHud.SelectionFinished(__instance, paint, __state.count, __state.first);

    private static float Radius(float original) => OpticalTargetingHud.Instance?.AcquisitionRadius(original) ?? original;

    // Keep the native selection algorithm; change only its acquisition circle
    // in HEAD mode. A changed game IL contract fails visibly instead of silently
    // patching an unrelated weapon-range constant.
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = new List<CodeInstruction>(instructions); var replacements = 0;
        for (var i = 0; i < code.Count; i++)
        {
            yield return code[i];
            if (i + 1 < code.Count && code[i].opcode == OpCodes.Ldc_R4 && code[i].operand is float radius && radius == 100 &&
                code[i + 1].operand is MethodInfo call && call.DeclaringType == typeof(FastMath) && call.Name == "InRange")
            {
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OpticalHeadSelection), nameof(Radius)));
                replacements++;
            }
        }
        if (replacements != 1) throw new InvalidOperationException("NOVR optical targeting: native acquisition-circle contract changed.");
    }
}

[HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.DeselectLast))]
internal static class OpticalHeadUnlock
{
    private static void Postfix() => OpticalTargetingHud.LookToAim();
}

[HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.DeselectAll))]
internal static class OpticalHeadClear
{
    private static void Postfix() => OpticalTargetingHud.LookToAim();
}
