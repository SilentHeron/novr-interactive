using System.Text;
using HarmonyLib;
using NOVR.VrUi.SpecialBehavior;
using TMPro;

namespace NOVR.VrUi.HarmonyPatches;

[HarmonyPatch(typeof(MessageFeed), "RefreshUI")]
internal static class NativeMessageFeedPatch
{
    // Includes delayed HQ lines, ordinary kills, expiry and ClearMessages.
    // The native queue, radio audio, filters and timers remain authoritative.
    [HarmonyPostfix]
    internal static void Postfix(TMP_Text ____display, StringBuilder ____sb, bool ____hasContent) =>
        NativeNotificationText.Record(____display, ____sb, ____hasContent);
}
