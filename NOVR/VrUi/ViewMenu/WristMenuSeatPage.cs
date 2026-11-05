using NOVR.MotionControls;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

namespace NOVR.VrUi;

internal sealed partial class WristMenu
{
    private void BuildSeatPage()
    {
        _seatPage = Page("SEAT & HANDLES");
        var parent = _seatPage.transform;
        var config = ModConfiguration.Instance;
        var motion = config.MotionControls;
        Section(parent, "Seat · 2 cm steps · handles & HUD stay fixed", 214);
        ActionButton(parent, "Seat up", 0, 170, () => CockpitProfiles.Instance?.MoveSeat(Vector3.up * .02f));
        ActionButton(parent, "Seat down", 1, 170, () => CockpitProfiles.Instance?.MoveSeat(Vector3.down * .02f));
        ActionButton(parent, "Seat left", 0, 126, () => CockpitProfiles.Instance?.MoveSeat(Vector3.left * .02f));
        ActionButton(parent, "Seat right", 1, 126, () => CockpitProfiles.Instance?.MoveSeat(Vector3.right * .02f));
        ActionButton(parent, "Seat forward", 0, 82, () => CockpitProfiles.Instance?.MoveSeat(Vector3.forward * .02f));
        ActionButton(parent, "Seat back", 1, 82, () => CockpitProfiles.Instance?.MoveSeat(Vector3.back * .02f));
        Section(parent, "Handles & layout", 38);
        Setting(parent, "Stick in left hand", () => motion.LeftHanded.Value, value => { CockpitHudWidgets.Instance?.Finish(false); motion.LeftHanded.Value = value; Close(); }, 0, -6);
        ActionButton(parent, "Reset seat & handles…", 1, -6, AskReset, true);
        ActionButton(parent, "Edit layout", 0, -54, StartOrResumeLayout);
        ActionButton(parent, "Aircraft profiles  ›", 1, -54, OpenAircraftProfiles);
        Section(parent, "Comfort · experimental", -100);
        Setting(parent, "Head clearance", config.SeatClearanceEnabled, 0, -143);
        var hint = ViewMenuControls.Text(parent, _font!, "Opt-in · off by default", new Vector2(160, -143), new Vector2(300, 42), 17);
        hint.color = ViewMenuControls.Muted;
        _settingRefresh.Add((parent.gameObject, () => hint.text = config.SeatClearanceEnabled.Value
            ? Comfort.SeatClearance.Status : "Opt-in · off by default"));
        Setting(parent, "Clearance radius", config.SeatClearanceRadius, -204, Centimeters, .005f);
    }
}
