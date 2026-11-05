using NOVR;
using NOVR.MotionControls;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class SeatPageChecks
{
    internal static void Run(Action<bool, string> check)
    {
        const string page = "SEAT & HANDLES";
        var config = new ModConfiguration();
        ModConfiguration.Instance = config;
        var profiles = new CockpitProfiles();
        CockpitProfiles.Instance = profiles;
        var widgets = new CockpitHudWidgets();
        CockpitHudWidgets.Instance = widgets;
        var menu = new WristMenu();
        menu.Build();
        var closes = WristMenu.Closes;
        menu.BuildSeat();
        var clearance = menu.SeatToggles["Head clearance"].Toggle;
        var radius = menu.SeatSliders["Clearance radius"];
        check(!clearance.isOn && !config.SeatClearanceEnabled.Value,
            "Opening Seat & handles does not opt into clearance.");
        check(radius.Slider.minValue == .06f && radius.Slider.maxValue == .20f && radius.Value.text == "12 cm",
            "Clearance radius is readable in cm and retains the configured bounds.");
        check(clearance.transform.parent.gameObject.Name == page && radius.Slider.transform.parent.gameObject.Name == page,
            "Clearance controls live directly on Seat & handles.");
        radius.Slider.value = .155f;
        check(Math.Abs(config.SeatClearanceRadius.Value - .155f) < .00001f && !config.SeatClearanceEnabled.Value,
            "Choosing a radius does not enable the experimental feature.");
        radius.Slider.onValueChanged.Invoke(.01f);
        check(config.SeatClearanceRadius.Value == .06f, "A radius callback below the slider range clamps to 6 cm.");
        radius.Slider.onValueChanged.Invoke(1f);
        check(config.SeatClearanceRadius.Value == .20f, "A radius callback above the slider range clamps to 20 cm.");
        radius.Slider.onValueChanged.Invoke(float.NaN);
        check(config.SeatClearanceRadius.Value == .20f, "Invalid slider callbacks leave the saved radius unchanged.");
        clearance.isOn = true;
        check(config.SeatClearanceEnabled.Value, "Head clearance enables only on a deliberate toggle.");
        NOVR.Comfort.SeatClearance.Status = "Seat not supported";
        menu.RefreshSeat();
        check(menu.ClearanceHint.text == "Seat not supported", "Enabled clearance displays the actual runtime support status.");
        NOVR.Comfort.SeatClearance.Status = "Seat detected";
        menu.RefreshSeat();
        check(menu.ClearanceHint.text == "Seat detected", "Status text refreshes when the active cockpit changes.");
        clearance.isOn = false;
        check(!config.SeatClearanceEnabled.Value && config.SeatClearanceRadius.Value == .20f,
            "Disabling clearance preserves the chosen radius.");
        menu.RefreshSeat();
        check(menu.ClearanceHint.text == "Opt-in · off by default", "Disabled clearance restores the experimental opt-in hint.");
        config.SeatClearanceEnabled.Value = true;
        config.SeatClearanceRadius.Value = .14f;
        menu.RefreshSeat();
        check(clearance.isOn && radius.Value.text == "14 cm", "Live setting refresh reflects saved values without changing them.");
        check(profiles.SeatMoves.Count == 0 && widgets.Begins == 0 && WristMenu.Closes == closes,
            "Clearance edits do not move the seat, start layout editing or close the menu.");

        var moves = new[] {
            ("Seat up", Vector3.up), ("Seat down", Vector3.down),
            ("Seat left", Vector3.left), ("Seat right", Vector3.right),
            ("Seat forward", Vector3.forward), ("Seat back", Vector3.back)
        };
        foreach (var (label, delta) in moves)
        {
            menu.Click(page, label);
            check(profiles.SeatMoves.Last() == delta * .02f, "The existing 2 cm seat action is preserved: " + label);
        }
        menu.Click(page, "Reset seat & handles…");
        check(menu.ResetRequests == 1, "Reset still opens the existing confirmation.");
        menu.Click(page, "Aircraft profiles  ›");
        check(menu.ActivePage == "AIRCRAFT PROFILES", "Aircraft profiles remains accessible.");
        menu.Click(page, "Edit layout");
        check(widgets.Preview && widgets.Begins == 1, "Edit layout retains the existing preview entry point.");
        menu.SeatToggles["Stick in left hand"].Toggle.isOn = true;
        check(config.MotionControls.LeftHanded.Value && !widgets.Preview,
            "Handedness retains its existing pending-layout cancellation.");
    }
}
