using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.SpecialBehavior;

// Keep the fitted native HUD gauge in its authored position. Only its visibility
// changes; cockpit instruments, scale, brightness and nozzle logic stay native.
[DefaultExecutionOrder(150)]
internal sealed class Fs20NozzleHud : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<NozzleGauge, INozzleGauge> SystemOf = AccessTools.FieldRefAccess<NozzleGauge, INozzleGauge>("system");
    private static readonly AccessTools.FieldRef<NozzleGauge, bool> Fade = AccessTools.FieldRefAccess<NozzleGauge, bool>("fade");
    private static readonly AccessTools.FieldRef<NozzleGauge, Image[]> Images = AccessTools.FieldRefAccess<NozzleGauge, Image[]>("fadeImages");
    private static readonly AccessTools.FieldRef<NozzleGauge, float[]> ImageAlphas = AccessTools.FieldRefAccess<NozzleGauge, float[]>("imageAlphas");
    private static readonly AccessTools.FieldRef<NozzleGauge, TextMeshProUGUI> Label = AccessTools.FieldRefAccess<NozzleGauge, TextMeshProUGUI>("label");

    private readonly struct GraphicState
    {
        private readonly Graphic _graphic;
        private readonly bool _enabled, _showEnabled;
        private readonly float _alpha, _showAlpha;
        internal GraphicState(Graphic graphic, bool showEnabled, float showAlpha)
        {
            _graphic = graphic;
            _enabled = graphic.enabled;
            _alpha = graphic.color.a;
            _showEnabled = showEnabled;
            _showAlpha = showAlpha;
        }
        internal void Apply(bool visible)
        {
            if (_graphic == null)
                return;
            _graphic.enabled = visible && _showEnabled;
            if (!visible)
                return;
            var color = _graphic.color;
            color.a = _showAlpha;
            _graphic.color = color;
        }
        internal void Restore()
        {
            if (_graphic == null)
                return;
            _graphic.enabled = _enabled;
            var color = _graphic.color;
            color.a = _alpha;
            _graphic.color = color;
        }
    }

    private readonly List<GraphicState> _graphics = new();
    private readonly NozzleHudContext _context = new();
    private NozzleGauge? _source;
    private Aircraft? _aircraft;
    private bool _owned, _nativeFade;

    internal void Bind(NozzleGauge source, Aircraft aircraft)
    {
        Restore();
        _source = source;
        _aircraft = aircraft;
    }

    private void LateUpdate()
    {
        if (!XRSettings.enabled || _source == null)
        {
            Restore();
            return;
        }
        if (!_owned)
            Begin();
        if (!_owned)
            return;
        // Native fading hides even a steady hover gauge after five seconds.
        // Continue its normal Refresh while managing only the fitted graphics.
        Fade(_source) = false;
        var ready = !CockpitAlerts.LostCockpit(_aircraft) && CameraStateManager.cameraMode == CameraMode.cockpit &&
            GameManager.GetLocalAircraft(out var current) && current == _aircraft &&
            SceneSingleton<CameraStateManager>.i != null && SceneSingleton<CameraStateManager>.i.followingUnit == _aircraft &&
            GameManager.flightControlsEnabled && Time.timeScale > 0 && !PlayerSettings.cinematicMode &&
            !DynamicMap.mapMaximized && !WristMenu.IsOpen && !CursorManager.GetFlag(CursorFlags.GameMenu) &&
            (SceneSingleton<RadialMenuMain>.i == null || !RadialMenuMain.IsInUse()) &&
            (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen);
        var system = SystemOf(_source);
        var fitted = system != null && (!(system is Object native) || native != null);
        var inputs = ready && fitted ? _aircraft!.GetInputs() : null;
        var visible = _context.Update(ready && inputs != null, fitted,
            ready && fitted ? system!.GetNozzleAngle() : 0,
            inputs != null ? inputs.customAxis1 : 1,
            ready && _aircraft!.IsAutoHoverEnabled(), Time.unscaledTime);
        foreach (var graphic in _graphics)
            graphic.Apply(visible);
    }

    private void Begin()
    {
        if (_source == null)
            return;
        var images = Images(_source);
        var alphas = ImageAlphas(_source);
        var label = Label(_source);
        if (images == null || alphas == null)
            return;
        _nativeFade = Fade(_source);
        foreach (var graphic in _source.GetComponentsInChildren<Graphic>(true))
        {
            var shown = graphic.enabled;
            var alpha = graphic.color.a;
            for (var i = 0; i < images.Length; i++)
                if (graphic == images[i])
                {
                    shown = true;
                    alpha = i < alphas.Length ? alphas[i] : alpha;
                    break;
                }
            if (graphic == label)
            {
                shown = true;
                alpha = 1;
            }
            _graphics.Add(new GraphicState(graphic, shown, alpha));
        }
        _owned = true;
    }

    private void Restore()
    {
        if (_owned && _source != null)
            Fade(_source) = _nativeFade;
        foreach (var graphic in _graphics)
            graphic.Restore();
        _graphics.Clear();
        _context.Reset();
        _owned = false;
    }
    private void OnDisable() => Restore();
    private void OnDestroy() => Restore();
}

[HarmonyPatch(typeof(NozzleGauge), nameof(NozzleGauge.Initialize))]
internal static class NozzleHudContextPatch
{
    [HarmonyPostfix]
    private static void Postfix(NozzleGauge __instance, Aircraft aircraft)
    {
        // MFD/cockpit apps can use the same gauge class. Never hide those.
        if (__instance.GetComponentInParent<HUDAppManager>() == null ||
            __instance.GetComponentInParent<FlightHud>() == null)
            return;
        var behavior = __instance.GetComponent<Fs20NozzleHud>();
        if (behavior == null)
            behavior = __instance.gameObject.AddComponent<Fs20NozzleHud>();
        behavior.Bind(__instance, aircraft);
    }
}
