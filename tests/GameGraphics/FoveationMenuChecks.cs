using NOVR;
using NOVR.Rendering;
using NOVR.VrUi;

internal static class FoveationMenuChecks
{
    internal static void Run(Action<bool, string> check)
    {
        // Existing saved indices must retain their meanings when the page moves
        // from four buttons to a six-position slider, even on unsupported PCs.
        var labels = new[] { "Off", "Mild", "Balanced", "Strong", "Extreme", "Maximum" };
        foreach (var available in new[] { false, true })
        foreach (var level in Enumerable.Range(0, labels.Length))
        {
            FixedFoveationRuntime.Reset(available);
            var settings = ModConfiguration.Instance.FixedFoveation;
            settings.Level.Value = (FixedFoveationLevel)level;
            settings.Level.Writes = 0;
            var menu = new WristMenu();
            var page = menu.BuildFoveation();
            menu.Refresh();
            var slider = menu.Scalars.Single().Value;
            check(page.Name == "FOVEATED RENDERING" && menu.Choices.Count == 0 &&
                  slider.Min == 0 && slider.Max == 5 && slider.Step == 1,
                "Foveation uses one six-position strength slider, including Off.");
            check(slider.Read() == level && slider.Format(slider.Read()) == labels[level] &&
                  settings.Level.Writes == 0 && settings.UseEyeTracking.Writes == 0 &&
                  settings.MeasureGpuCost.Writes == 0 && FixedFoveationRuntime.Calls.Count == 0,
                "Building/refreshing preserves saved level and independent options: " + labels[level]);
            check(slider.Slider.interactable == (available || level != 0),
                "Unsupported setups can turn an existing selection Off but cannot opt in from Off.");
        }

        FixedFoveationRuntime.Reset(true);
        var supported = new WristMenu();
        var supportedPage = supported.BuildFoveation();
        supported.Refresh();
        var strength = supported.Scalars.Single().Value;
        foreach (var level in Enumerable.Range(0, labels.Length))
        {
            strength.Write(level);
            check(FixedFoveationRuntime.Calls[^1] == "level:" + level &&
                  ModConfiguration.Instance.FixedFoveation.RequestedLevel == (FixedFoveationLevel)level,
                "Each discrete page value reaches the runtime setter with its unchanged numeric ID.");
        }
        var eyes = supportedPage.transform.Checks["Follow eye tracking"];
        var measure = supportedPage.transform.Checks["Measure GPU cost"];
        eyes.Write(true);
        check(ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value &&
              ModConfiguration.Instance.FixedFoveation.RequestedLevel == FixedFoveationLevel.Maximum &&
              !ModConfiguration.Instance.FixedFoveation.MeasureGpuCost.Value,
            "Eye tracking is independently selectable and does not change strength or measurements.");
        strength.Write(1);
        check(ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value,
            "Strength changes retain the user's independent eye-tracking preference.");
        measure.Write(true);
        check(ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value &&
              ModConfiguration.Instance.FixedFoveation.RequestedLevel == FixedFoveationLevel.Mild &&
              ModConfiguration.Instance.FixedFoveation.MeasureGpuCost.Value,
            "GPU diagnostics remain independently selectable.");

        FixedFoveationRuntime.IsAvailable = false;
        FixedFoveationRuntime.Status = "Unavailable on Linux/Proton.";
        FixedFoveationRuntime.EyeTrackingStatus = "Eye tracking inactive on this setup.";
        supported.Refresh();
        check(strength.Slider.interactable && eyes.Toggle.interactable && measure.Toggle.interactable &&
              supported.LiveText.SequenceEqual(new[] { FixedFoveationRuntime.EyeTrackingStatus, FixedFoveationRuntime.Status }),
            "Losing support displays runtime reasons and retains the ability to disable every saved option.");
        strength.Write(0);
        eyes.Write(false);
        measure.Write(false);
        supported.Refresh();
        check(ModConfiguration.Instance.FixedFoveation.RequestedLevel == FixedFoveationLevel.Off &&
              !strength.Slider.interactable && !eyes.Toggle.interactable && !measure.Toggle.interactable,
            "Explicit Off reaches the runtime; unsupported controls disable after saved options are cleared.");
        FixedFoveationRuntime.IsAvailable = true;
        supported.Refresh();
        check(strength.Slider.interactable && eyes.Toggle.interactable && measure.Toggle.interactable,
            "Controls become available again without changing saved settings.");

        var widgets = supportedPage.transform.Widgets;
        check(widgets.Count == 8 && widgets.All(w => w.Left >= -310 && w.Right <= 310 && w.Top <= 232 && w.Bottom >= -244),
            "Actual page positions keep all eight rows inside the shared menu header/footer and side bounds.");
        var rows = widgets.OrderByDescending(w => w.Y).ToArray();
        check(rows.Zip(rows.Skip(1)).All(pair => pair.First.Bottom - pair.Second.Top >= 4),
            "Foveation controls, live labels and notes have at least four pixels of vertical separation.");
        check(widgets.Where(w => w.Label == "Live text").Select(w => w.Height).SequenceEqual(new[] { 32f, 94f }),
            "The page reserves distinct gaze text and a four-line runtime status area.");
        check(supported.Notes.Any(note => note.Contains("test levels")) &&
              supported.Notes.Any(note => note.Contains("4×4") && note.Contains("MSAA off") && note.Contains("2×2")) &&
              supported.Notes.Any(note => note.Contains("not total frame time or FPS")),
            "Visible notes identify the aggressive test levels, MSAA rate cap and measurement scope.");
    }
}
