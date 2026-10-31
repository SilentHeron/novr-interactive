using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

// Native map side panels close by moving beyond Screen.width. Those positions
// remain visible beside a world-space VR canvas; the root outline is not part
// of the displayPanel that MFDScreen already deactivates.
internal sealed class ClosedMfdBorder : MonoBehaviour
{
    private MFDScreen? _screen;
    private Image? _border;
    private bool _suppressed, _originalEnabled;

    private void Awake()
    {
        _screen = GetComponent<MFDScreen>();
        var image = GetComponent<Image>();
        if (image != null && image.type == Image.Type.Sliced && !image.fillCenter)
            _border = image;
    }

    internal void Refresh()
    {
        if (_screen == null || _border == null) return;
        if (UnityEngine.XR.XRSettings.enabled && !_screen.isActive)
        {
            if (!_suppressed) { _originalEnabled = _border.enabled; _suppressed = true; }
            _border.enabled = false;
        }
        else Restore();
    }

    private void LateUpdate() => Refresh();
    private void Restore()
    {
        if (_suppressed && _border != null) _border.enabled = _originalEnabled;
        _suppressed = false;
    }
    private void OnDisable() => Restore();
    private void OnDestroy() => Restore();
}

internal static class ClosedMfdBorderPatch
{
    private static void Refresh(MFDScreen screen)
    {
        var border = screen.GetComponent<ClosedMfdBorder>();
        if (border == null) border = screen.gameObject.AddComponent<ClosedMfdBorder>();
        border.Refresh();
    }

    [HarmonyPatch(typeof(MFDScreen), nameof(MFDScreen.CloseScreen))]
    private static class Close
    {
        [HarmonyPostfix]
        private static void Postfix(MFDScreen __instance) => Refresh(__instance);
    }

    [HarmonyPatch(typeof(MFDScreen), nameof(MFDScreen.ShowScreen))]
    private static class Show
    {
        [HarmonyPostfix]
        private static void Postfix(MFDScreen __instance) => Refresh(__instance);
    }
}
