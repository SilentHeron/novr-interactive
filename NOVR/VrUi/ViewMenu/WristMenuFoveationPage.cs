using NOVR.Rendering;
using UnityEngine;

namespace NOVR.VrUi;

internal sealed partial class WristMenu
{
    private GameObject BuildFixedFoveationPage()
    {
        var page = Page("FOVEATED RENDERING");
        var parent = page.transform;
        Section(parent, "Experimental · PC rendering", 214);
        var strength = Setting(parent, "Strength (fixed & eye-tracked)", 159, 0, (int)FixedFoveationLevel.Maximum,
            () => (int)ModConfiguration.Instance.FixedFoveation.RequestedLevel,
            value => FixedFoveationRuntime.SetLevel((int)value),
            value => FixedFoveationPolicy.Normalize((int)value).ToString(), 1);
        _settingRefresh.Add((page, () => strength.Slider.interactable = FixedFoveationRuntime.IsAvailable ||
            ModConfiguration.Instance.FixedFoveation.RequestedLevel != FixedFoveationLevel.Off));
        var eyeTracking = ViewMenuControls.Checkbox(parent, _font!, "Follow eye tracking", new Vector2(0, 103), 620,
            () => ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value, FixedFoveationRuntime.SetEyeTracking);
        _settingRefresh.Add((page, () =>
        {
            eyeTracking.Refresh();
            eyeTracking.Toggle.interactable = FixedFoveationRuntime.IsAvailable ||
                ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value;
        }));
        var gaze = ViewMenuControls.Text(parent, _font!, "", new Vector2(0, 60), new Vector2(608, 32), 16);
        _liveLabels.Add((gaze, () => FixedFoveationRuntime.EyeTrackingStatus));
        var status = ViewMenuControls.Text(parent, _font!, "", new Vector2(0, -7), new Vector2(608, 94), 17);
        _liveLabels.Add((status, () => FixedFoveationRuntime.Status));
        var measure = ViewMenuControls.Checkbox(parent, _font!, "Measure GPU cost", new Vector2(0, -83), 620,
            () => ModConfiguration.Instance.FixedFoveation.MeasureGpuCost.Value, FixedFoveationRuntime.SetDiagnostics);
        _settingRefresh.Add((page, () =>
        {
            measure.Refresh();
            measure.Toggle.interactable = FixedFoveationRuntime.IsAvailable ||
                ModConfiguration.Instance.FixedFoveation.MeasureGpuCost.Value;
        }));
        Note(parent, "Compare the same scene: GPU timer covers\nworld opaques; the whole-frame line shows FPS.", -135, 42);
        Note(parent, "Extreme/Maximum: test levels. Keep MSAA on:\nturning it off to unlock 4×4 edges adds\nfar more aliasing than it saves.", -202, 72);
        return page;
    }
}
