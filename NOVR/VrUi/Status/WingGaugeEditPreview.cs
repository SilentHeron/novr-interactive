using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

// A faded wing gauge still needs its actual diagram visible for placement.
// Suspend only its fade flag during editing, leaving native opacity/timing
// state untouched; exact graphic values resume when the preview ends.
internal sealed class WingGaugeEditPreview
{
    private static readonly FieldInfo? Fade = AccessTools.Field(typeof(WingAngleGauge), "fade");
    private static readonly FieldInfo? Images = AccessTools.Field(typeof(WingAngleGauge), "fadeImages");
    private static readonly FieldInfo? Alphas = AccessTools.Field(typeof(WingAngleGauge), "imageAlphas");
    private static readonly FieldInfo? Label = AccessTools.Field(typeof(WingAngleGauge), "label");
    private readonly WingAngleGauge _source;
    private readonly List<Graphic> _graphics = new();
    private readonly List<(Graphic graphic, bool enabled, float alpha, bool show, float shownAlpha)> _states = new();
    private bool _active, _fade;

    internal WingGaugeEditPreview(WingAngleGauge source) => _source = source;

    internal void Update(bool editing)
    {
        if (!editing || _source == null)
        {
            Restore();
            return;
        }
        if (!_active)
        {
            // A future game revision may replace this gauge. Keep its native
            // behavior if the inspected fade fields are no longer available.
            if (Fade == null || Images == null || Alphas == null || Label == null ||
                !(Images?.GetValue(_source) is Image[] images) || !(Alphas?.GetValue(_source) is float[] alphas) ||
                !(Fade?.GetValue(_source) is bool nativeFade))
                return;
            var label = Label?.GetValue(_source) as Graphic;
            _source.GetComponentsInChildren(true, _graphics);
            foreach (var graphic in _graphics)
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
                _states.Add((graphic, graphic.enabled, graphic.color.a, shown, alpha));
            }
            _graphics.Clear();
            _fade = nativeFade;
            Fade?.SetValue(_source, false);
            _active = true;
        }
        foreach (var state in _states)
            Apply(state.graphic, state.show, state.shownAlpha);
    }

    internal void Restore()
    {
        if (_active && _source != null)
            Fade?.SetValue(_source, _fade);
        foreach (var state in _states)
            Apply(state.graphic, state.enabled, state.alpha);
        _states.Clear();
        _graphics.Clear();
        _active = false;
    }

    private static void Apply(Graphic graphic, bool enabled, float alpha)
    {
        if (graphic == null)
            return;
        graphic.enabled = enabled;
        var color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }
}
