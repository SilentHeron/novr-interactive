using BepInEx.Configuration;
using NOVR.VrUi;
using UnityEngine;

internal static class ControlChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var parent = new GameObject("Settings").transform;
        var font = new Font();
        var saved = .333333f;
        var writes = 0;
        var slider = ViewMenuControls.Slider(parent, font, "Opacity", 150, .05f, 1, () => saved, value => { saved = value; writes++; }, value => $"{value * 100:0}%", .01f);
        check(writes == 0 && saved == .333333f, "Building a scalar control does not quantize or save the current value");
        slider.Refresh();
        slider.Refresh();
        check(writes == 0, "Refresh never emits slider writes");
        check(slider.Slider.minValue == .05f && slider.Slider.maxValue == 1, "Slider takes real setting bounds");
        check(slider.Slider.gameObject.GetComponent<UnityEngine.UI.Image>().raycastTarget, "Full slider row remains a point-and-drag target");
        foreach (var child in Descendants(slider.Slider.transform))
        {
            // Decorative child images must never intercept the controller ray.
            try
            {
                check(!child.GetComponent<UnityEngine.UI.Image>().raycastTarget, "Slider track/fill/handle is not a separate click target");
            }
            catch (KeyNotFoundException) { }
        }
        slider.Slider.value = .77f;
        check(writes == 1 && Math.Abs(saved - .77f) < .00001f && slider.Value.text == "77%", "A drag writes one bounded value and updates its readout");
        slider.Slider.onValueChanged.Invoke(float.NaN);
        slider.Slider.onValueChanged.Invoke(float.PositiveInfinity);
        check(writes == 1 && saved < 1, "Malformed UI callbacks cannot poison a setting");
        slider.Slider.interactable = false;
        slider.Slider.onValueChanged.Invoke(.4f);
        check(writes == 1, "Disabled slider rejects late callbacks");
        slider.Slider.interactable = true;
        slider.Slider.value = 2;
        check(saved == 1, "Slider endpoint is clamped to its setting maximum");
        saved = .123456f;
        var before = writes;
        slider.Refresh();
        check(saved == .123456f && writes == before && slider.Slider.value == saved, "External settings changes synchronize without rewriting precision");
        var entry = new ConfigEntry<float>(.12f, new ConfigDescription(new AcceptableValueRange<float>(.05f, .6f)));
        var bound = ViewMenuControls.Slider(parent, font, "Travel", 70, entry, value => $"{value * 100:0} cm", .005f);
        check(bound.Slider.minValue == .05f && bound.Slider.maxValue == .6f, "Config-backed slider reads its bounds from ConfigDescription");
        bound.Slider.value = .2f;
        check(Math.Abs(entry.Value - .2f) < .000001f, "Config-backed slider routes changes to that existing entry");
        var rejected = false;
        try
        {
            ViewMenuControls.Slider(parent, font, "Unbounded", 0, new ConfigEntry<float>(1, new ConfigDescription(null)), x => x.ToString());
        }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "A numeric setting without declared bounds cannot quietly get arbitrary slider limits");
        var on = true;
        var toggles = 0;
        var box = ViewMenuControls.Checkbox(parent, font, "Show sleeves", new Vector2(0, 0), 300, () => on, value => { on = value; toggles++; });
        check(box.Toggle.isOn && toggles == 0, "Checkbox starts from saved state without toggling it");
        box.Toggle.isOn = false;
        check(!on && toggles == 1, "One checkbox press writes one real boolean transition");
        on = true;
        box.Refresh();
        check(box.Toggle.isOn && toggles == 1, "Checkbox refresh never generates a second setting event");
        box.Toggle.interactable = false;
        box.Toggle.onValueChanged.Invoke(false);
        check(on && toggles == 1, "Disabled checkbox rejects a late event");
        var config = new NOVR.ModConfiguration();
        var hud = ViewMenuControls.Slider(parent, font, "Flight HUD", 0, config.FlightHudScale, x => $"{x * 100:0}%", .01f, value => WristMenuSettingsActions.FlightHudSize(config, value));
        check(!config.FocusedFlightHud.Value && config.FlightHudScale.Value == .38f, "Reading combined HUD size does not enable focused mode");
        hud.Slider.value = .48f;
        check(config.FocusedFlightHud.Value && Near(config.FlightHudScale.Value, .48f) && Near(config.MarkerScale.Value, .55f) &&
            Near(config.MarkerLabelScale.Value, .65f) && Near(config.ReticleScale.Value, .55f) && Near(config.WeaponHudScale.Value, .6f),
            "Combined HUD slider restores focused mode and preserves every legacy relative-size delta");
        hud.Slider.value = .38f;
        check(Near(config.MarkerScale.Value, .45f) && Near(config.MarkerLabelScale.Value, .55f) && Near(config.ReticleScale.Value, .45f) && Near(config.WeaponHudScale.Value, .5f),
            "Reversing combined HUD adjustment restores each independent initial size");
        config.SymbolsFollowFlightHud.Value = true;
        hud.Slider.value = .5f;
        check(config.FocusedFlightHud.Value && Near(config.FlightHudScale.Value, .5f) && Near(config.MarkerScale.Value, .45f) &&
            Near(config.MarkerLabelScale.Value, .55f) && Near(config.ReticleScale.Value, .45f) && Near(config.WeaponHudScale.Value, .5f),
            "With symbols following Flight HUD Scale, the combined slider does not also nudge the symbol sliders");
        hud.Slider.value = .38f;
        config.SymbolsFollowFlightHud.Value = false;
        foreach (var left in new[] { false, true })
        {
            check(WristMenuSettingsActions.PalmPreset(new Vector3(0, 0, left ? 90 : -90), left) == 0, "Only the fitted upright orientation selects the upright preset");
            check(WristMenuSettingsActions.PalmPreset(Vector3.zero, left) == 1, "Neutral palm-down orientation selects the down preset");
            check(WristMenuSettingsActions.PalmPreset(new Vector3(0, 0, 180), left) == -1 && WristMenuSettingsActions.PalmPreset(new Vector3(30, 0, left ? 90 : -90), left) == -1,
                "Custom wrist poses do not falsely highlight a named preset");
        }
        var widgets = new NOVR.VrUi.SpecialBehavior.CockpitHudWidgets();
        NOVR.VrUi.SpecialBehavior.CockpitHudWidgets.Instance = widgets;
        var pose = widgets.GetPose("weapons");
        pose.Background = .333333f;
        widgets.SetAppearance("weapons", pose);
        var initialWrites = widgets.AppearanceSaves;
        var menu = new WristMenu();
        menu.Build();
        check(widgets.GetPose("weapons").Background == .333333f && widgets.AppearanceSaves == initialWrites,
            "Constructing the actual widget page preserves fractional saved opacity without a write");
        foreach (var value in new[] { -.1f, 0f, .05f, .12f, .333f, .6f, 1f, 2f })
            check(ViewMenuValue.Snap(value, .05f, .6f, .005f) >= .05f && ViewMenuValue.Snap(value, .05f, .6f, .005f) <= .6f, "Slider step rounding always stays within bounds");
    }
    private static bool Near(float a, float b) => Math.Abs(a - b) < .00001f;
    private static IEnumerable<Transform> Descendants(Transform root)
    {
        foreach (var child in root.children)
        {
            yield return child;
            foreach (var next in Descendants(child))
                yield return next;
        }
    }
}
